using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Каркасы коробок и ручки выделенной коробки, комнаты или лестницы в окне сцены. Ручки — крупные квадраты на гранях (<see cref="LiveBuilderFaceHandles"/>):
    /// тянешь — ходит только эта грань, противоположная стоит; Alt — обе сразу, Alt+Shift — все стороны вместе;
    /// у коробки Ctrl — выдавить от грани новую коробку (<see cref="BoxModelPartExtrusion"/>), Shift+клик по квадрату — набрать грань (<see cref="BoxModelFaceSelection"/>):
    /// потянул квадрат набранной — едут все набранные (<see cref="BoxModelFaceMoveHandle"/>).
    /// Грани прилипают к сетке <see cref="LiveBuilderGrid"/>, всё с Undo. Переносят коробку штатным инструментом Move.
    /// У лестницы ещё стрелка подъёма на полу и сколько ступеней.
    /// </summary>
    public static class BoxModelPartHandles
    {
        // Голубой, а не зелёный: зелёным Unity рисует коллайдеры, каркас коробки с ним путается.
        public static readonly Color AddColor = new(0.3f, 0.7f, 1f);
        public static readonly Color CutColor = new(1f, 0.35f, 0.3f);

        private static readonly Color StairsArrowColor = new(1f, 0.85f, 0.2f);


        // ── Каркасы ──────────────────────────────────────────────────────

        // Каркасы всех коробок объекта, с копиями повторов и ступенями: добавляющие — голубые, вырезающие — красные; выделенные — ярче.
        // Что целиком выше разреза над этажом — не рисуется. Все коробки или только выделенные — <see cref="BoxModelWireframesSetting"/>.
        public static void DrawWireframes(BoxModel model)
        {
            using (new Handles.DrawingScope(model.transform.localToWorldMatrix))
                foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                {
                    var min = box.Box.MinInMeters;
                    var max = box.Box.MaxInMeters;

                    if (LiveBuilderStoreys.IsHiddenBySection(model.transform.TransformPoint(min)))
                        continue;

                    var isSelected = IsSelected(box);

                    if (!isSelected && !BoxModelWireframesSetting.IsShowingAll)
                        continue;

                    var color = box.Mode == BoxModelPartMode.Add ? AddColor : CutColor;
                    Handles.color = isSelected ? color : new Color(color.r, color.g, color.b, 0.45f);
                    Handles.DrawWireCube((min + max) / 2f, max - min);
                }
        }

        // Выделена сама коробка или в режиме «Грани» выделена её грань.
        private static bool IsSelected(BoxModelSolverBox box)
        {
            if (box.Owner == null)
                return false;

            if (Selection.Contains(box.Owner.gameObject))
                return true;

            foreach (var partFace in BoxModelFaceSelection.Faces)
                if (partFace.Part == box.OwnerPart)
                    return true;

            return false;
        }


        // ── Ручки ────────────────────────────────────────────────────────

        // Точки на гранях коробки: тянешь — грань ходит по сетке, коробка не тоньше одного шага.
        // С Ctrl — коробка стоит, от грани растёт новая (<see cref="BoxModelPartExtrusion"/>). Shift+клик — грань в набор или из набора;
        // тянешь грань из набора, где их несколько, — едут все набранные.
        public static void DrawHandles(BoxModel model, BoxModelPart part)
        {
            var color = part.Mode == BoxModelPartMode.Add ? AddColor : CutColor;
            BoxModelPartExtrusion.FinishIfReleased();
            BoxModelPartExtrusion.DrawDepthLabel();

            if (!DragFaces(model, part.transform, part.Size, color, null, out var min, out var max, out var draggedMin, out var draggedMax,
                    (axis, sign) => BoxModelFaceSelection.Toggle(new BoxModelPartFace(part, BoxFaces.Index(axis, sign > 0f)))))
                return;

            if (TryFindMovedFace(min, max, draggedMin, draggedMax, out var face, out var faceCoordinate)
                && BoxModelFaceSelection.Faces.Count > 1 && BoxModelFaceSelection.Contains(new BoxModelPartFace(part, face)))
            {
                BoxModelFaceMoveHandle.DragCollectedFaces(new BoxModelPartFace(part, face), faceCoordinate, EditorGUI.actionKey);
                return;
            }

            if (!BoxModelPartExtrusion.TryExtrude(model, part, min, max, draggedMin, draggedMax, EditorGUI.actionKey))
                ResizePiece(model, part, part.transform, min, max, draggedMin, draggedMax, size => part.Size = size);
        }

        // Какую одну грань сдвинули и где она теперь; сдвинуто больше одной (Alt, Alt+Shift) — false.
        private static bool TryFindMovedFace(Vector3 min, Vector3 max, Vector3 draggedMin, Vector3 draggedMax, out int face, out float faceCoordinate)
        {
            face = -1;
            faceCoordinate = 0f;

            for (var axis = 0; axis < 3; axis++)
            {
                var isMinMoved = !Mathf.Approximately(min[axis], draggedMin[axis]);
                var isMaxMoved = !Mathf.Approximately(max[axis], draggedMax[axis]);

                if (!isMinMoved && !isMaxMoved)
                    continue;

                if (face >= 0 || isMinMoved && isMaxMoved)
                    return false;

                face = BoxFaces.Index(axis, isMaxMoved);
                faceCoordinate = isMaxMoved ? draggedMax[axis] : draggedMin[axis];
            }

            return face >= 0;
        }


        // То же для комнаты — по её внутреннему размеру.
        public static void DrawHandles(BoxModel model, BoxModelRoom room)
        {
            DrawBoundsHandle(model, room, room.transform, room.Size, CutColor, size => room.Size = size);
        }


        // Лестница — как кубик в ProBuilder: тянешь грань — длина, ширина или высота, ступени подстраиваются.
        // Число ступеней — от высоты, глубина ступени — под длину марша.
        public static void DrawHandles(BoxModel model, BoxModelStairs stairs)
        {
            var axisNames = stairs.RunAxis == 0 ? new[] { "длина", "высота", "ширина" } : new[] { "ширина", "высота", "длина" };
            DrawStairsDirection(model, stairs);

            DrawBoundsHandle(model, stairs, stairs.transform, stairs.Size, AddColor, size =>
            {
                stairs.Width = stairs.RunAxis == 0 ? size.z : size.x;
                stairs.Height = size.y;
                stairs.StepDepth = stairs.StepDepthForLength(size[stairs.RunAxis]);
            }, axisNames);
        }

        // Стрелка на полу от низа марша к верху и подпись: сколько ступеней, подъём и глубина ступени. У лестницы с площадкой — по первому маршу.
        private static void DrawStairsDirection(BoxModel model, BoxModelStairs stairs)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var min = model.transform.InverseTransformPoint(stairs.transform.position);
            var size = stairs.Size;
            var run = stairs.RunAxis;
            var cross = run == 0 ? 2 : 0;
            var start = min + Vector3.up * 0.03f;
            start[cross] += (stairs.IsFirstFlightOnOtherSide ? size[cross] - stairs.FlightWidth : 0f) + stairs.FlightWidth / 2f;
            start[run] += stairs.RunsTowardsPlus ? size[run] * 0.15f : size[run] * 0.85f;

            var end = start;
            end[run] = min[run] + (stairs.RunsTowardsPlus ? size[run] * 0.85f : size[run] * 0.15f);

            var along = (end - start).normalized;
            var side = Vector3.Cross(Vector3.up, along) * Mathf.Min(stairs.FlightWidth * 0.25f, 0.5f);
            var head = end - along * Mathf.Min(size[run] * 0.2f, 0.6f);

            using (new Handles.DrawingScope(StairsArrowColor, model.transform.localToWorldMatrix))
            {
                Handles.DrawAAPolyLine(4f, start, end);
                Handles.DrawAAPolyLine(4f, head + side, end, head - side);
            }

            var landing = stairs.IsSwitchback ? " · площадка " + LiveBuilderSceneText.FormatMeters(stairs.LandingDepth) + " м на середине" : "";
            LiveBuilderSceneText.DrawLabel(model.transform.TransformPoint(min + size), stairs.StepCount + " ступеней · подъём " +
                                                                                       LiveBuilderSceneText.FormatMeters(stairs.Height / stairs.StepCount) + " · глубина " +
                                                                                       LiveBuilderSceneText.FormatMeters(stairs.StepDepth) + " м" + landing);
        }


        // Ручки граней по углу и размеру; сдвинутые грани — на сетку, с Undo.
        private static void DrawBoundsHandle(BoxModel model, Object piece, Transform pieceTransform, Vector3 size, Color color, System.Action<Vector3> setSize,
            string[] axisNames = null)
        {
            if (DragFaces(model, pieceTransform, size, color, axisNames, out var min, out var max, out var draggedMin, out var draggedMax))
                ResizePiece(model, piece, pieceTransform, min, max, draggedMin, draggedMax, setSize);
        }

        // Ручки граней: тянули ли. Углы до и после сдвига — в осях объекта, без сетки.
        private static bool DragFaces(BoxModel model, Transform pieceTransform, Vector3 size, Color color, string[] axisNames,
            out Vector3 min, out Vector3 max, out Vector3 draggedMin, out Vector3 draggedMax, System.Action<int, float> onShiftClick = null)
        {
            min = model.transform.InverseTransformPoint(pieceTransform.position);
            max = min + size;
            draggedMin = min;
            draggedMax = max;

            return LiveBuilderFaceHandles.Draw(model.transform.localToWorldMatrix, ref draggedMin, ref draggedMax, color, axisNames, 7, onShiftClick);
        }

        // Сдвинутые грани — на сетку, новый угол и размер — в объект, с Undo.
        private static void ResizePiece(BoxModel model, Object piece, Transform pieceTransform, Vector3 min, Vector3 max, Vector3 draggedMin, Vector3 draggedMax,
            System.Action<Vector3> setSize)
        {
            SnapMovedFaces(min, max, ref draggedMin, ref draggedMax);

            Undo.RecordObjects(new[] { piece, pieceTransform }, "Resize Box");
            pieceTransform.position = model.transform.TransformPoint(draggedMin);
            setSize(draggedMax - draggedMin);
        }

        // Сдвинутые грани — на сетку; несдвинутые остаются ровно где были, без дрожания от округлений.
        private static void SnapMovedFaces(Vector3 oldMin, Vector3 oldMax, ref Vector3 newMin, ref Vector3 newMax)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var step = LiveBuilderGrid.StepOn(axis);
                var isMinMoved = !Mathf.Approximately(newMin[axis], oldMin[axis]);
                var isMaxMoved = !Mathf.Approximately(newMax[axis], oldMax[axis]);

                newMin[axis] = isMinMoved ? LiveBuilderGrid.Snap(newMin[axis], axis) : oldMin[axis];
                newMax[axis] = isMaxMoved ? LiveBuilderGrid.Snap(newMax[axis], axis) : oldMax[axis];

                if (newMax[axis] - newMin[axis] >= step)
                    continue;

                if (isMaxMoved)
                    newMax[axis] = newMin[axis] + step;
                else
                    newMin[axis] = newMax[axis] - step;
            }
        }
    }
}
