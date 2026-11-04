using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки скоса рёбер (<see cref="BoxModelEdgeChamfer"/>) у выделенной добавляющей коробки: по точке посередине каждого из двенадцати рёбер.
    /// Тянешь точку внутрь коробки — ребро срезается наискось: насколько ушла поперёк одной стороны — настолько срез заходит на неё, так же со второй.
    /// Пандус — тянешь верхнее ребро до противоположного нижнего; вернул точку на ребро — скос убран.
    /// От точки к началу среза на обеих сторонах — тонкие линии, рядом — размеры. Всё по сетке, с Undo.
    /// </summary>
    public static class BoxModelChamferHandles
    {
        private static readonly Color HandleColor = new(1f, 0.85f, 0.2f);

        // Размер точки — доля размера ручек Unity в этом месте.
        private const float DotSizeFactor = 0.05f;


        public static void Draw(BoxModel model, BoxModelPart part)
        {
            if (part.Mode != BoxModelPartMode.Add)
                return;

            var min = BoxModelRebuilder.LocalMinOf(model, part);
            var max = min + part.Size;

            using (new Handles.DrawingScope(HandleColor, model.transform.localToWorldMatrix))
                for (var firstAxis = 0; firstAxis < 3; firstAxis++)
                    for (var secondAxis = firstAxis + 1; secondAxis < 3; secondAxis++)
                        for (var corner = 0; corner < 4; corner++)
                            DrawEdge(part, BoxFaces.Index(firstAxis, corner % 2 == 0), BoxFaces.Index(secondAxis, corner / 2 == 0), min, max);
        }


        // ── Одно ребро ───────────────────────────────────────────────────

        // Точка ребра: на самом ребре, а со скосом — сдвинута внутрь на его размеры. Тянешь — новые размеры, по сетке, не больше коробки.
        private static void DrawEdge(BoxModelPart part, int firstFace, int secondFace, Vector3 min, Vector3 max)
        {
            var firstAxis = BoxFaces.Axis(firstFace);
            var secondAxis = BoxFaces.Axis(secondFace);
            var firstSign = BoxFaces.IsPositive(firstFace) ? 1f : -1f;
            var secondSign = BoxFaces.IsPositive(secondFace) ? 1f : -1f;
            var hasChamfer = part.TryGetChamfer(firstFace, secondFace, out var chamfer);

            // Угол ребра посередине его длины.
            var edgeMiddle = (min + max) / 2f;
            edgeMiddle[firstAxis] = firstSign > 0f ? max[firstAxis] : min[firstAxis];
            edgeMiddle[secondAxis] = secondSign > 0f ? max[secondAxis] : min[secondAxis];

            var sizeOnFirst = hasChamfer ? chamfer.SizeOnFirstFace : 0f;
            var sizeOnSecond = hasChamfer ? chamfer.SizeOnSecondFace : 0f;
            var position = HandlePosition(edgeMiddle, firstAxis, firstSign, secondAxis, secondSign, sizeOnFirst, sizeOnSecond);

            if (hasChamfer)
                DrawGuides(edgeMiddle, position, firstAxis, secondAxis, chamfer);

            var size = HandleUtility.GetHandleSize(position) * DotSizeFactor;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider2D(position, AxisVector(3 - firstAxis - secondAxis), AxisVector(secondAxis), AxisVector(firstAxis), size,
                Handles.DotHandleCap, 0f);

            if (!EditorGUI.EndChangeCheck())
                return;

            // Внутрь поперёк первой стороны — это вдоль оси второй, против её знака; и наоборот.
            var newSizeOnFirst = Mathf.Clamp(BoxModelGrid.Snap((edgeMiddle[secondAxis] - moved[secondAxis]) * secondSign, secondAxis), 0f, max[secondAxis] - min[secondAxis]);
            var newSizeOnSecond = Mathf.Clamp(BoxModelGrid.Snap((edgeMiddle[firstAxis] - moved[firstAxis]) * firstSign, firstAxis), 0f, max[firstAxis] - min[firstAxis]);

            if (Mathf.Approximately(newSizeOnFirst, sizeOnFirst) && Mathf.Approximately(newSizeOnSecond, sizeOnSecond))
                return;

            Undo.RecordObject(part, "Chamfer Edge");
            part.SetChamfer(new BoxModelEdgeChamfer(firstFace, secondFace, newSizeOnFirst, newSizeOnSecond));
        }

        // Ребро, сдвинутое внутрь: на sizeOnFirst поперёк первой стороны и на sizeOnSecond поперёк второй.
        private static Vector3 HandlePosition(Vector3 edgeMiddle, int firstAxis, float firstSign, int secondAxis, float secondSign, float sizeOnFirst, float sizeOnSecond)
        {
            var position = edgeMiddle;
            position[secondAxis] -= secondSign * sizeOnFirst;
            position[firstAxis] -= firstSign * sizeOnSecond;
            return position;
        }

        // Линии от точки к началу среза на обеих сторонах и подпись с размерами.
        private static void DrawGuides(Vector3 edgeMiddle, Vector3 position, int firstAxis, int secondAxis, BoxModelEdgeChamfer chamfer)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var onFirst = edgeMiddle;
            onFirst[secondAxis] = position[secondAxis];
            var onSecond = edgeMiddle;
            onSecond[firstAxis] = position[firstAxis];

            Handles.DrawDottedLine(position, onFirst, 3f);
            Handles.DrawDottedLine(position, onSecond, 3f);
            Handles.DrawAAPolyLine(3f, onFirst, onSecond);

            // Подпись ждёт точку мира — рисуем её без матрицы объекта.
            var worldPosition = Handles.matrix.MultiplyPoint3x4(position);

            using (new Handles.DrawingScope(Matrix4x4.identity))
                BoxModelSceneText.DrawLabel(worldPosition, "скос " + BoxModelSceneText.FormatMeters(chamfer.SizeOnFirstFace) + " × " +
                                                           BoxModelSceneText.FormatMeters(chamfer.SizeOnSecondFace) + " м");
        }


        private static Vector3 AxisVector(int axis)
        {
            var vector = Vector3.zero;
            vector[axis] = 1f;
            return vector;
        }
    }
}
