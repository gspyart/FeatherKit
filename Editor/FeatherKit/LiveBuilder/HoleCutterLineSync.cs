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
    /// Высоту не трогает. Правки — без Undo, как у привязки к этажам: после любого Ctrl+Z сверка повторяет их сама.
    /// </summary>
    public static class HoleCutterLineSync
    {
        private const float Tolerance = 0.001f;

        // Поворот меньше этого, в градусах, — не поворачивали.
        private const float RotationTolerance = 0.01f;


        // Все вырезы на линии — сцены или открытого префаба. Подпись каждой линии считаем один раз на проход.
        public static void SyncAll()
        {
            var lineHashes = new Dictionary<SplineContainer, int>();
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var cutter in stage.FindComponentsOfType<GeometryCutter>())
                if (cutter.FollowsWallLine && TryGetLine(cutter, lineHashes, out var owner, out var container, out var lineHash))
                    SyncCutter(cutter, owner, container, lineHash);
        }

        // Линия стены или здания, в котором лежит вырез.
        private static bool TryGetLine(Component hole, Dictionary<SplineContainer, int> lineHashes, out Transform owner, out SplineContainer container, out int lineHash)
        {
            var pathOwner = LiveBuilderPathOwners.OwnerAbove(hole);
            owner = pathOwner != null ? pathOwner.transform : null;
            container = null;
            lineHash = 0;

            if (pathOwner == null || !pathOwner.TryGetComponent(out container) || container.Splines.Count == 0)
                return false;

            if (!lineHashes.TryGetValue(container, out lineHash))
                lineHashes[container] = lineHash = LiveBuilderSyncEdits.LineHashOf(container);

            return true;
        }


        // ── Вырез ────────────────────────────────────────────────────────

        // Линию двигали, а число точек то же, — вырез едет за своим отрезком. Иначе, если что-то поменялось (и поворот руками), — запомнить, где он сейчас.
        private static void SyncCutter(GeometryCutter cutter, Transform owner, SplineContainer container, int lineHash)
        {
            var localPosition = owner.InverseTransformPoint(GeometryCutterOutline.ShapeBottomOf(cutter));
            var localRotation = Quaternion.Inverse(owner.rotation) * cutter.transform.rotation;
            var isLineChanged = cutter.SyncedLineHash != lineHash;
            var isCutterMoved = (localPosition - cutter.SyncedLocalPosition).sqrMagnitude > Tolerance * Tolerance
                || Quaternion.Angle(localRotation, cutter.SyncedLocalRotation) > RotationTolerance;

            if (isLineChanged && cutter.SyncedLineHash != 0 && HasSameKnots(container, cutter.LineSplineIndex, cutter.SyncedKnotCount))
                MoveCutterToPlace(cutter, owner, container, localPosition, localRotation, lineHash);
            else if (isLineChanged || isCutterMoved)
                RememberCutterPlace(cutter, container, localPosition, localRotation, lineHash);
        }

        // На запомненное место линии — с тем же сдвигом от неё и поворотом относительно неё; высота — своя.
        private static void MoveCutterToPlace(GeometryCutter cutter, Transform owner, SplineContainer container, Vector3 localPosition, Quaternion localRotation,
            int lineHash)
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
            });
        }

        // Объект — так, чтобы низ дыры встал в эту точку: со сдвигом формы сам объект стоит в стороне от дыры.
        private static void PlaceShapeBottom(GeometryCutter cutter, Vector3 shapeBottom, Quaternion rotation)
        {
            cutter.transform.rotation = rotation;
            cutter.transform.position += shapeBottom - GeometryCutterOutline.ShapeBottomOf(cutter);
        }

        // Ближайшее место на линии; сдвиг выреза от неё в плане и поворот — в осях линии в этом месте.
        private static void RememberCutterPlace(GeometryCutter cutter, SplineContainer container, Vector3 localPosition, Quaternion localRotation, int lineHash)
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
            });
        }


        // ── Линия ────────────────────────────────────────────────────────

        private static bool HasSameKnots(SplineContainer container, int splineIndex, int knotCount)
        {
            return splineIndex < container.Splines.Count && container.Splines[splineIndex].Count == knotCount;
        }
    }
}
