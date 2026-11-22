using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сверка частей одного здания (<see cref="SplineBuildingPart"/>) за проход: какие части на месте, их вырезы по габариту до вырезов,
    /// подписи с вырезами и собрана ли каждая по своей подписи. Сборка части берёт вырезы и подпись отсюда и второй раз их не ищет.
    /// </summary>
    internal sealed class SplineBuildingPartChecks
    {
        private readonly Dictionary<(SplineBuildingPartKind, int), SplineBuildingPartCheck> checks = new();


        // Каждая часть, которая должна быть и уже есть, — сверена один раз.
        public SplineBuildingPartChecks(SplineBuilding building, SplineBuildingPartLayout layout, LiveBuilderBuildQuality quality)
        {
            var parts = PartsByPlace(building);
            var worldKey = quality == LiveBuilderBuildQuality.Final ? SplineBuildingPartKeys.WorldKeyOf(building, layout.Paths) : null;

            foreach (var partKey in layout.Parts)
                if (parts.TryGetValue((partKey.Kind, partKey.Storey), out var part))
                    checks[(partKey.Kind, partKey.Storey)] = CheckPart(part, partKey.Key, quality, worldKey);
        }

        // Части здания по виду и этажу, и выключенные тоже.
        private static Dictionary<(SplineBuildingPartKind, int), SplineBuildingPart> PartsByPlace(SplineBuilding building)
        {
            var parts = new Dictionary<(SplineBuildingPartKind, int), SplineBuildingPart>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
                parts.TryAdd((part.Kind, part.Kind == SplineBuildingPartKind.Roof ? 0 : part.Storey), part);

            return parts;
        }

        // Собрана по этой подписи и с теми вырезами, что режут её сейчас; габарит до вырезов — от прошлой сборки, при той же подписи он тот же.
        private static SplineBuildingPartCheck CheckPart(SplineBuildingPart part, string partKey, LiveBuilderBuildQuality quality, string worldKey)
        {
            if (part.GetComponent<MeshFilter>().sharedMesh == null)
                return new SplineBuildingPartCheck(part, part.UncutBounds, null, null, false);

            var cutters = CuttersOf(part, part.UncutBounds);
            var partSignature = partKey + SplineBuildingPartKeys.WorldKeyOfPart(part, quality, worldKey)
                                        + GeometryCutterLookup.KeyOfTouchingCopies(part, part.UncutBounds, cutters);
            var isUpToDate = LiveBuilderDraftBuilds.IsBuilt(part, part.BuiltSignature, partSignature, quality);
            return new SplineBuildingPartCheck(part, part.UncutBounds, cutters, partSignature, isUpToDate);
        }


        // ── Итог ─────────────────────────────────────────────────────────

        // Части нет — она будет создана: false.
        public bool TryGet(SplineBuildingPartKey partKey, out SplineBuildingPartCheck check)
        {
            return checks.TryGetValue((partKey.Kind, partKey.Storey), out check);
        }


        // Все нужные части на месте и собраны по своим подписям.
        public bool AreAllUpToDate(SplineBuildingPartLayout layout)
        {
            foreach (var partKey in layout.Parts)
                if (!TryGet(partKey, out var check) || !check.IsUpToDate)
                    return false;

            return true;
        }


        // ── Вырезы ───────────────────────────────────────────────────────

        // Вырезы, что режут часть с этим габаритом до вырезов (в осях части).
        public static List<GeometryCutter> CuttersOf(SplineBuildingPart part, Bounds uncutBounds)
        {
            return GeometryCutterLookup.CuttersFor(part, GeometryCutterLookup.WorldBoundsOf(uncutBounds, part.transform));
        }
    }


    /// <summary>Сверка одной части здания за проход: её вырезы по габариту до вырезов, подпись с ними и собрана ли она по этой подписи.</summary>
    internal sealed class SplineBuildingPartCheck
    {
        public SplineBuildingPartCheck(SplineBuildingPart part, Bounds uncutBounds, List<GeometryCutter> cutters, string signature, bool isUpToDate)
        {
            Part = part;
            UncutBounds = uncutBounds;
            Cutters = cutters;
            Signature = signature;
            IsUpToDate = isUpToDate;
        }


        public SplineBuildingPart Part { get; }
        public Bounds UncutBounds { get; }

        // Нет меша — вырезы не искались: null.
        public List<GeometryCutter> Cutters { get; }

        public string Signature { get; }
        public bool IsUpToDate { get; }
    }
}
