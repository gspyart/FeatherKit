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
    /// Прилипание к стенам здания — <see cref="SplineBuildingWallSnapping"/>, здание и этажи уровня — <see cref="SplineBuildingLevelAlignment"/>.
    /// </summary>
    public sealed class LiveBuilderBuildingContext
    {
        private const string AllStoreysSessionKey = "FeatherKit.LiveBuilder.IsOnAllStoreys";

        // Что-то перед полом этажа выше него хотя бы на столько — закрывает его от мыши, м: земля вровень с полом не закрывает.
        private const float FloorBlockTolerance = 0.05f;

        // Больше зданий в памяти — забыть всех: удалённые здания иначе копились бы.
        private const int MaxCachedBuildings = 256;

        // Внутренние стороны стен зданий в плане и подпись того, из чего они посчитаны (<see cref="InnerContoursOf"/>).
        private static readonly Dictionary<SplineBuilding, (Hash128 Hash, List<List<Vector2>> Contours)> InnerContoursCache = new();


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

        // Сколько этажей с этого и выше, считая этот.
        public int StoreysAbove => Building.StoreyCount - Storey;

        // Сколько копий вверх у нового: этот этаж — одна, «все выше» — до верхнего этажа.
        public int CopyCount => IsOnAllStoreys ? StoreysAbove : 1;

        // Высота пола этого этажа над контуром здания, в его осях, м.
        public float FloorTopLocal => SplineBuildingLevelAlignment.BaseHeightOf(Building) + Building.FloorTopHeight + Storey * Building.StoreyHeight;

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

            var storey = LiveBuilderStoreys.CurrentStorey - SplineBuildingLevelAlignment.BaseStoreyOf(building);

            if (storey < 0 || storey >= building.StoreyCount)
                return false;

            context = new LiveBuilderBuildingContext(building, storey);
            return true;
        }

        // Ближайшее под мышью — часть здания или его интерьера: это здание. Иначе — здание, внутри стен которого пол этажа под мышью,
        // но только если до пола луч ничего не задел: платформа или коробка перед зданием — мышь на ней, а не на полу здания за ней.
        // Коробки без коллайдера тоже закрывают пол — их ищет BoxModelPicker.
        private static SplineBuilding BuildingUnder(Ray worldRay)
        {
            var nearestDistance = float.MaxValue;
            SplineBuilding nearestBuilding = null;

            if (BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var hit))
            {
                nearestDistance = hit.distance;
                nearestBuilding = hit.collider.GetComponentInParent<SplineBuilding>();
            }

            if (BoxModelPicker.TryPickSurface(worldRay, out var surfaceHit) && surfaceHit.Distance < nearestDistance)
            {
                nearestDistance = surfaceHit.Distance;
                nearestBuilding = surfaceHit.Model.GetComponentInParent<SplineBuilding>();
            }

            if (nearestBuilding != null)
                return nearestBuilding;

            var floor = BoxModelDrawPlaneFinder.StoreyFloorPlane(null);

            if (!floor.TryIntersect(worldRay, out var floorPoint) || IsAboveFloorInFront(worldRay, nearestDistance, floorPoint))
                return null;

            foreach (var building in UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
            {
                if (!building.gameObject.activeInHierarchy)
                    continue;

                var local = building.transform.InverseTransformPoint(floorPoint);

                if (IsInsideInnerContours(building, new Vector2(local.x, local.z)))
                    return building;
            }

            return null;
        }

        // Луч задел что-то раньше пола этажа, и оно выше пола: стоит на нём или над ним — закрывает пол. Земля вровень с полом — нет.
        private static bool IsAboveFloorInFront(Ray worldRay, float hitDistance, Vector3 floorPoint)
        {
            if (hitDistance >= Vector3.Distance(worldRay.origin, floorPoint))
                return false;

            return worldRay.GetPoint(hitDistance).y > floorPoint.y + FloorBlockTolerance;
        }


        // Здание этого объекта, внутри которого точка мира: в плане — внутри его стен, по высоте — от пола до крыши. Нет такого — null.
        public static SplineBuilding BuildingContaining(BoxModel model, Vector3 worldPoint)
        {
            foreach (var building in model.GetComponentsInChildren<SplineBuilding>(false))
                if (SplineWallRebuilder.OuterModelOf(building) == model && Contains(building, worldPoint))
                    return building;

            return null;
        }


        // Любое включённое здание сцены или открытого префаба, внутри которого точка мира. Нет такого — null.
        public static SplineBuilding BuildingContaining(Vector3 worldPoint)
        {
            foreach (var building in UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
                if (building.gameObject.activeInHierarchy && Contains(building, worldPoint))
                    return building;

            return null;
        }

        // Точка мира внутри здания: в плане — внутри его стен, по высоте — от пола до крыши.
        private static bool Contains(SplineBuilding building, Vector3 worldPoint)
        {
            var localPoint = building.transform.InverseTransformPoint(worldPoint);
            var baseHeight = SplineBuildingLevelAlignment.BaseHeightOf(building);

            if (localPoint.y < baseHeight - 0.01f || localPoint.y > baseHeight + building.TotalHeight + 0.01f)
                return false;

            return IsInsideInnerContours(building, new Vector2(localPoint.x, localPoint.z));
        }


        // Точка плана здания, в его осях, — внутри его стен.
        private static bool IsInsideInnerContours(SplineBuilding building, Vector2 localPlanPoint)
        {
            foreach (var contour in InnerContoursOf(building))
                if (PlanPolygons.IsInside(localPlanPoint, contour))
                    return true;

            return false;
        }

        // Внутренние стороны стен здания в плане. Здание под мышью ищется на каждое её движение — контуры помнятся,
        // пока не поменялись линия, толщина стен, где линия относительно них и шаг изгибов.
        private static List<List<Vector2>> InnerContoursOf(SplineBuilding building)
        {
            var container = building.GetComponent<SplineContainer>();
            var hash = new Hash128();
            hash.Append(LiveBuilderSyncEdits.LineHashOf(container));
            hash.Append(building.WallThickness);
            hash.Append((int)building.ContourSide);
            hash.Append(building.CurveStep);
            hash.Append(building.CornerSegments);

            if (InnerContoursCache.TryGetValue(building, out var cached) && cached.Hash == hash)
                return cached.Contours;

            var contours = new List<List<Vector2>>();

            foreach (var path in SplineWallPath.FromContainer(container, building.CurveStep))
                contours.Add(SplineBuildingMeshBuilder.InnerContourOf(building, path));

            if (InnerContoursCache.Count >= MaxCachedBuildings)
                InnerContoursCache.Clear();

            InnerContoursCache[building] = (hash, contours);
            return contours;
        }


        // ── Подсказка ─────────────────────────────────────────────────────

        // Подсказка внизу окна сцены. У лестницы копии — без верхнего этажа: с него идти некуда.
        public string DescriptionOf(bool skipsTopStorey)
        {
            var group = BoxModelFactory.SelectedGroupIn(Building);

            // Выделена группа здания — новое встаёт в неё и повторяется по этажам вместе с ней.
            if (group != null)
                return "Здание «" + Building.name + "» · в группу «" + group.name + "»";

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
            return SplineBuildingWallSnapping.SnapToWalls(Building, localPoint);
        }


        // Как далеко точка плана от внутренней стороны стен здания, м.
        public float DistanceToWalls(Vector2 point)
        {
            return PlanPolygons.DistanceToContours(point, InnerContours());
        }
    }
}
