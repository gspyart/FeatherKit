using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прямоугольник на полу для «Комнаты» и «Лестницы»: зажал — первый угол, тянешь — второй, всё по сетке.
    /// Пол — этаж здания по контуру под мышью (углы прилипают к его стенам), верх объекта из коробок, а если там стена, потолок,
    /// другая геометрия или пусто — пол текущего этажа.
    /// Помнит, начат ли он внутри объекта из коробок — на полу под потолком того же объекта или в его сплошном объёме: тогда новое вырезается в нём.
    /// Тот же поиск пола под потолком дома нужен и комнате по контуру — <see cref="TryFindHouseFloor"/>.
    /// </summary>
    public sealed class LiveBuilderFloorRectangle
    {
        // Индекс граней «верх» и «низ» в BoxFaces.
        private const int TopFace = 2;
        private const int BottomFace = 3;

        private bool isStarted;


        // Начат и не отменён. Здание, на полу которого начали, удалили или отменили — жест кончился сам.
        public bool IsActive => isStarted && (BuildingContext == null || BuildingContext.IsBuildingAlive);

        public BoxModelDrawPlane Plane { get; private set; }

        // Начат на полу объекта из коробок — внутри дома.
        public bool IsInsideModel { get; private set; }

        // Углы в осях рамки плоскости; высота — пол.
        public Vector3 Start { get; private set; }
        public Vector3 End { get; private set; }

        public Vector3 Min => Vector3.Min(Start, End);
        public Vector3 Max => Vector3.Max(Start, End);

        public bool HasArea => !Mathf.Approximately(Start.x, End.x) && !Mathf.Approximately(Start.z, End.z);

        // Модель, в которую пойдёт новое: та, на чьём полу начали, или выделенная; null — будет новая.
        public BoxModel TargetModel => Plane.Model;

        // Начат на полу здания по контуру — его этаж, высоты и стены; углы тогда в осях здания и прилипают к его стенам.
        public LiveBuilderBuildingContext BuildingContext { get; private set; }


        // ── Жест ─────────────────────────────────────────────────────────

        // Здание под мышью — на полу его этажа; пол объекта из коробок — на нём; иначе — пол текущего этажа.
        public bool TryBegin(Ray worldRay)
        {
            BuildingContext = null;

            if (LiveBuilderBuildingContext.TryFind(worldRay, out var context) && context.FloorPlane.TryIntersect(worldRay, out var buildingPoint))
            {
                BuildingContext = context;
                Plane = context.FloorPlane;
                Start = SnapInBuilding(buildingPoint);
                End = Start;
                IsInsideModel = false;
                isStarted = true;
                return true;
            }

            var isOnModelFloor = BoxModelPicker.TryPickSurface(worldRay, out var surfaceHit) && surfaceHit.Surface.Face == TopFace;
            BoxModelDrawPlane plane;
            Vector3 point;

            if (isOnModelFloor && BoxModelDrawPlaneFinder.TryFind(worldRay, false, out plane, out point))
            {
                IsInsideModel = TryFindCeilingAbove(surfaceHit.Model, surfaceHit.LocalPoint, out _);
            }
            else
            {
                if (!TryFindStoreyFloor(worldRay, out plane, out point))
                    return false;

                IsInsideModel = plane.Model != null && IsInsideSolid(plane.Model, point);
            }

            Plane = plane;
            Start = point;
            End = point;
            isStarted = true;
            return true;
        }


        public void Update(Ray worldRay)
        {
            if (IsActive && Plane.TryIntersect(worldRay, out var point))
                End = BuildingContext != null ? SnapInBuilding(point) : Plane.SnapOnPlane(point);
        }


        public void Cancel()
        {
            isStarted = false;
            IsInsideModel = false;
            BuildingContext = null;
        }

        // В здании: по сетке и к стенам здания, если до них меньше полшага.
        private Vector3 SnapInBuilding(Vector3 localPoint)
        {
            return BuildingContext.SnapToWalls(Plane.SnapOnPlane(localPoint));
        }


        // Точка рамки в мир.
        public Vector3 ToWorld(Vector3 framePoint)
        {
            return Plane.FrameToWorld.MultiplyPoint3x4(framePoint);
        }


        // Пол дома из коробок под мышью: верх объекта, над которым в пределах этажа его же потолок. Точка — по сетке, в мире;
        // высота — от пола до этого потолка, м. Так комната по контуру в доме знает, докуда ставить стены.
        public static bool TryFindHouseFloor(Ray worldRay, out BoxModel house, out Vector3 worldPoint, out float ceilingHeight)
        {
            house = null;
            worldPoint = default;
            ceilingHeight = 0f;

            if (!BoxModelPicker.TryPickSurface(worldRay, out var surfaceHit) || surfaceHit.Surface.Face != TopFace
                || !TryFindCeilingAbove(surfaceHit.Model, surfaceHit.LocalPoint, out ceilingHeight)
                || !BoxModelDrawPlaneFinder.TryFind(worldRay, false, out var plane, out var point))
                return false;

            house = surfaceHit.Model;
            worldPoint = plane.FrameToWorld.MultiplyPoint3x4(point);
            return true;
        }


        // Пол текущего этажа: в осях выделенного объекта, а без выделения — того, внутри чьего объёма точка (сплошной дом под разрезом).
        private static bool TryFindStoreyFloor(Ray worldRay, out BoxModelDrawPlane plane, out Vector3 point)
        {
            plane = BoxModelDrawPlaneFinder.StoreyFloorPlane(BoxModelActions.SelectedModel());

            if (!plane.TryIntersect(worldRay, out point))
                return false;

            if (plane.Model == null)
            {
                var worldPoint = point;

                foreach (var model in StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModel>())
                    if (IsInsideSolid(model, model.transform.InverseTransformPoint(worldPoint)))
                    {
                        plane = BoxModelDrawPlaneFinder.StoreyFloorPlane(model);
                        plane.TryIntersect(worldRay, out point);
                        break;
                    }
            }

            point = plane.SnapOnPlane(point);
            return true;
        }


        // Чуть выше точки пола — сплошной объём объекта: комната вырезается в нём.
        private static bool IsInsideSolid(BoxModel model, Vector3 localPoint)
        {
            var probe = MillimeterBox.ToMillimeters(localPoint + Vector3.up * 0.05f);
            var shapes = new List<BoxModelPartShape>();

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                shapes.Add(box.Shape);

            foreach (var cell in BoxModelSolver.Solve(shapes))
                if (probe.x > cell.Box.Min.x && probe.x < cell.Box.Max.x && probe.y > cell.Box.Min.y && probe.y < cell.Box.Max.y
                    && probe.z > cell.Box.Min.z && probe.z < cell.Box.Max.z)
                    return true;

            return false;
        }


        // Над точкой пола в пределах этажа есть потолок того же объекта — значит, это пол внутри дома, а не крыша. Высота — до ближайшего потолка, м.
        private static bool TryFindCeilingAbove(BoxModel model, Vector3 localPoint, out float ceilingHeight)
        {
            var x = Mathf.RoundToInt(localPoint.x * MillimeterBox.MillimetersPerMeter);
            var z = Mathf.RoundToInt(localPoint.z * MillimeterBox.MillimetersPerMeter);
            var floor = Mathf.RoundToInt(localPoint.y * MillimeterBox.MillimetersPerMeter);
            var reach = Mathf.RoundToInt((LiveBuilderStoreys.StoreyHeight + 0.5f) * MillimeterBox.MillimetersPerMeter);
            var nearest = int.MaxValue;

            foreach (var surface in BoxModelRebuilder.GetSurfaces(model))
            {
                var area = surface.Area;
                var isAbove = surface.Plane > floor && surface.Plane <= floor + reach;

                if (surface.Face == BottomFace && isAbove && x >= area.xMin && x <= area.xMax && z >= area.yMin && z <= area.yMax)
                    nearest = Mathf.Min(nearest, surface.Plane);
            }

            ceilingHeight = nearest < int.MaxValue ? (nearest - floor) / (float)MillimeterBox.MillimetersPerMeter : 0f;
            return nearest < int.MaxValue;
        }
    }
}
