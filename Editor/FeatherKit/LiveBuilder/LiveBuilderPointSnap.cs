using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Привязка к точкам построек, как в SketchUp: точка рисования прилипает к углу или середине ребра коробки, к точке или середине отрезка
    /// стены и здания, если до неё на экране меньше <see cref="SnapPixels"/>; нет такой — к любой точке ребра или отрезка («на ребре»); дальше — сетка. Точка другой высоты — по проекции на плоскость рисования:
    /// коробка на полу встаёт вровень с углом здания над ней. Что прилипло — подпись у прицела («угол», «середина», «точка»).
    /// Работает везде, где точка ложится на плоскость рисования (<see cref="BoxModelDrawPlane.SnapOnPlane"/>): коробка, комната, лестница, стена,
    /// здание, линейка. Точки берутся из данных построек (не из меша) и помнятся до следующей правки сцены; на горизонтальной плоскости
    /// перебираются только соседние по плану (<see cref="LiveBuilderPlanCells"/>). Спрятанное разрезом этажа не ловит. Клавиша S или кнопка в окне —
    /// выключить и включить (<see cref="IsEnabled"/>). Без окна сцены (проверки) не прилипает.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderPointSnap
    {
        // Ближе этого к точке на экране — прилипает, пикс.
        public const float SnapPixels = 10f;

        // Совпадение точки с последней прилипшей — для подписи, м.
        private const float SamePointTolerance = 0.0005f;

        private const string EnabledPreferenceKey = "FeatherKit.LiveBuilder.PointSnapEnabled";

        private static readonly List<(Vector3 Point, string Label)> Points = new();
        private static readonly HashSet<Vector3Int> SeenPoints = new();

        // Рёбра коробок и отрезки линий — к любой их точке прилипает, если рядом нет угла или середины.
        private static readonly List<(Vector3 Start, Vector3 End)> Edges = new();
        private static readonly HashSet<(Vector3Int, Vector3Int)> SeenEdges = new();

        // Те же точки и рёбра по клеткам плана и списки для поиска в них — чтобы не заводить новые на каждое движение мыши.
        private static readonly LiveBuilderPlanCells PlanCells = new();
        private static readonly List<int> NearbyPoints = new();
        private static readonly HashSet<int> NearbyEdges = new();

        private static bool isStale = true;

        private static Vector3 lastSnappedWorld;
        private static string lastLabel;


        // Сцену правили — точки собираются заново при следующем поиске.
        static LiveBuilderPointSnap()
        {
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => isStale = true;
            Undo.undoRedoPerformed += () => isStale = true;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => isStale = true;
        }


        // Прилипание к точкам включено — своё у каждого человека, в настройках редактора.
        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPreferenceKey, true);
            set
            {
                EditorPrefs.SetBool(EnabledPreferenceKey, value);
                lastLabel = null;
            }
        }


        // ── Привязка ─────────────────────────────────────────────────────

        // Точка плоскости рисования (в осях рамки) — к ближайшей точке построек на экране, по проекции на плоскость. Нет рядом или выключено — false.
        public static bool TrySnap(Matrix4x4 frameToWorld, int axis, float coordinate, Vector3 localPoint, out Vector3 snappedLocal)
        {
            snappedLocal = default;

            if (!IsEnabled || Camera.current == null || Event.current == null)
                return false;

            Refresh();

            var worldToFrame = frameToWorld.inverse;
            var world = frameToWorld.MultiplyPoint3x4(localPoint);
            var mouse = HandleUtility.WorldToGUIPoint(world);
            var reach = HandleUtility.GetHandleSize(world) * 0.5f;
            var bestPixels = SnapPixels;
            string bestLabel = null;

            // Плоскость горизонтальна — точки сносит на неё по Y: соседи по плану те же, перебираем только их.
            var isLevelPlane = Mathf.Abs(((Vector3)frameToWorld.GetColumn(axis)).normalized.y) > 0.999f;
            var isNearbyOnly = isLevelPlane && PlanCells.TryCollectPoints(world, reach, NearbyPoints);
            var count = isNearbyOnly ? NearbyPoints.Count : Points.Count;

            for (var index = 0; index < count; index++)
            {
                var (point, label) = Points[isNearbyOnly ? NearbyPoints[index] : index];

                // Над разрезом этажа — не видно, не ловим.
                if (LiveBuilderStoreys.IsHiddenBySection(point))
                    continue;

                var local = worldToFrame.MultiplyPoint3x4(point);
                local[axis] = coordinate;
                var projected = frameToWorld.MultiplyPoint3x4(local);

                if ((projected - world).sqrMagnitude > reach * reach)
                    continue;

                var pixels = (HandleUtility.WorldToGUIPoint(projected) - mouse).magnitude;

                if (pixels >= bestPixels)
                    continue;

                bestPixels = pixels;
                bestLabel = label;
                snappedLocal = local;
            }

            if (bestLabel == null && TrySnapToEdge(frameToWorld, worldToFrame, axis, coordinate, world, mouse, reach, isLevelPlane, out snappedLocal))
                bestLabel = "на ребре";

            if (bestLabel == null)
                return false;

            lastSnappedWorld = frameToWorld.MultiplyPoint3x4(snappedLocal);
            lastLabel = bestLabel;
            return true;
        }

        // Ближайшая точка рёбер и отрезков, спроецированных на плоскость рисования, — если до неё на экране меньше SnapPixels.
        private static bool TrySnapToEdge(Matrix4x4 frameToWorld, Matrix4x4 worldToFrame, int axis, float coordinate, Vector3 world, Vector2 mouse, float reach,
            bool isLevelPlane, out Vector3 snappedLocal)
        {
            var found = Vector3.zero;
            var bestPixels = SnapPixels;
            var isFound = false;
            var localWorldPoint = worldToFrame.MultiplyPoint3x4(world);

            if (isLevelPlane && PlanCells.TryCollectEdges(world, reach, NearbyEdges))
            {
                foreach (var index in NearbyEdges)
                    ConsiderEdge(index);
            }
            else
            {
                for (var index = 0; index < Edges.Count; index++)
                    ConsiderEdge(index);
            }

            snappedLocal = found;
            return isFound;

            void ConsiderEdge(int index)
            {
                var (start, end) = Edges[index];

                if (LiveBuilderStoreys.IsHiddenBySection(start) && LiveBuilderStoreys.IsHiddenBySection(end))
                    return;

                var localStart = worldToFrame.MultiplyPoint3x4(start);
                var localEnd = worldToFrame.MultiplyPoint3x4(end);
                localStart[axis] = coordinate;
                localEnd[axis] = coordinate;

                var along = localEnd - localStart;

                // Ребро поперёк плоскости — на ней одна точка: это угол, его ловят выше.
                if (along.sqrMagnitude < 1e-6f)
                    return;

                var fraction = Mathf.Clamp01(Vector3.Dot(localWorldPoint - localStart, along) / along.sqrMagnitude);
                var local = localStart + along * fraction;
                var projected = frameToWorld.MultiplyPoint3x4(local);

                if ((projected - world).sqrMagnitude > reach * reach)
                    return;

                var pixels = (HandleUtility.WorldToGUIPoint(projected) - mouse).magnitude;

                if (pixels >= bestPixels)
                    return;

                bestPixels = pixels;
                found = local;
                isFound = true;
            }
        }


        // Подпись, если эта точка мира — последняя прилипшая; иначе null.
        public static string LabelAt(Vector3 worldPoint)
        {
            return lastLabel != null && (worldPoint - lastSnappedWorld).sqrMagnitude < SamePointTolerance * SamePointTolerance ? lastLabel : null;
        }


        // ── Точки построек ───────────────────────────────────────────────

        private static void Refresh()
        {
            if (!isStale)
                return;

            isStale = false;
            Points.Clear();
            SeenPoints.Clear();
            Edges.Clear();
            SeenEdges.Clear();
            PlanCells.Clear();
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var model in stage.FindComponentsOfType<BoxModel>())
                if (model.gameObject.activeInHierarchy)
                    AddBoxCorners(model);

            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                if (wall.gameObject.activeInHierarchy)
                    AddLinePoints(wall);

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
                if (building.gameObject.activeInHierarchy)
                    AddLinePoints(building);
        }

        // Углы, середины и рёбра наружных поверхностей объекта из коробок.
        private static void AddBoxCorners(BoxModel model)
        {
            var modelTransform = model.transform;

            foreach (var surface in BoxModelRebuilder.GetSurfaces(model))
            {
                var axis = BoxFaces.Axis(surface.Face);
                var axisU = BoxFaces.SurfaceAxisU(surface.Face);
                var axisV = BoxFaces.SurfaceAxisV(surface.Face);
                var area = surface.Area;
                float minU = area.xMin, maxU = area.xMax, minV = area.yMin, maxV = area.yMax;
                var middleU = (minU + maxU) / 2f;
                var middleV = (minV + maxV) / 2f;

                Vector3 PointAt(float u, float v)
                {
                    var local = Vector3.zero;
                    local[axis] = MillimeterBox.ToMeters(surface.Plane);
                    local[axisU] = u / MillimeterBox.MillimetersPerMeter;
                    local[axisV] = v / MillimeterBox.MillimetersPerMeter;
                    return modelTransform.TransformPoint(local);
                }

                var corners = new[] { PointAt(minU, minV), PointAt(maxU, minV), PointAt(maxU, maxV), PointAt(minU, maxV) };

                for (var index = 0; index < corners.Length; index++)
                {
                    Add(corners[index], "угол");
                    AddEdge(corners[index], corners[(index + 1) % corners.Length]);
                }

                Add(PointAt(middleU, minV), "середина");
                Add(PointAt(middleU, maxV), "середина");
                Add(PointAt(minU, middleV), "середина");
                Add(PointAt(maxU, middleV), "середина");
            }
        }

        // Точки линии стены или контура здания и середины их отрезков.
        private static void AddLinePoints(Component owner)
        {
            if (!owner.TryGetComponent<SplineContainer>(out var container))
                return;

            foreach (var spline in container.Splines)
            {
                var count = spline.Count;

                for (var index = 0; index < count; index++)
                {
                    var point = owner.transform.TransformPoint(spline[index].Position);
                    Add(point, "точка");

                    if (index + 1 >= count && !spline.Closed)
                        continue;

                    var next = owner.transform.TransformPoint(spline[(index + 1) % count].Position);
                    Add((point + next) / 2f, "середина");
                    AddEdge(point, next);
                }
            }
        }

        // Одно ребро один раз, в какую сторону его ни запиши.
        private static void AddEdge(Vector3 start, Vector3 end)
        {
            var startKey = Vector3Int.RoundToInt(start * MillimeterBox.MillimetersPerMeter);
            var endKey = Vector3Int.RoundToInt(end * MillimeterBox.MillimetersPerMeter);
            var key = startKey.x < endKey.x || (startKey.x == endKey.x && (startKey.y < endKey.y || (startKey.y == endKey.y && startKey.z <= endKey.z)))
                ? (startKey, endKey)
                : (endKey, startKey);

            if (!SeenEdges.Add(key))
                return;

            PlanCells.AddEdge(Edges.Count, start, end);
            Edges.Add((start, end));
        }

        // Одна точка один раз: у соседних коробок углы общие.
        private static void Add(Vector3 point, string label)
        {
            if (!SeenPoints.Add(Vector3Int.RoundToInt(point * MillimeterBox.MillimetersPerMeter)))
                return;

            PlanCells.AddPoint(Points.Count, point);
            Points.Add((point, label));
        }
    }
}
