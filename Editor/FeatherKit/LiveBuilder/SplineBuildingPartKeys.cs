using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Подписи частей здания по контуру (<see cref="SplineBuildingPart"/>) до вырезов: поменялась подпись — часть пересобирается, остальные нет.
    /// - общее у здания: контур, толщины, высота этажа, материалы
    /// - своё у части: вид, этаж, её высоты, верхний ли этаж, проёмы стен и дыры плит, которые её касаются
    /// - здание под повтором (<see cref="LiveBuilderArray"/>) — то же у каждой копии и её сдвиг
    /// Числа этажей в подписи нет: новый этаж пересобирает только верх.
    /// Текстура (подгонки, World, плитки) — в общем; мировой ключ — отдельно (<see cref="WorldKeyOf"/>): он зависит от места здания.
    /// </summary>
    public static class SplineBuildingPartKeys
    {
        // Какие части должны быть у здания, из чего они строятся и их подписи — по числам здания и вырезающим коробкам объекта вокруг.
        public static SplineBuildingPartLayout LayoutOf(SplineBuilding building)
        {
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var copies = SplineWallRebuilder.CopiesOf(building, paths, true);
            var hasArrayCopies = LiveBuilderArrayCopyPoses.HasArrayCopies(building.transform);
            var commonKey = CommonKeyOf(building);
            var parts = new List<SplineBuildingPartKey>();

            foreach (var (kind, storey) in SplineBuildingMeshBuilder.PartsOf(building))
            {
                var key = commonKey;
                key.Append((int)kind);
                key.Append(storey);

                // Без повторов — одна копия на месте: подпись та же, что была до повторов.
                if (hasArrayCopies)
                    key.Append(copies.Count);

                foreach (var copy in copies)
                {
                    if (hasArrayCopies)
                        LiveBuilderArrayCopyPoses.AppendQuantized(ref key, copy.LocalOffset);

                    if (kind == SplineBuildingPartKind.Walls)
                        AppendWallsKey(ref key, building, storey, copy.HolesByPath);
                    else
                        AppendSlabKey(ref key, building, kind, storey, paths, copy.SlabHoles);
                }

                parts.Add(new SplineBuildingPartKey(kind, storey, key.ToString()));
            }

            return new SplineBuildingPartLayout(paths, copies, parts);
        }

        // Общее у всех частей: версия раскладки, числа здания без числа этажей, материалы, точки контура.
        private static Hash128 CommonKeyOf(SplineBuilding building)
        {
            var key = new Hash128();
            key.Append(SplineBuildingRebuilder.LayoutVersion);
            key.Append(building.StoreyHeight);
            key.Append(building.WallThickness);
            key.Append((int)building.ContourSide);
            key.Append(building.SlabThickness);
            key.Append(building.HasFloor ? 1 : 0);
            key.Append(building.HasCollider ? 1 : 0);
            key.Append(building.CurveStep);
            key.Append(building.CornerSegments);

            foreach (var material in new[] { building.OuterWallMaterial, building.InnerWallMaterial, building.FloorMaterial, building.CeilingMaterial, building.RoofMaterial })
                key.Append(BoxModelSideMaterials.KeyOf(material));

            AppendTexture(ref key, building);
            LiveBuilderStableHash.AppendSplines(ref key, building.GetComponent<SplineContainer>());
            return key;
        }


        // Выравнивание World, подгонки и плитки материалов здания — только не по умолчанию: подписи старых зданий прежние.
        public static void AppendTexture(ref Hash128 key, SplineBuilding building)
        {
            LiveBuilderTextureSignature.AppendAlignment(ref key, building.TextureAlignment);
            LiveBuilderTextureSignature.AppendFits(ref key, building.TextureFits);

            if (!LiveBuilderMaterialTiling.HasAnySize)
                return;

            foreach (var material in SplineBuildingMeshBuilder.MaterialsOf(building, BoxModelFactory.LoadDefaultMaterial()))
                LiveBuilderTextureSignature.AppendTile(ref key, material);
        }


        // Мировой ключ здания (World): сдвиги осей и отрезков контура по модулю периодов рисунка всех его мест; Object — пусто.
        public static string WorldKeyOf(SplineBuilding building, IReadOnlyList<SplineWallPath> paths)
        {
            var space = LiveBuilderTextureSpace.Of(building.transform, building.TextureAlignment);

            if (!space.IsWorld)
                return "";

            var periods = new LiveBuilderTexturePeriods();

            foreach (var material in SplineBuildingMeshBuilder.MaterialsOf(building, BoxModelFactory.LoadDefaultMaterial()))
                SplineWallSignature.AddPeriods(periods, building.TextureFits, material);

            return LiveBuilderTextureSignature.WorldKeyOf(space, periods, SplineWallSignature.WorldShiftsOf(space, paths));
        }


        // Мировой ключ части: чистовая — нынешний у здания, черновая — тот, по которому часть собрана.
        public static string WorldKeyOfPart(SplineBuildingPart part, LiveBuilderBuildQuality quality, string currentBuildingKey)
        {
            return quality == LiveBuilderBuildQuality.Final ? currentBuildingKey : LiveBuilderTextureSignature.WorldKeyIn(part.BuiltSignature);
        }


        // ── Своё у части ─────────────────────────────────────────────────

        // Проём на другом этаже тоже режет стену вертикальным швом: в подписи все проёмы, а их высоты — в пределах этажа, как их видит сборка.
        // Одинаковые в пределах этажа — один раз: копия проёма на новом этаже даёт тот же шов, что и на этажах под ним.
        private static void AppendWallsKey(ref Hash128 key, SplineBuilding building, int storey, List<List<SplineWallHole>> wallHolesByPath)
        {
            var bottom = SplineBuildingMeshBuilder.WallsBottomOf(building, storey);
            var top = SplineBuildingMeshBuilder.WallsTopOf(building, storey);
            key.Append(storey == building.StoreyCount - 1 ? 1 : 0);
            key.Append(bottom);
            key.Append(top);

            foreach (var holes in wallHolesByPath)
            {
                var holesAsSeen = new List<SplineWallHole>(holes.Count);
                var seenHoles = new HashSet<(long, long, long, long)>();

                foreach (var hole in holes)
                {
                    var holeAsSeen = new SplineWallHole(hole.AlongFrom, hole.AlongTo, Mathf.Clamp(hole.HeightFrom, bottom, top), Mathf.Clamp(hole.HeightTo, bottom, top));

                    if (seenHoles.Add(QuantizedOf(holeAsSeen)))
                        holesAsSeen.Add(holeAsSeen);
                }

                key.Append(holesAsSeen.Count);

                foreach (var hole in holesAsSeen)
                {
                    LiveBuilderStableHash.AppendQuantized(ref key, hole.AlongFrom);
                    LiveBuilderStableHash.AppendQuantized(ref key, hole.AlongTo);
                    LiveBuilderStableHash.AppendQuantized(ref key, hole.HeightFrom);
                    LiveBuilderStableHash.AppendQuantized(ref key, hole.HeightTo);
                }
            }
        }

        private static (long, long, long, long) QuantizedOf(SplineWallHole hole)
        {
            return (LiveBuilderStableHash.Quantize(hole.AlongFrom), LiveBuilderStableHash.Quantize(hole.AlongTo),
                LiveBuilderStableHash.Quantize(hole.HeightFrom), LiveBuilderStableHash.Quantize(hole.HeightTo));
        }

        // Дыры — только те, что проходят через эту плиту: лестница этажом выше её не трогает.
        private static void AppendSlabKey(ref Hash128 key, SplineBuilding building, SplineBuildingPartKind kind, int storey, List<SplineWallPath> paths,
            List<SplineBuildingSlabHole> slabHoles)
        {
            foreach (var path in paths)
            {
                var (bottom, top) = SplineBuildingMeshBuilder.SlabHeightsOf(building, kind, storey, path.BaseHeightAt(0f));
                key.Append(bottom);
                key.Append(top);

                foreach (var hole in slabHoles)
                {
                    if (!SplineBuildingMeshBuilder.IsHoleThrough(hole, bottom, top))
                        continue;

                    foreach (var corner in hole.Corners)
                    {
                        LiveBuilderStableHash.AppendQuantized(ref key, corner.x);
                        LiveBuilderStableHash.AppendQuantized(ref key, corner.y);
                    }

                    LiveBuilderStableHash.AppendQuantized(ref key, hole.Bottom);
                    LiveBuilderStableHash.AppendQuantized(ref key, hole.Top);
                }
            }
        }
    }


    /// <summary>Что нужно, чтобы собрать части здания: пути контура, копии повтора с проёмами стен и дырами перекрытий и какие части должны быть — с подписями.</summary>
    public sealed class SplineBuildingPartLayout
    {
        public SplineBuildingPartLayout(List<SplineWallPath> paths, List<SplineWallCopy> copies, List<SplineBuildingPartKey> parts)
        {
            Paths = paths;
            Copies = copies;
            Parts = parts;
        }


        public List<SplineWallPath> Paths { get; }

        // Без повторов — одна, само здание.
        public List<SplineWallCopy> Copies { get; }

        public List<SplineBuildingPartKey> Parts { get; }
    }


    /// <summary>Часть здания, которая должна быть, и её подпись до вырезов.</summary>
    public readonly struct SplineBuildingPartKey
    {
        public SplineBuildingPartKey(SplineBuildingPartKind kind, int storey, string key)
        {
            Kind = kind;
            Storey = storey;
            Key = key;
        }


        public SplineBuildingPartKind Kind { get; }
        public int Storey { get; }
        public string Key { get; }
    }
}
