using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выделенные грани в окне сцены (<see cref="BoxModelFaceSelection"/>): подсветка и одна ручка-стрелки посередине.
    /// Тянешь — все грани сдвигаются на одно и то же расстояние по сетке, каждая только поперёк себя:
    /// выделил верх трёх коробок, потянул вверх — все три выросли одинаково. Коробка не тоньше шага сетки, всё с Undo.
    /// С Ctrl — выдавить: от каждой грани новая коробка (<see cref="BoxModelPartExtrusion"/>).
    /// Выделенные куски граней едут вместе со всеми, но по-своему: от каждого растёт коробка или вырез (<see cref="BoxModelFacePiecePushPull"/>).
    /// Тот же набор без режима «Грани»: у выделенной коробки Shift+клик по квадратам граней (<see cref="BoxModelPartHandles"/>) — подсветка здесь,
    /// а потянул квадрат любой из набранных — все грани уходят на то же расстояние наружу, каждая поперёк себя.
    /// </summary>
    public static class BoxModelFaceMoveHandle
    {
        private static readonly Color SelectedFaceColor = new(1f, 0.55f, 0.1f);

        // Пока тянешь: где была ручка и коробки в начале — сдвиг всегда считаем от них, без накопления округлений.
        private static bool isDragging;
        private static Vector3 dragStartPosition;
        private static Vector3 handlePosition;

        // Где была схваченная грань набора в начале перетаскивания её квадрата, в осях объекта.
        private static float dragStartFaceCoordinate;
        private static readonly Dictionary<BoxModelPart, (Vector3 Min, Vector3 Max)> BoxesAtDragStart = new();


        // ── Рисование ────────────────────────────────────────────────────

        public static void DrawSelection()
        {
            BoxModelPartExtrusion.FinishIfReleased();
            BoxModelPartExtrusion.DrawDepthLabel();
            BoxModelFacePiecePushPull.FinishIfReleased();
            BoxModelFacePiecePushPull.DrawDepthLabel();

            var faces = BoxModelFaceSelection.Faces;

            if (isDragging && GUIUtility.hotControl == 0)
                isDragging = false;

            if (faces.Count == 0)
                return;

            foreach (var partFace in faces)
                DrawFace(partFace, new Color(SelectedFaceColor.r, SelectedFaceColor.g, SelectedFaceColor.b, 0.25f), SelectedFaceColor);

            DrawMoveHandle(faces);
        }


        // Набор граней у выделенных коробок без режима «Грани»: только подсветка — тянут его квадраты граней.
        public static void DrawCollectedFaces()
        {
            EndDragIfReleased();

            foreach (var partFace in BoxModelFaceSelection.Faces)
                DrawFace(partFace, new Color(SelectedFaceColor.r, SelectedFaceColor.g, SelectedFaceColor.b, 0.25f), SelectedFaceColor);
        }


        // Прямоугольник грани или её куска — и для выделенных, и для той, что под мышью.
        public static void DrawFace(BoxModelPartFace partFace, Color fillColor, Color outlineColor)
        {
            var model = partFace.Model;

            if (model == null)
                return;

            if (partFace.IsPiece)
            {
                var pieceCorners = partFace.Piece.Corners();

                for (var index = 0; index < pieceCorners.Length; index++)
                    pieceCorners[index] = model.transform.TransformPoint(pieceCorners[index]);

                Handles.DrawSolidRectangleWithOutline(pieceCorners, fillColor, outlineColor);
                return;
            }

            var min = BoxModelShapeCollector.LocalMinOf(model, partFace.Part.transform);
            var max = min + partFace.Part.Size;
            var axis = BoxFaces.Axis(partFace.Face);
            var axisU = BoxFaces.SurfaceAxisU(partFace.Face);
            var axisV = BoxFaces.SurfaceAxisV(partFace.Face);
            var cornerCoordinates = new[] { (min[axisU], min[axisV]), (max[axisU], min[axisV]), (max[axisU], max[axisV]), (min[axisU], max[axisV]) };
            var corners = new Vector3[4];

            for (var index = 0; index < corners.Length; index++)
            {
                var corner = Vector3.zero;
                corner[axis] = BoxFaces.IsPositive(partFace.Face) ? max[axis] : min[axis];
                corner[axisU] = cornerCoordinates[index].Item1;
                corner[axisV] = cornerCoordinates[index].Item2;
                corners[index] = model.transform.TransformPoint(corner);
            }

            Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
        }


        // ── Перенос ──────────────────────────────────────────────────────

        private static void DrawMoveHandle(IReadOnlyList<BoxModelPartFace> faces)
        {
            var position = isDragging ? handlePosition : CenterOf(faces);

            EditorGUI.BeginChangeCheck();
            var movedPosition = Handles.PositionHandle(position, HandleRotationOf(faces));

            if (!EditorGUI.EndChangeCheck())
                return;

            if (!isDragging)
                BeginDrag(faces, position);

            handlePosition = movedPosition;

            var worldOffset = handlePosition - dragStartPosition;

            // Куски: на сколько стрелки ушли поперёк куска, наружу — плюс.
            BoxModelFacePiecePushPull.Drag(faces, piece => LiveBuilderGrid.Snap(
                piece.Model.transform.InverseTransformVector(worldOffset)[piece.Piece.Axis], piece.Piece.Axis) * piece.Piece.OutwardSign);

            // Цельные грани: с Ctrl стоят, а от каждой растёт новая коробка.
            var wholeFaces = WholeFacesOf(faces);

            if (!BoxModelPartExtrusion.TryExtrudeFaces(wholeFaces, worldOffset, EditorGUI.actionKey))
                MoveFaces(wholeFaces, partFace => LiveBuilderGrid.Snap(partFace.Model.transform.InverseTransformVector(worldOffset)[BoxFaces.Axis(partFace.Face)],
                    BoxFaces.Axis(partFace.Face)));
        }

        // Тянут квадрат грани, которая в наборе: все набранные — на то же расстояние наружу, каждая поперёк себя, по сетке; с Ctrl — выдавить от всех.
        // faceCoordinate — где сейчас схваченная грань по своей оси, в осях объекта, без сетки.
        public static void DragCollectedFaces(BoxModelPartFace grabbed, float faceCoordinate, bool isExtruding)
        {
            var faces = WholeFacesOf(BoxModelFaceSelection.Faces);
            var axis = BoxFaces.Axis(grabbed.Face);
            var isPositive = BoxFaces.IsPositive(grabbed.Face);

            if (!isDragging)
            {
                var min = BoxModelShapeCollector.LocalMinOf(grabbed.Model, grabbed.Part.transform);
                dragStartFaceCoordinate = isPositive ? min[axis] + grabbed.Part.Size[axis] : min[axis];
                BeginDrag(faces, Vector3.zero);
            }

            var outward = (faceCoordinate - dragStartFaceCoordinate) * (isPositive ? 1f : -1f);

            if (!BoxModelPartExtrusion.TryExtrudeFacesOutward(faces, outward, isExtruding))
                MoveFaces(faces, partFace => LiveBuilderGrid.Snap(outward, BoxFaces.Axis(partFace.Face)) * (BoxFaces.IsPositive(partFace.Face) ? 1f : -1f));
        }

        // Отпустили — следующее перетаскивание начнётся заново от того, где грани сейчас.
        public static void EndDragIfReleased()
        {
            if (isDragging && GUIUtility.hotControl == 0)
                isDragging = false;
        }

        // Весь жест — один шаг Ctrl+Z: в эту группу пишут и сдвиг граней, и куски, и выдавливание — каждый убирает только своё.
        private static void BeginDrag(IReadOnlyList<BoxModelPartFace> faces, Vector3 position)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Move Box Faces");
            isDragging = true;
            dragStartPosition = position;
            BoxesAtDragStart.Clear();

            foreach (var partFace in faces)
            {
                var min = BoxModelShapeCollector.LocalMinOf(partFace.Model, partFace.Part.transform);
                BoxesAtDragStart[partFace.Part] = (min, min + partFace.Part.Size);
            }
        }

        // Каждая грань — на свой сдвиг вдоль своей оси (уже по сетке), от того, где она была в начале. Две грани одной коробки двигаются вместе.
        private static void MoveFaces(IReadOnlyList<BoxModelPartFace> faces, System.Func<BoxModelPartFace, float> snappedOffsetOf)
        {
            var newBoxes = new Dictionary<BoxModelPart, (Vector3 Min, Vector3 Max, Vector3Int MovedSides)>();

            foreach (var partFace in faces)
            {
                if (!BoxesAtDragStart.TryGetValue(partFace.Part, out var startBox))
                    continue;

                if (!newBoxes.TryGetValue(partFace.Part, out var box))
                    box = (startBox.Min, startBox.Max, Vector3Int.zero);

                var axis = BoxFaces.Axis(partFace.Face);
                var snappedOffset = snappedOffsetOf(partFace);

                // MovedSides по оси: 1 — ходит верхняя грань, −1 — нижняя, 2 — обе.
                if (BoxFaces.IsPositive(partFace.Face))
                    box.Max[axis] = startBox.Max[axis] + snappedOffset;
                else
                    box.Min[axis] = startBox.Min[axis] + snappedOffset;

                box.MovedSides[axis] = box.MovedSides[axis] == 0 ? (BoxFaces.IsPositive(partFace.Face) ? 1 : -1) : 2;
                newBoxes[partFace.Part] = box;
            }

            foreach (var (part, box) in newBoxes)
            {
                var min = box.Min;
                var max = box.Max;
                KeepThickerThanStep(ref min, ref max, box.MovedSides);
                Resize(part, min, max);
            }
        }

        // Грань не проходит сквозь противоположную: коробка остаётся не тоньше шага сетки.
        // Сошлись обе грани — коробка в шаг толщиной там, где они встретились.
        private static void KeepThickerThanStep(ref Vector3 min, ref Vector3 max, Vector3Int movedSides)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var step = LiveBuilderGrid.StepOn(axis);

                if (movedSides[axis] == 0 || max[axis] - min[axis] >= step)
                    continue;

                if (movedSides[axis] == 1)
                    max[axis] = min[axis] + step;
                else if (movedSides[axis] == -1)
                    min[axis] = max[axis] - step;
                else
                {
                    min[axis] = LiveBuilderGrid.Snap((min[axis] + max[axis] - step) / 2f, axis);
                    max[axis] = min[axis] + step;
                }
            }
        }

        private static void Resize(BoxModelPart part, Vector3 localMin, Vector3 localMax)
        {
            var model = part.GetComponentInParent<BoxModel>();
            var currentMin = BoxModelShapeCollector.LocalMinOf(model, part.transform);

            if (currentMin == localMin && part.Size == localMax - localMin)
                return;

            Undo.RecordObjects(new Object[] { part, part.transform }, "Move Box Faces");
            part.transform.position = model.transform.TransformPoint(localMin);
            part.Size = localMax - localMin;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static Vector3 CenterOf(IReadOnlyList<BoxModelPartFace> faces)
        {
            var sum = Vector3.zero;

            foreach (var partFace in faces)
            {
                if (partFace.IsPiece)
                {
                    sum += partFace.Model.transform.TransformPoint(partFace.Piece.Center);
                    continue;
                }

                var min = BoxModelShapeCollector.LocalMinOf(partFace.Model, partFace.Part.transform);
                var center = min + partFace.Part.Size / 2f;
                var axis = BoxFaces.Axis(partFace.Face);
                center[axis] = BoxFaces.IsPositive(partFace.Face) ? min[axis] + partFace.Part.Size[axis] : min[axis];
                sum += partFace.Model.transform.TransformPoint(center);
            }

            return sum / faces.Count;
        }

        // Грани целиком, без кусков: их двигают и выдавливают как коробки.
        private static List<BoxModelPartFace> WholeFacesOf(IReadOnlyList<BoxModelPartFace> faces)
        {
            var wholeFaces = new List<BoxModelPartFace>();

            foreach (var partFace in faces)
                if (!partFace.IsPiece)
                    wholeFaces.Add(partFace);

            return wholeFaces;
        }

        // Стрелки — по осям объекта, если все грани из одного; из разных — по осям мира.
        private static Quaternion HandleRotationOf(IReadOnlyList<BoxModelPartFace> faces)
        {
            var model = faces[0].Model;

            foreach (var partFace in faces)
                if (partFace.Model != model)
                    return Quaternion.identity;

            return model.transform.rotation;
        }
    }
}
