using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки выделенного выреза (<see cref="GeometryCutter"/>) в Live Builder: рамка всех копий поверх стен, крупные квадраты на гранях — размер
    /// (<see cref="LiveBuilderFaceHandles"/>: противоположная грань стоит, размер — целыми клетками сетки), стрелки в середине — перенос в осях выреза.
    /// Ручки — на той копии вверх, что ближе к середине окна сцены: окно на пятом этаже правится там, где его видно. Повернуть — штатным поворотом Unity (E).
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

        // Грани коробки выреза: опора — низ посередине, поэтому после правки она переезжает в новый низ.
        // Оси ручек — только поворот выреза, без его места: иначе тянешь грань — вырез едет, оси с ним, и Unity прибавляет сдвиг снова и снова.
        private static void DrawSizeHandles(GeometryCutter cutter, Vector3 copyLift)
        {
            var rotation = cutter.transform.rotation;
            var pivot = Quaternion.Inverse(rotation) * (cutter.transform.position + copyLift);
            var min = pivot + new Vector3(-cutter.Width / 2f, 0f, -cutter.Depth / 2f);
            var max = pivot + new Vector3(cutter.Width / 2f, cutter.Height, cutter.Depth / 2f);
            var oldMin = min;
            var oldMax = max;

            if (!LiveBuilderFaceHandles.Draw(Matrix4x4.Rotate(rotation), ref min, ref max, GeometryCutterGizmos.CutterColor, AxisNames))
                return;

            LiveBuilderFaceHandles.SnapSizesToGrid(oldMin, oldMax, ref min, ref max);
            var size = max - min;
            var newBottom = rotation * new Vector3((min.x + max.x) / 2f, min.y, (min.z + max.z) / 2f);

            Undo.RecordObjects(new Object[] { cutter, cutter.transform }, "Resize Cutter");
            cutter.transform.position = newBottom - copyLift;
            cutter.Width = size.x;
            cutter.Height = size.y;
            cutter.Depth = size.z;
        }

        // Стрелки — в середине копии, не в опоре: там внизу ручка размера.
        private static void DrawMoveHandle(GeometryCutter cutter, Vector3 copyLift)
        {
            var center = cutter.transform.TransformPoint(new Vector3(0f, cutter.Height / 2f, 0f)) + copyLift;

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(center, cutter.transform.rotation);

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(cutter.transform, "Move Cutter");
            cutter.transform.position += moved - center;
        }
    }
}
