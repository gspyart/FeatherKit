using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Где строишь, если внутри здания по контуру (<see cref="SplineBuilding"/>): здание под мышью и этаж — тот, что выбран в окне
    /// (этажи уровня и здания — одни и те же: здание стоит низом на этаже уровня, его этажи идут с тем же шагом). Отсюда пол этажа,
    /// сколько до потолка и сколько этажей выше. Стена, комната, лестница и проём сами берут высоты здания,
    /// а «только этот этаж / этот и все выше» решает, встанет ли новое на одном этаже или на всех выше разом.
    /// </summary>
    public sealed class LiveBuilderBuildingContext
    {
        private const string AllStoreysSessionKey = "FeatherKit.LiveBuilder.IsOnAllStoreys";

        // Насколько точка может не дойти до стены здания и всё равно к ней прилипнуть — доля шага сетки.
        private const float ContourSnapFraction = 0.6f;


        private LiveBuilderBuildingContext(SplineBuilding building, int storey)
        {
            Building = building;
            Storey = storey;
        }


        // «Этот и все этажи выше» вместо «только этот». Только до закрытия редактора: забытая включённой, она тихо копировала бы всё на все этажи.
        public static bool IsOnAllStoreys
        {
            get => SessionState.GetBool(AllStoreysSessionKey, false);
            set
            {
                if (value == IsOnAllStoreys)
                    return;

                SessionState.SetBool(AllStoreysSessionKey, value);
                SceneView.RepaintAll();
            }
        }

        public SplineBuilding Building { get; }

        // Здание ещё на сцене. Его могли удалить или отменить Ctrl+Z, пока найденное лежит в памяти до следующего движения мыши.
        public bool IsBuildingAlive => Building != null;

        // Этаж с нуля.
        public int Storey { get; }

        // Объект из коробок, в котором стоит здание: в него идут перегородки и лестницы — его вырезы режут и здание.
        public BoxModel Model => SplineWallRebuilder.OuterModelOf(Building);

        // От пола до потолка этажа, м.
        public float ClearHeight => Building.StoreyHeight - Building.SlabThickness;

        // Сколько этажей с этого и выше, считая этот.
        public int StoreysAbove => Building.StoreyCount - Storey;

        // Сколько копий вверх у нового: этот этаж — одна, «все выше» — до верхнего этажа.
        public int CopyCount => IsOnAllStoreys ? StoreysAbove : 1;

        // Высота пола этого этажа над контуром здания, в его осях, м.
        public float FloorTopLocal => BaseHeightOf(Building) + Building.FloorTopHeight + Storey * Building.StoreyHeight;

        // Пол этого этажа как плоскость рисования — в осях здания.
        public BoxModelDrawPlane FloorPlane => new(Model, Building.transform.localToWorldMatrix, 1, FloorTopLocal, 1f);


        // ── Поиск ────────────────────────────────────────────────────────

        // Здание под мышью — луч попал в его часть или мышь над его полом на выбранном этаже. Этаж — выбранный в окне;
        // такого этажа у здания нет (ниже или выше него) — не в здании.
        public static bool TryFind(Ray worldRay, out LiveBuilderBuildingContext context)
        {
            context = null;
            var building = BuildingUnder(worldRay);

            if (building == null)
                return false;

            var storey = LiveBuilderStoreys.CurrentStorey - BaseStoreyOf(building);

            if (storey < 0 || storey >= building.StoreyCount)
                return false;

            context = new LiveBuilderBuildingContext(building, storey);
            return true;
        }

        private static SplineBuilding BuildingUnder(Ray worldRay)
        {
            if (BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var hit))
            {
                var hitBuilding = hit.collider.GetComponentInParent<SplineBuilding>();

                if (hitBuilding != null)
                    return hitBuilding;
            }

            var floor = BoxModelDrawPlaneFinder.StoreyFloorPlane(null);

            if (!floor.TryIntersect(worldRay, out var floorPoint))
                return null;

            foreach (var building in UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
            {
                if (!building.gameObject.activeInHierarchy)
                    continue;

                var local = building.transform.InverseTransformPoint(floorPoint);

                foreach (var path in SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep))
                    if (PlanPolygons.IsInside(new Vector2(local.x, local.z), SplineBuildingMeshBuilder.InnerContourOf(building, path)))
                        return building;
            }

            return null;
        }


        // Здание этого объекта, внутри которого точка мира: в плане — внутри его стен, по высоте — от пола до крыши. Нет такого — null.
        public static SplineBuilding BuildingContaining(BoxModel model, Vector3 worldPoint)
        {
            foreach (var building in model.GetComponentsInChildren<SplineBuilding>(false))
            {
                if (SplineWallRebuilder.OuterModelOf(building) != model)
                    continue;

                var localPoint = building.transform.InverseTransformPoint(worldPoint);
                var baseHeight = BaseHeightOf(building);

                if (localPoint.y < baseHeight - 0.01f || localPoint.y > baseHeight + building.TotalHeight + 0.01f)
                    continue;

                foreach (var path in SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep))
                    if (PlanPolygons.IsInside(new Vector2(localPoint.x, localPoint.z), SplineBuildingMeshBuilder.InnerContourOf(building, path)))
                        return building;
            }

            return null;
        }


        // Этаж уровня, на котором стоит низ здания.
        public static int BaseStoreyOf(SplineBuilding building)
        {
            return Mathf.RoundToInt((BottomWorldHeightOf(building) - LiveBuilderStoreys.GroundLevel) / LiveBuilderStoreys.StoreyHeight);
        }


        // Здание стоит низом ровно на этаже уровня и с тем же шагом — его этажи совпадают с этажами окна.
        public static bool IsAlignedToLevel(SplineBuilding building)
        {
            var levelFloor = LiveBuilderStoreys.GroundLevel + BaseStoreyOf(building) * LiveBuilderStoreys.StoreyHeight;
            return Mathf.Abs(BottomWorldHeightOf(building) - levelFloor) < 0.005f && Mathf.Abs(building.StoreyHeight - LiveBuilderStoreys.StoreyHeight) < 0.005f;
        }


        // Поставить здание низом на ближайший этаж уровня и взять высоту этажа из окна. С Undo.
        public static void AlignToLevel(SplineBuilding building)
        {
            Undo.RecordObjects(new Object[] { building, building.transform }, "Align Building To Storeys");
            building.StoreyHeight = LiveBuilderStoreys.StoreyHeight;

            var levelFloor = LiveBuilderStoreys.GroundLevel + BaseStoreyOf(building) * LiveBuilderStoreys.StoreyHeight;
            building.transform.position += Vector3.up * (levelFloor - BottomWorldHeightOf(building));
        }

        // Низ здания в мире, м: низ плиты пола, а без пола — низ стен; на высоте контура.
        private static float BottomWorldHeightOf(SplineBuilding building)
        {
            return building.transform.TransformPoint(new Vector3(0f, BaseHeightOf(building), 0f)).y;
        }


        // ── Подсказка ─────────────────────────────────────────────────────

        // Подсказка внизу окна сцены. У лестницы копии — без верхнего этажа: с него идти некуда.
        public string DescriptionOf(bool skipsTopStorey)
        {
            var copiesAbove = StoreysAbove - 1 - (skipsTopStorey ? 1 : 0);
            return "Здание «" + Building.name + "», его этаж " + (Storey + 1) + " из " + Building.StoreyCount + " · " +
                   (IsOnAllStoreys && copiesAbove > 0 ? "этот и ещё " + copiesAbove + " выше" : "только этот этаж");
        }


        // ── Контур ────────────────────────────────────────────────────────

        // Внутренняя сторона стен здания в плане (X, Z здания) — по одному контуру на каждый сплайн.
        public List<List<Vector2>> InnerContours()
        {
            return SplineBuildingMeshBuilder.InnerContoursOf(Building);
        }


        // Точка в осях здания, прилипшая к внутренней стороне его стен, если до неё меньше чем полшага сетки: стена доходит до стены здания без щели.
        // Рядом сразу две стены здания (угол) — прилипает к обеим, ровно в угол.
        public Vector3 SnapToWalls(Vector3 localPoint)
        {
            return SnapToWalls(Building, localPoint);
        }

        // То же для любого здания — например, когда тянешь точку уже поставленной перегородки.
        public static Vector3 SnapToWalls(SplineBuilding building, Vector3 localPoint)
        {
            var maxDistance = BoxModelGrid.CurrentStep * ContourSnapFraction;
            var point = new Vector2(localPoint.x, localPoint.z);
            var nearEdges = new List<(Vector2 Start, Vector2 End, float Distance)>();

            foreach (var contour in SplineBuildingMeshBuilder.InnerContoursOf(building))
                for (var index = 0; index < contour.Count; index++)
                {
                    var start = contour[index];
                    var end = contour[(index + 1) % contour.Count];
                    var distance = Vector2.Distance(point, PlanPolygons.ClosestOnSegment(point, start, end));

                    if (distance < maxDistance)
                        nearEdges.Add((start, end, distance));
                }

            if (nearEdges.Count == 0)
                return localPoint;

            nearEdges.Sort((first, second) => first.Distance.CompareTo(second.Distance));
            var nearest = nearEdges[0];
            var snapped = PlanPolygons.ClosestOnSegment(point, nearest.Start, nearest.End);

            foreach (var other in nearEdges)
                if (TryIntersectLines(nearest.Start, nearest.End, other.Start, other.End, out var corner) && Vector2.Distance(point, corner) < maxDistance * 1.5f)
                {
                    snapped = corner;
                    break;
                }

            return new Vector3(snapped.x, localPoint.y, snapped.y);
        }

        // Пересечение двух прямых; почти параллельные — не пересекаются.
        private static bool TryIntersectLines(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd, out Vector2 intersection)
        {
            var first = firstEnd - firstStart;
            var second = secondEnd - secondStart;
            var cross = first.x * second.y - first.y * second.x;
            intersection = default;

            if (Mathf.Abs(cross) < 0.01f * first.magnitude * second.magnitude)
                return false;

            var offset = secondStart - firstStart;
            var along = (offset.x * second.y - offset.y * second.x) / cross;
            intersection = firstStart + first * along;
            return true;
        }


        // Как далеко точка плана от внутренней стороны стен здания, м.
        public float DistanceToWalls(Vector2 point)
        {
            return PlanPolygons.DistanceToContours(point, InnerContours());
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static float BaseHeightOf(SplineBuilding building)
        {
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            return paths.Count > 0 ? paths[0].BaseHeightAt(0f) : 0f;
        }
    }
}
