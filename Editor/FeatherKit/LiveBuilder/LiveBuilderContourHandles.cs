using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Правка контура уже построенного: точки выделенного здания, стены по линии или комнаты по контуру — прямо в окне сцены, как в tsitski.
    /// Здесь — ручки на контуре и порядок, кто первым забирает событие:
    /// - клик по точке — выбрать её; Shift+клик — добавить или убрать; двойной клик — вид по кругу: угол → плавно → Безье
    /// - рамка по пустому месту — выбрать точки в ней (с Shift — добавить); у выбранных — стрелки переноса по осям (<see cref="LiveBuilderContourMoveHandle"/>)
    /// - «+» посередине отрезка — новая точка, её сразу можно тянуть; «+» за концом открытой линии — продлить её
    /// - у выбранной точки Безье — два уса; у выбранного угла — голубая ручка скругления (<see cref="LiveBuilderCornerRadiusHandle"/>)
    /// Перетаскивание — <see cref="LiveBuilderContourDrag"/>, рамка выбора — <see cref="LiveBuilderContourBoxSelect"/>,
    /// клавиши — <see cref="LiveBuilderContourKeys"/>, вид — <see cref="LiveBuilderContourDrawing"/>.
    /// Что выбрано — общее с окном Live Builder (<see cref="LiveBuilderContourSelection"/>). Каждое действие — один шаг Ctrl+Z;
    /// стены, перекрытия и вырезы перестраиваются сами. Весь контур виден линией — и стороны, что не строятся.
    /// </summary>
    public sealed class LiveBuilderContourHandles
    {
        // На сколько за концом открытой линии стоит «+» продления, м.
        private const float ExtendDistance = 1f;

        private static readonly int DragControlHint = "LiveBuilderContourDrag".GetHashCode();
        private static readonly int BoxSelectControlHint = "LiveBuilderContourBoxSelect".GetHashCode();
        private static readonly int KnotControlHint = "LiveBuilderContourKnot".GetHashCode();
        private static readonly int InsertControlHint = "LiveBuilderContourInsert".GetHashCode();
        private static readonly int TangentControlHint = "LiveBuilderContourTangent".GetHashCode();

        private readonly LiveBuilderContourDrag drag = new();
        private readonly LiveBuilderContourBoxSelect boxSelect = new();
        private readonly LiveBuilderContourMoveHandle moveHandle = new();

        // Свои ручки этого кадра — чтобы отличить нажатие по пустому месту от нажатия по точке.
        private readonly HashSet<int> ownControls = new();


        // Показан ли контур: выделено одно здание, стена или комната по контуру.
        public static bool IsShown => LiveBuilderContourSelection.Owner != null;

        public static string Hint => LiveBuilderContourSelection.Knots.Count > 0
            ? "Выбрано точек: " + LiveBuilderContourSelection.Knots.Count + ". Тяни точку или стрелки в середине — двигать все" +
              (LiveBuilderContourSelection.Owner is SplineWall ? " (вверх тоже)" : "") + ", клавиши-стрелки — на шаг сетки, Delete — убрать, " +
              "двойной клик — угол, плавно или Безье, у угла — голубая ручка: скругление, Esc — снять выбор"
            : "Точки контура: тяни — двигать, «+» — новая точка, рамка по пустому месту или Shift+клик — выбрать несколько, двойной клик — угол, плавно или Безье" +
              (LiveBuilderContourSelection.Owner is SplineWall ? ", C — замкнуть или разомкнуть" : "");

        // Тянут точку, ус, рамку или стрелки выбранных — мышь занята правкой контура.
        public bool IsGestureActive => drag.IsDragging || boxSelect.IsSelecting || moveHandle.IsDragging;


        // ── Окно сцены ───────────────────────────────────────────────────

        // Звать до общего элемента инструмента: точки и «+» ближе к мыши забирают нажатие себе.
        // canEdit — правит ли этот контур выбранный инструмент; не правит — видна только линия контура, без ручек.
        // allowsBoxSelect — рамка по пустому месту; в инструментах линии выключена: там нажатие по пустому начинает новую линию.
        public void OnSceneGUI(System.Predicate<Component> canEdit, bool allowsBoxSelect)
        {
            var owner = SelectedOwner();

            if (owner != null && !canEdit(owner))
            {
                if (Event.current.type == EventType.Repaint)
                    LiveBuilderContourDrawing.DrawContourLines(owner);

                owner = null;
            }

            LiveBuilderContourSelection.SetOwner(owner);

            var dragControl = GUIUtility.GetControlID(DragControlHint, FocusType.Passive);
            var boxSelectControl = GUIUtility.GetControlID(BoxSelectControlHint, FocusType.Passive);

            if (owner == null)
            {
                Cancel();
                return;
            }

            var container = owner.GetComponent<SplineContainer>();

            if (LiveBuilderContourSelection.Prune(container) && drag.IsDragging)
                drag.End();

            if (drag.Handle(owner, container, dragControl) || boxSelect.Handle(owner, container, boxSelectControl) || LiveBuilderContourKeys.Handle(owner, container, IsGestureActive))
                return;

            if (Event.current.type == EventType.Layout)
                ownControls.Clear();

            LiveBuilderContourDrawing.DrawContourLines(owner);

            // Стрелки выбранных — раньше точек и усов: что лежит под ними, забирает нажатие себе.
            if (moveHandle.Draw(owner, container) || DoTangents(owner, container, dragControl) || DoCornerRadii(owner, container))
                return;

            for (var splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                var spline = container.Splines[splineIndex];

                for (var knot = 0; knot < spline.Count; knot++)
                    if (DoKnot(owner, container, splineIndex, knot, dragControl))
                        return;

                var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;

                for (var segment = 0; segment < segmentCount; segment++)
                    if (DoInsert(owner, container, splineIndex, segment, dragControl))
                        return;

                if (!spline.Closed && spline.Count >= 2 && (DoExtend(owner, container, splineIndex, true, dragControl) || DoExtend(owner, container, splineIndex, false, dragControl)))
                    return;
            }

            if (allowsBoxSelect)
                boxSelect.TryBegin(ownControls);
        }


        // Бросить перетаскивание, рамку и стрелки: мышь освобождается, уже сдвинутые точки остаются. Зовёт и Esc инструмента.
        public void Cancel()
        {
            drag.End();
            boxSelect.Cancel();
            moveHandle.EndDrag();
        }

        // Одно здание, стена по линии или комната по контуру в выделении; иначе — null.
        public static Component SelectedOwner()
        {
            var selected = Selection.activeGameObject;

            if (selected == null || Selection.gameObjects.Length != 1 || !selected.activeInHierarchy || !selected.TryGetComponent<SplineContainer>(out _))
                return null;

            if (selected.TryGetComponent<SplineWall>(out var wall))
                return wall;

            return selected.TryGetComponent<SplineBuilding>(out var building) ? building : null;
        }


        // ── Точки ────────────────────────────────────────────────────────

        // Точка: клик — выбрать и тянуть, Shift+клик — добавить к выбору или убрать, двойной клик — следующий вид точки.
        // Возвращает, поменялось ли что-то.
        private bool DoKnot(Component owner, SplineContainer container, int splineIndex, int knotIndex, int dragControl)
        {
            var spline = container.Splines[splineIndex];
            var control = GUIUtility.GetControlID(KnotControlHint, FocusType.Passive);
            var position = owner.transform.TransformPoint(spline[knotIndex].Position);
            var size = HandleUtility.GetHandleSize(position) * LiveBuilderContourDrawing.KnotSize;
            var current = Event.current;

            switch (current.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddControl(control, HandleUtility.DistanceToCircle(position, size));
                    ownControls.Add(control);
                    break;

                case EventType.MouseDown when current.button == 0 && !current.alt && HandleUtility.nearestControl == control:
                    current.Use();

                    if (current.clickCount == 2)
                    {
                        Undo.RecordObject(container, "Change Contour Point");
                        LiveBuilderContourSelection.SetShape(spline, knotIndex, LiveBuilderContourSelection.Next(LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knotIndex))));
                        LiveBuilderContourSelection.SelectOnly(splineIndex, knotIndex);
                        return true;
                    }

                    if (current.shift)
                    {
                        LiveBuilderContourSelection.Toggle(splineIndex, knotIndex);
                        return true;
                    }

                    if (!LiveBuilderContourSelection.Contains(splineIndex, knotIndex))
                        LiveBuilderContourSelection.SelectOnly(splineIndex, knotIndex);

                    drag.Begin(dragControl, (splineIndex, knotIndex), null);
                    return true;

                case EventType.Repaint:
                    var isSelected = LiveBuilderContourSelection.Contains(splineIndex, knotIndex);
                    var color = isSelected ? Color.white : HandleUtility.nearestControl == control ? LiveBuilderContourDrawing.HoverColor : LiveBuilderContourDrawing.KnotColor;
                    LiveBuilderContourDrawing.DrawKnot(control, position, size, color, LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knotIndex)));
                    break;
            }

            return false;
        }


        // ── Новые точки ──────────────────────────────────────────────────

        // «+» посередине отрезка: нажал — там новая точка, и её сразу тянешь.
        private bool DoInsert(Component owner, SplineContainer container, int splineIndex, int segment, int dragControl)
        {
            var spline = container.Splines[splineIndex];
            var middle = (Vector3)CurveUtility.EvaluatePosition(spline.GetCurve(segment), 0.5f);
            var isSmooth = spline.GetTangentMode(segment) != TangentMode.Linear || spline.GetTangentMode((segment + 1) % spline.Count) != TangentMode.Linear;
            return DoAddHandle(owner, container, splineIndex, segment + 1, middle, isSmooth, dragControl);
        }


        // «+» за концом открытой линии: нажал — линия продлилась новой точкой, тянешь её.
        private bool DoExtend(Component owner, SplineContainer container, int splineIndex, bool isAtStart, int dragControl)
        {
            var spline = container.Splines[splineIndex];
            var end = isAtStart ? 0 : spline.Count - 1;
            var neighbour = isAtStart ? 1 : spline.Count - 2;
            var endPosition = (Vector3)spline[end].Position;
            var direction = endPosition - (Vector3)spline[neighbour].Position;

            if (direction.sqrMagnitude < 0.0001f)
                return false;

            var position = endPosition + direction.normalized * ExtendDistance;
            return DoAddHandle(owner, container, splineIndex, isAtStart ? 0 : spline.Count, position, spline.GetTangentMode(end) != TangentMode.Linear, dragControl);
        }


        // Ручка «+»: где новая точка встанет и под каким номером. Возвращает, поменялся ли контур. Общая у «+» на отрезке и за концом.
        private bool DoAddHandle(Component owner, SplineContainer container, int splineIndex, int newIndex, Vector3 localPosition, bool isSmooth, int dragControl)
        {
            var control = GUIUtility.GetControlID(InsertControlHint, FocusType.Passive);
            var position = owner.transform.TransformPoint(localPosition);
            var size = HandleUtility.GetHandleSize(position) * LiveBuilderContourDrawing.InsertSize;
            var current = Event.current;

            switch (current.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddControl(control, HandleUtility.DistanceToCircle(position, size));
                    ownControls.Add(control);
                    break;

                case EventType.MouseDown when current.button == 0 && !current.alt && HandleUtility.nearestControl == control:
                    Undo.RecordObject(container, "Add Contour Point");
                    container.Splines[splineIndex].Insert(newIndex, new BezierKnot(localPosition), isSmooth ? TangentMode.AutoSmooth : TangentMode.Linear);
                    LiveBuilderContourSelection.SelectOnly(splineIndex, newIndex);
                    current.Use();
                    drag.Begin(dragControl, (splineIndex, newIndex), null);
                    return true;

                case EventType.Repaint:
                    var color = HandleUtility.nearestControl == control ? LiveBuilderContourDrawing.HoverColor : LiveBuilderContourDrawing.InsertColor;
                    LiveBuilderContourDrawing.DrawInsert(position, size, color);
                    break;
            }

            return false;
        }


        // ── Скругление углов ─────────────────────────────────────────────

        // У выбранных угловых точек — голубая ручка радиуса (<see cref="LiveBuilderCornerRadiusHandle"/>). Возвращает, поменялся ли радиус.
        private bool DoCornerRadii(Component owner, SplineContainer container)
        {
            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                var spline = container.Splines[splineIndex];

                if (!LiveBuilderCornerRadiusHandle.CanRound(spline, knotIndex))
                    continue;

                var isChanged = LiveBuilderCornerRadiusHandle.Draw(owner, container, spline, knotIndex, out var control);

                if (Event.current.type == EventType.Layout)
                    ownControls.Add(control);

                if (isChanged)
                    return true;
            }

            return false;
        }


        // ── Усы Безье ────────────────────────────────────────────────────

        // Усы выбранных точек Безье: нажал на конец уса — тянешь его. Возвращает, начали ли тянуть.
        private bool DoTangents(Component owner, SplineContainer container, int dragControl)
        {
            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                var spline = container.Splines[splineIndex];

                if (LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knotIndex)) != LiveBuilderKnotShape.Bezier)
                    continue;

                foreach (var tangent in new[] { BezierTangent.In, BezierTangent.Out })
                    if (DoTangent(owner, spline, splineIndex, knotIndex, tangent, dragControl))
                        return true;
            }

            return false;
        }

        private bool DoTangent(Component owner, Spline spline, int splineIndex, int knotIndex, BezierTangent tangent, int dragControl)
        {
            var control = GUIUtility.GetControlID(TangentControlHint, FocusType.Passive);
            var knotPosition = owner.transform.TransformPoint(spline[knotIndex].Position);
            var tip = LiveBuilderContourDrawing.TangentTipOf(owner, spline[knotIndex], tangent);
            var size = HandleUtility.GetHandleSize(tip) * LiveBuilderContourDrawing.TangentSize;
            var current = Event.current;

            switch (current.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddControl(control, HandleUtility.DistanceToCircle(tip, size));
                    ownControls.Add(control);
                    break;

                case EventType.MouseDown when current.button == 0 && !current.alt && HandleUtility.nearestControl == control:
                    current.Use();
                    drag.Begin(dragControl, (splineIndex, knotIndex), tangent);
                    return true;

                case EventType.Repaint:
                    Handles.color = HandleUtility.nearestControl == control ? LiveBuilderContourDrawing.HoverColor : LiveBuilderContourDrawing.TangentColor;
                    Handles.DrawLine(knotPosition, tip, 2f);
                    Handles.SphereHandleCap(control, tip, Quaternion.identity, size * 1.4f, EventType.Repaint);
                    break;
            }

            return false;
        }
    }
}
