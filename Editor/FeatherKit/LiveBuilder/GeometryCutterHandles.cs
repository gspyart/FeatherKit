using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки выделенного выреза (<see cref="GeometryCutter"/>) в Live Builder: рамка всех копий поверх стен, крупные квадраты на гранях — размер
    /// (<see cref="LiveBuilderFaceHandles"/>: противоположная грань стоит, размер — целыми клетками сетки), стрелки в середине — перенос дыры в осях выреза.
    /// Ручки правят только дыру — размер и сдвиг формы, сам объект стоит: визуал внутри выреза не едет. Объект целиком — штатным переносом (W) и поворотом (E) Unity.
    /// Shift + тянуть стрелки — копия выреза целиком, с визуалом: второе окно рядом.
    /// Ручки — на той копии вверх, что ближе к середине окна сцены: окно на пятом этаже правится там, где его видно.
    /// </summary>
    public static class GeometryCutterHandles
    {
        private static readonly string[] AxisNames = { "ширина", "высота", "глубина" };

        // Пока тянешь ручку — копия та же, даже если другая стала ближе к середине окна.
        private static GeometryCutter heldCutter;
        private static int heldCopy;


        public static void Draw(GeometryCutter cutter)
        {
            var copyLift = Vector3.up * (CopyInView(cutter) * cutter.CopyStep);

            DrawOutlines(cutter);
            DrawSizeHandles(cutter, copyLift);
            DrawMoveHandle(cutter, copyLift);
        }

        // Копия вверх, на которую смотришь: её середина ближе всех к середине окна сцены.
        private static int CopyInView(GeometryCutter cutter)
        {
            if (GUIUtility.hotControl != 0 && heldCutter == cutter)
                return Mathf.Min(heldCopy, cutter.CopiesUp - 1);

            var camera = Camera.current;

            if (camera == null || cutter.CopiesUp <= 1)
                return 0;

            var best = 0;
            var bestDistance = float.MaxValue;

            for (var copy = 0; copy < cutter.CopiesUp; copy++)
            {
                var center = GeometryCutterOutline.CopyToWorld(cutter, copy).MultiplyPoint3x4(new Vector3(0f, cutter.Height / 2f, 0f));
                var viewport = camera.WorldToViewportPoint(center);
                var distance = (new Vector2(viewport.x, viewport.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude;

                if (viewport.z <= 0f || distance >= bestDistance)
                    continue;

                best = copy;
                bestDistance = distance;
            }

            heldCutter = cutter;
            heldCopy = best;
            return best;
        }

        // Рамка формы спереди и сзади у каждой копии — поверх стен: видно, что выделено, даже где значок закрыт.
        private static void DrawOutlines(GeometryCutter cutter)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var outline = GeometryCutterOutline.OutlineOf(cutter);
            var front = new Vector3[outline.Count + 1];
            var back = new Vector3[outline.Count + 1];

            for (var index = 0; index <= outline.Count; index++)
            {
                var point = outline[index % outline.Count];
                front[index] = new Vector3(point.x, point.y, cutter.Depth / 2f);
                back[index] = new Vector3(point.x, point.y, -cutter.Depth / 2f);
            }

            Handles.color = GeometryCutterGizmos.CutterColor;

            for (var copy = 0; copy < cutter.CopiesUp; copy++)
            {
                using (new Handles.DrawingScope(GeometryCutterOutline.CopyToWorld(cutter, copy)))
                {
                    Handles.DrawAAPolyLine(3f, front);
                    Handles.DrawAAPolyLine(3f, back);
                }
            }
        }

        // Грани коробки выреза: опора формы — низ посередине, поэтому после правки она переезжает в новый низ.
        // Оси ручек — поворот и масштаб выреза, без его места: иначе тянешь грань — дыра едет, оси с ней, и Unity прибавляет сдвиг снова и снова.
        // Масштаб — как у дыры (<see cref="GeometryCutterOutline.ShapeToWorld"/>): без него рамка не совпадает с дырой и грань скачет.
        private static void DrawSizeHandles(GeometryCutter cutter, Vector3 copyLift)
        {
            var frame = cutter.transform.localToWorldMatrix;
            frame.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
            var pivot = frame.inverse.MultiplyPoint3x4(GeometryCutterOutline.ShapeBottomOf(cutter) + copyLift);
            var min = pivot + new Vector3(-cutter.Width / 2f, 0f, -cutter.Depth / 2f);
            var max = pivot + new Vector3(cutter.Width / 2f, cutter.Height, cutter.Depth / 2f);
            var oldMin = min;
            var oldMax = max;

            if (!LiveBuilderFaceHandles.Draw(frame, ref min, ref max, GeometryCutterGizmos.CutterColor, AxisNames))
                return;

            LiveBuilderFaceHandles.SnapSizesToGrid(oldMin, oldMax, ref min, ref max);
            var size = max - min;
            var newBottom = frame.MultiplyPoint3x4(new Vector3((min.x + max.x) / 2f, min.y, (min.z + max.z) / 2f));

            Undo.RecordObject(cutter, "Resize Cutter");
            MoveShapeBottom(cutter, newBottom - copyLift);
            cutter.Width = size.x;
            cutter.Height = size.y;
            cutter.Depth = size.z;
        }

        // Стрелки — в середине копии, не в опоре: там внизу ручка размера. Тянешь — едет дыра.
        // С Shift в начале — копия выреза целиком, с визуалом внутри, по сетке в осях выреза: ряд окон вдоль стены (<see cref="LiveBuilderMoveHandle"/>).
        private static void DrawMoveHandle(GeometryCutter cutter, Vector3 copyLift)
        {
            var isCopyDrag = LiveBuilderMoveHandle.IsDragging(cutter.transform);
            var center = isCopyDrag
                ? LiveBuilderMoveHandle.HandlePosition
                : GeometryCutterOutline.ShapeToWorld(cutter).MultiplyPoint3x4(new Vector3(0f, cutter.Height / 2f, 0f)) + copyLift;

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(center, cutter.transform.rotation);

            if (EditorGUI.EndChangeCheck())
            {
                if (!isCopyDrag && Event.current.shift)
                {
                    LiveBuilderMoveHandle.BeginDrag(new[] { cutter.transform }, center, cutter.transform.rotation, true);
                    isCopyDrag = true;
                }

                if (isCopyDrag)
                    LiveBuilderMoveHandle.DragTo(moved);
                else
                {
                    Undo.RecordObject(cutter, "Move Cutter Hole");
                    MoveShapeBottom(cutter, GeometryCutterOutline.ShapeBottomOf(cutter) + moved - center);
                }
            }

            if (isCopyDrag)
                LiveBuilderMoveHandle.DrawDragLabel();
        }


        // Низ дыры — в эту точку мира сдвигом формы; сам объект стоит.
        public static void MoveShapeBottom(GeometryCutter cutter, Vector3 worldBottom)
        {
            cutter.ShapeOffset = cutter.transform.InverseTransformPoint(worldBottom);
        }
    }
}
