using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Перетаскивание в правке контура (<see cref="LiveBuilderContourHandles"/>): тянут точку — едут все выбранные на тот же сдвиг, по сетке
    /// (у перегородки в здании — ещё и к его стенам); тянут ус Безье — второй ус той же точки зеркально, без сетки.
    /// Здесь же — сетка контура и сдвиг выбранных на шаг (стрелками, <see cref="LiveBuilderContourKeys"/>).
    /// </summary>
    public sealed class LiveBuilderContourDrag
    {
        // Точка, за которую тянут: по ней — сетка и стены, остальные выбранные едут на тот же сдвиг. Или её ус.
        private (int Spline, int Knot) grabbedKnot;
        private BezierTangent? grabbedTangent;

        // Элемент, который держит мышь, пока тянут.
        private int heldDragControl;


        public bool IsDragging { get; private set; }


        // ── Жест ─────────────────────────────────────────────────────────

        public void Begin(int dragControl, (int Spline, int Knot) grabbed, BezierTangent? tangent)
        {
            grabbedKnot = grabbed;
            grabbedTangent = tangent;
            heldDragControl = dragControl;
            GUIUtility.hotControl = dragControl;
            IsDragging = true;
        }


        // Тянут точку или ус: мышь по полу — за ней, отпустил — готово. Возвращает, забрал ли событие.
        public bool Handle(Component owner, SplineContainer container, int dragControl)
        {
            var current = Event.current;

            if (!IsDragging)
                return false;

            // Мышь отобрали (окно потеряло фокус) или Ctrl+Z убрал точку, за которую тянули, — тянуть нечего.
            if (GUIUtility.hotControl != dragControl || !HasGrabbedKnot(container))
            {
                End();
                return false;
            }

            switch (current.GetTypeForControl(dragControl))
            {
                case EventType.MouseDrag:
                    if (grabbedTangent.HasValue)
                        MoveTangent(owner, container, current.mousePosition);
                    else
                        MoveSelected(owner, container, current.mousePosition);

                    current.Use();
                    return true;

                case EventType.MouseUp:
                    End();
                    current.Use();
                    return true;

                case EventType.Repaint:
                    LiveBuilderContourDrawing.DrawContourLines(owner);
                    LiveBuilderContourDrawing.DrawSideLengths(owner, container, grabbedKnot);
                    LiveBuilderContourDrawing.DrawSelectedKnots(owner, container);
                    LiveBuilderContourDrawing.DrawTangentLines(owner, container);
                    return true;
            }

            return false;
        }

        private bool HasGrabbedKnot(SplineContainer container)
        {
            return grabbedKnot.Spline < container.Splines.Count && grabbedKnot.Knot < container.Splines[grabbedKnot.Spline].Count;
        }

        // Точка, за которую тянут, — туда, где мышь на полу, по сетке; остальные выбранные — на тот же сдвиг.
        private void MoveSelected(Component owner, SplineContainer container, Vector2 mousePosition)
        {
            var grabbed = container.Splines[grabbedKnot.Spline][grabbedKnot.Knot];

            if (!TryGetFloorPoint(owner, grabbed.Position, mousePosition, out var worldPoint))
                return;

            var target = owner.transform.InverseTransformPoint(SnapToGrid(owner, worldPoint));
            var shift = target - (Vector3)grabbed.Position;
            shift.y = 0f;

            if (shift.sqrMagnitude < 0.0000003f)
                return;

            ShiftSelected(container, shift, "Move Contour Points");
        }

        // Ус Безье — туда, где мышь на полу; второй ус той же точки — зеркально. Без сетки: кривую гнут на глаз.
        private void MoveTangent(Component owner, SplineContainer container, Vector2 mousePosition)
        {
            var spline = container.Splines[grabbedKnot.Spline];
            var knot = spline[grabbedKnot.Knot];

            if (!TryGetFloorPoint(owner, knot.Position, mousePosition, out var worldPoint))
                return;

            var localTangent = owner.transform.InverseTransformPoint(worldPoint) - (Vector3)knot.Position;
            localTangent.y = 0f;
            var knotTangent = math.rotate(math.inverse(knot.Rotation), (float3)localTangent);

            Undo.RecordObject(container, "Bend Contour");

            if (grabbedTangent == BezierTangent.Out)
                knot.TangentOut = knotTangent;
            else
                knot.TangentIn = knotTangent;

            spline.SetKnot(grabbedKnot.Knot, knot, grabbedTangent.Value);
        }

        // Где мышь на горизонтальном полу на высоте точки — в мире.
        private static bool TryGetFloorPoint(Component owner, float3 localKnot, Vector2 mousePosition, out Vector3 worldPoint)
        {
            var floor = new Plane(owner.transform.up, owner.transform.TransformPoint(localKnot));
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            worldPoint = default;

            if (!floor.Raycast(ray, out var enter))
                return false;

            worldPoint = ray.GetPoint(enter);
            return true;
        }


        // Отпустил или отменили: мышь освобождается, если её держит перетаскивание.
        public void End()
        {
            if (IsDragging && GUIUtility.hotControl == heldDragControl)
                GUIUtility.hotControl = 0;

            IsDragging = false;
            grabbedTangent = null;
        }


        // ── Сдвиг и сетка ────────────────────────────────────────────────

        // Сдвинуть все выбранные точки на одно и то же расстояние в осях владельца.
        public static void ShiftSelected(SplineContainer container, Vector3 localShift, string undoName)
        {
            Undo.RecordObject(container, undoName);

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                if (!LiveBuilderContourSelection.IsKnotInside(container, splineIndex, knotIndex))
                    continue;

                var spline = container.Splines[splineIndex];
                var knot = spline[knotIndex];
                knot.Position += (float3)localShift;
                spline.SetKnot(knotIndex, knot);
            }
        }


        // Оси, в которых сетка: здание у перегородки в нём, объект из коробок вокруг, иначе мир (null).
        public static Transform SnapFrameOf(Component owner)
        {
            var building = BuildingOfPartition(owner);

            if (building != null)
                return building.transform;

            var model = SplineWallRebuilder.OuterModelOf(owner);
            return model != null ? model.transform : null;
        }


        // Сетка — в осях здания (у перегородки в нём), объекта из коробок вокруг или мира.
        private static Vector3 SnapToGrid(Component owner, Vector3 worldPoint)
        {
            var building = BuildingOfPartition(owner);
            var frame = SnapFrameOf(owner);
            var framePoint = frame != null ? frame.InverseTransformPoint(worldPoint) : worldPoint;

            framePoint = BoxModelGrid.Snap(framePoint);

            if (building != null)
                framePoint = LiveBuilderBuildingContext.SnapToWalls(building, framePoint);

            return frame != null ? frame.TransformPoint(framePoint) : framePoint;
        }

        // Здание, к которому привязана стена по линии, — её точки прилипают к его стенам.
        private static SplineBuilding BuildingOfPartition(Component owner)
        {
            return owner is SplineWall && owner.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid ? link.Building : null;
        }
    }
}
