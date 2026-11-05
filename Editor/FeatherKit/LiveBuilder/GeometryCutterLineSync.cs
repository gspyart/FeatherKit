using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Держит вырезы с включённым «местом в стене» (<see cref="GeometryCutter"/>) на их местах
    /// линии стены или контура здания — перед каждой пересборкой. Каждый помнит сплайн и место на нём: номер точки с долей до следующей.
    /// Место — у самой дыры (низ формы со сдвигом), а не у точки объекта: дыру, сдвинутую на другой отрезок, держит её отрезок.
    /// Сдвинули точки — едет на то же место своего отрезка; вырез ещё и держит свой сдвиг от линии и поворачивается вместе с отрезком.
    /// Точек стало больше или меньше — остаётся где стоял и запоминает новое место; сдвинули или повернули его самого — запоминается, куда.
    /// Поменяли толщину стены — сквозной вырез снова насквозь: глубина и место поперёк — по новой стене.
    /// Высоту не трогает. Правки — без Undo, как у привязки к этажам: после любого Ctrl+Z сверка повторяет их сама.
    /// </summary>
    public static class GeometryCutterLineSync
    {
        private const float Tolerance = 0.001f;

        // Поворот меньше этого, в градусах, — не поворачивали.
        private const float RotationTolerance = 0.01f;


        // Все вырезы на линии — сцены или открытого префаба. Подпись каждой линии считаем один раз на проход. Ошибка в одном не останавливает остальные.
        public static void SyncAll()
        {
            var lineHashes = new Dictionary<SplineContainer, int>();
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var cutter in stage.FindComponentsOfType<GeometryCutter>())
            {
                try
                {
                    if (cutter.FollowsWallLine && TryGetLine(cutter, lineHashes, out var pathOwner, out var container, out var lineHash))
                        SyncCutter(cutter, pathOwner, container, lineHash);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, cutter);
                }
            }
        }

        // Стена или здание, в котором лежит вырез, и его линия.
        private static bool TryGetLine(Component hole, Dictionary<SplineContainer, int> lineHashes, out Component pathOwner, out SplineContainer container, out int lineHash)
        {
            pathOwner = LiveBuilderPathOwners.OwnerAbove(hole);
            container = null;
            lineHash = 0;

            if (pathOwner == null || !pathOwner.TryGetComponent(out container) || !HasUsableSpline(container))
                return false;

            if (!lineHashes.TryGetValue(container, out lineHash))
                lineHashes[container] = lineHash = LiveBuilderSyncEdits.LineHashOf(container);

            return true;
        }


        // ── Вырез ────────────────────────────────────────────────────────

        // Линию двигали, а число точек то же, — вырез едет за своим отрезком. Иначе, если что-то поменялось (и поворот руками), — запомнить, где он сейчас.
        // Потом — толщина стены: поменялась — сквозной вырез снова насквозь.
        private static void SyncCutter(GeometryCutter cutter, Component pathOwner, SplineContainer container, int lineHash)
        {
            var owner = pathOwner.transform;
            var thickness = ThicknessOf(pathOwner);
            var previousThickness = cutter.SyncedWallThickness;
            var localPosition = owner.InverseTransformPoint(GeometryCutterOutline.ShapeBottomOf(cutter));
            var localRotation = Quaternion.Inverse(owner.rotation) * cutter.transform.rotation;
            var isLineChanged = cutter.SyncedLineHash != lineHash;
            var isCutterMoved = (localPosition - cutter.SyncedLocalPosition).sqrMagnitude > Tolerance * Tolerance
                || Quaternion.Angle(localRotation, cutter.SyncedLocalRotation) > RotationTolerance;

            if (isLineChanged && cutter.SyncedLineHash != 0 && HasSameKnots(container, cutter.LineSplineIndex, cutter.SyncedKnotCount))
                MoveCutterToPlace(cutter, owner, container, localPosition, localRotation, lineHash, thickness);
            else if (isLineChanged || isCutterMoved)
                RememberCutterPlace(cutter, container, localPosition, localRotation, lineHash, thickness);

            FitToNewThickness(cutter, pathOwner, container, lineHash, previousThickness, thickness);
        }

        // На запомненное место линии — с тем же сдвигом от неё и поворотом относительно неё; высота — своя.
        private static void MoveCutterToPlace(GeometryCutter cutter, Transform owner, SplineContainer container, Vector3 localPosition, Quaternion localRotation,
            int lineHash, float thickness)
        {
            LiveBuilderSyncEdits.FrameAt(container, cutter.LineSplineIndex, cutter.PlaceOnLine, out var linePoint, out var lineRotation);
            var offset = lineRotation * cutter.OffsetFromLine;
            var targetPosition = new Vector3(linePoint.x + offset.x, localPosition.y, linePoint.z + offset.z);
            var targetRotation = lineRotation * cutter.RotationOnLine;

            if ((targetPosition - localPosition).sqrMagnitude > Tolerance * Tolerance || Quaternion.Angle(targetRotation, localRotation) > RotationTolerance)
                LiveBuilderSyncEdits.Apply(cutter.transform, () => PlaceShapeBottom(cutter, owner.TransformPoint(targetPosition), owner.rotation * targetRotation));

            LiveBuilderSyncEdits.Apply(cutter, () =>
            {
                cutter.SyncedLineHash = lineHash;
                cutter.SyncedLocalPosition = targetPosition;
                cutter.SyncedLocalRotation = targetRotation;
                cutter.SyncedWallThickness = thickness;
            });
        }

        // Объект — так, чтобы низ дыры встал в эту точку: со сдвигом формы сам объект стоит в стороне от дыры.
        private static void PlaceShapeBottom(GeometryCutter cutter, Vector3 shapeBottom, Quaternion rotation)
        {
            cutter.transform.rotation = rotation;
            cutter.transform.position += shapeBottom - GeometryCutterOutline.ShapeBottomOf(cutter);
        }

        // Ближайшее место на линии; сдвиг выреза от неё в плане и поворот — в осях линии в этом месте.
        private static void RememberCutterPlace(GeometryCutter cutter, SplineContainer container, Vector3 localPosition, Quaternion localRotation, int lineHash,
            float thickness)
        {
            LiveBuilderSyncEdits.NearestPlace(container, localPosition, out var splineIndex, out var place);
            LiveBuilderSyncEdits.FrameAt(container, splineIndex, place, out var linePoint, out var lineRotation);

            var horizontalOffset = localPosition - linePoint;
            horizontalOffset.y = 0f;
            var toLine = Quaternion.Inverse(lineRotation);

            LiveBuilderSyncEdits.Apply(cutter, () =>
            {
                cutter.LineSplineIndex = splineIndex;
                cutter.PlaceOnLine = place;
                cutter.OffsetFromLine = toLine * horizontalOffset;
                cutter.RotationOnLine = toLine * localRotation;
                cutter.SyncedKnotCount = container.Splines[splineIndex].Count;
                cutter.SyncedLineHash = lineHash;
                cutter.SyncedLocalPosition = localPosition;
                cutter.SyncedLocalRotation = localRotation;
                cutter.SyncedWallThickness = thickness;
            });
        }


        // ── Толщина стены ────────────────────────────────────────────────

        // Стене или зданию поменяли толщину (числом или типом): вырез, который был глубже прежней стены, — сквозной;
        // он снова насквозь новой — глубина и место поперёк, как ставит «Проём», поворот свой. Ниша (мельче стены) остаётся как была.
        // Прежняя толщина не запомнена (вырез со старых сцен) — не трогаем: запомнится при следующем сдвиге линии или выреза.
        private static void FitToNewThickness(GeometryCutter cutter, Component pathOwner, SplineContainer container, int lineHash, float previousThickness,
            float thickness)
        {
            if (previousThickness <= 0f || Mathf.Abs(thickness - previousThickness) <= Tolerance)
                return;

            if (cutter.Depth >= previousThickness - Tolerance && TryFitThroughWall(cutter, pathOwner))
            {
                var owner = pathOwner.transform;
                var localPosition = owner.InverseTransformPoint(GeometryCutterOutline.ShapeBottomOf(cutter));
                var localRotation = Quaternion.Inverse(owner.rotation) * cutter.transform.rotation;
                RememberCutterPlace(cutter, container, localPosition, localRotation, lineHash, thickness);
                return;
            }

            LiveBuilderSyncEdits.Apply(cutter, () => cutter.SyncedWallThickness = thickness);
        }

        // Глубина — толщина стены с запасом, низ дыры — посередине стены напротив того места, где дыра сейчас.
        private static bool TryFitThroughWall(GeometryCutter cutter, Component pathOwner)
        {
            var owner = pathOwner.transform;
            var localBottom = owner.InverseTransformPoint(GeometryCutterOutline.ShapeBottomOf(cutter));
            SplineWallPath nearestPath = null;
            var nearestGap = float.MaxValue;
            var along = 0f;

            foreach (var path in LiveBuilderPathOwners.PathsOf(pathOwner))
            {
                var pathAlong = path.ClosestDistanceTo(localBottom, out var gap);

                if (gap >= nearestGap)
                    continue;

                nearestGap = gap;
                nearestPath = path;
                along = pathAlong;
            }

            if (nearestPath == null)
                return false;

            LiveBuilderOpeningPlacer.FitCutterToWall(pathOwner, nearestPath, along - cutter.Width / 2f, cutter.Width, localBottom.y,
                out var fittedBottom, out _, out var depth);

            LiveBuilderSyncEdits.Apply(cutter.transform, () => PlaceShapeBottom(cutter, owner.TransformPoint(fittedBottom), cutter.transform.rotation));
            LiveBuilderSyncEdits.Apply(cutter, () => cutter.Depth = depth);
            return true;
        }

        // Толщина стены по линии или стен здания, с типом — из типа.
        private static float ThicknessOf(Component pathOwner)
        {
            return pathOwner is SplineBuilding building ? building.WallThickness : ((SplineWall)pathOwner).Thickness;
        }


        // ── Линия ────────────────────────────────────────────────────────

        private static bool HasSameKnots(SplineContainer container, int splineIndex, int knotCount)
        {
            return knotCount >= 2 && splineIndex < container.Splines.Count && container.Splines[splineIndex].Count == knotCount;
        }

        // Хоть один сплайн из двух точек и больше: на пустом место на линии не посчитать — вырез улетел бы в бесконечность.
        private static bool HasUsableSpline(SplineContainer container)
        {
            foreach (var spline in container.Splines)
                if (spline.Count >= 2)
                    return true;

            return false;
        }
    }
}
