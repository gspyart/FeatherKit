using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Подпись» стены по линии — хеш всего, от чего зависит её меш, одинаковый на любом запуске и машине (<see cref="LiveBuilderStableHash"/>).
    /// Совпала с той, по которой собран меш, — пересобирать незачем (<see cref="SplineWallRebuilder"/>).
    /// Сюда же — вырезающие коробки объекта вокруг, которые режут стену или здание: ими подписывается и здание.
    /// Текстура — через <see cref="LiveBuilderTextureSignature"/>: подгонки, World и плитки до вырезов, мировой ключ — перед вырезами.
    /// </summary>
    public static class SplineWallSignature
    {
        // Версия расчёта стены: поменялась — все стены пересобираются. 2 — стороны на отрезке строго поперёк, откосы прямые;
        // 4 — коробка во всю высоту стены её не режет; 5 — развёртка верха и низа по плану.
        private const int GeometryVersion = 5;

        // Запас габарита стены или здания для поиска вырезающих коробок, м: коробка, вплотную касающаяся стены, тоже в подписи.
        private const float OwnerBoundsMargin = 0.05f;


        // Всё, от чего зависит меш стены, — хешем, одинаковым на любом запуске и машине (<see cref="LiveBuilderStableHash"/>); в конце — вырезы.
        public static string Of(SplineWall wall, List<GeometryCutter> cutters, LiveBuilderBuildQuality quality = LiveBuilderBuildQuality.Final)
        {
            var worldKey = LiveBuilderTextureSignature.WorldKeyFor(quality, wall.BuiltSignature, () => WorldKeyOf(wall));
            return UncutOf(wall) + worldKey + GeometryCutterLookup.KeyOf(wall, cutters);
        }


        // Мировой ключ стены (World): сдвиги её осей и начал путей по модулю периодов рисунка; Object — пусто.
        public static string WorldKeyOf(SplineWall wall)
        {
            var space = LiveBuilderTextureSpace.Of(wall.transform, wall.TextureAlignment);

            if (!space.IsWorld)
                return "";

            var periods = new LiveBuilderTexturePeriods();
            var material = wall.Material;
            AddPeriods(periods, wall.TextureFits, material != null ? material : BoxModelFactory.LoadDefaultMaterial());

            if (wall.HasSlabs)
            {
                AddPeriods(periods, wall.TextureFits, wall.FloorMaterial);
                AddPeriods(periods, wall.TextureFits, wall.CeilingMaterial);
            }

            return LiveBuilderTextureSignature.WorldKeyOf(space, periods, WorldShiftsOf(space, SplineWallPath.FromWall(wall)));
        }


        // Каждая подгонка постройки и «как есть» — на этом материале.
        public static void AddPeriods<TPlace>(LiveBuilderTexturePeriods periods, LiveBuilderTextureFitList<TPlace> fits, Material material)
            where TPlace : struct, System.Enum
        {
            periods.Add(null, material);

            foreach (var entry in fits.Entries)
                periods.Add(entry.Fit, material);
        }


        // Сдвиги осей постройки и направлений всех отрезков до мира: сторона может начаться с любого отрезка (путь режут стены здания).
        // Свой отсчёт стороны от места не зависит — его в ключе нет (<see cref="SplineWallTextureAxes"/>).
        public static List<float> WorldShiftsOf(LiveBuilderTextureSpace space, IReadOnlyList<SplineWallPath> paths)
        {
            var shifts = new List<float>(LiveBuilderTextureSignature.AxisShiftsOf(space));

            foreach (var path in paths)
                for (var segment = 0; segment < path.SegmentCount; segment++)
                    shifts.Add(space.ShiftAlong(path.DirectionOf(segment)));

            return shifts;
        }


        // До вырезов: по ней же узнаётся заготовка жеста с проёмом (<see cref="GeometryCutterDragBlanks"/>).
        public static string UncutOf(SplineWall wall)
        {
            var hash = new Hash128();
            hash.Append(GeometryVersion);
            hash.Append(wall.Thickness);
            hash.Append(wall.Height);
            hash.Append((int)wall.Alignment);
            LiveBuilderArrayCopyPoses.AppendKey(ref hash, wall.transform, wall.transform);
            hash.Append(wall.CurveStep);
            hash.Append(wall.CornerSegments);
            hash.Append(BoxModelSideMaterials.KeyOf(wall.Material));
            AppendTexture(ref hash, wall);
            hash.Append(wall.HasCollider ? 1 : 0);
            SplineWallBuildingSides.AppendBuildingKey(ref hash, wall);
            LiveBuilderStableHash.AppendSplines(ref hash, wall.GetComponent<SplineContainer>());

            // Пол и потолок — только если есть: у прежних стен подпись та же, пересборки нет.
            if (wall.HasSlabs)
            {
                hash.Append(wall.HasFloor ? 1 : 0);
                hash.Append(wall.HasCeiling ? 1 : 0);
                hash.Append(wall.SlabThickness);
                hash.Append(BoxModelSideMaterials.KeyOf(wall.FloorMaterial));
                hash.Append(BoxModelSideMaterials.KeyOf(wall.CeilingMaterial));
            }

            // Двери-дети, что режут стену проёмом по линии (<see cref="LiveBuilderCutRules.IsWallChildBox"/>), — коробками в осях стены.
            foreach (var cutter in LiveBuilderCutRules.PreciseChildCuttersOf(wall))
                foreach (var box in GeometryCutterSolverBoxes.BoxesOf(cutter, wall.transform, Vector3.zero))
                {
                    AppendQuantized(ref hash, box.MinInMeters);
                    AppendQuantized(ref hash, box.MaxInMeters);
                }

            var model = SplineWallRebuilder.OuterModelOf(wall);

            // Коробки — уже в осях стены: где стоит сама стена в объекте, подписи не важно. С полом и потолком — и те, что режут перекрытия.
            if (model != null)
            {
                var cutBoxes = CutBoxesHashOf(model, wall, wall.HasSlabs);
                hash.Append(ref cutBoxes);
            }

            return hash.ToString();
        }


        // Выравнивание World, подгонки и плитки материалов стены — только не по умолчанию: подписи старых стен прежние.
        private static void AppendTexture(ref Hash128 hash, SplineWall wall)
        {
            LiveBuilderTextureSignature.AppendAlignment(ref hash, wall.TextureAlignment);
            LiveBuilderTextureSignature.AppendFits(ref hash, wall.TextureFits);

            if (!LiveBuilderMaterialTiling.HasAnySize)
                return;

            LiveBuilderTextureSignature.AppendTile(ref hash, wall.Material);
            LiveBuilderTextureSignature.AppendTile(ref hash, wall.FloorMaterial);
            LiveBuilderTextureSignature.AppendTile(ref hash, wall.CeilingMaterial);
        }


        // Только задевающие габарит, углами в осях стены или здания с округлением до 0,1 мм: правка добавляющих коробок подпись не меняет,
        // а здание, которое едет вместе со своей лестницей, не пересобирается.
        public static Hash128 CutBoxesHashOf(BoxModel model, Component owner, bool includesSlabCuts)
        {
            var hash = new Hash128();
            var modelToOwner = LiveBuilderStableHash.RelativeMatrix(owner.transform, model.transform);
            var ownerBounds = WithArrayCopies(owner, UncutLocalBoundsOf(owner));

            // Новое правило режет стены не так, как старое: та же коробка может дать другой меш.
            if (!LiveBuilderCutRules.IsLegacy(model))
                hash.Append(BoxModel.CurrentDataVersion);

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                var cut = box.CutSource;
                var isApplied = box.Mode == BoxModelPartMode.Cut && (cut.CutsWalls || includesSlabCuts && cut.CutsSlabs);

                if (!isApplied || !LiveBuilderCutRules.CutBoxCutsConstruction(model, box, owner))
                    continue;

                var min = modelToOwner.MultiplyPoint3x4(box.Box.MinInMeters);
                var max = modelToOwner.MultiplyPoint3x4(box.Box.MaxInMeters);
                var boxBounds = new Bounds(min, Vector3.zero);
                boxBounds.Encapsulate(max);

                // Коробка по осям объекта, а стена может стоять в нём повёрнутой: габарит — по всем восьми углам.
                foreach (var corner in CornersOf(box.Box.MinInMeters, box.Box.MaxInMeters))
                    boxBounds.Encapsulate(modelToOwner.MultiplyPoint3x4(corner));

                if (!boxBounds.Intersects(ownerBounds))
                    continue;

                AppendQuantized(ref hash, min);
                AppendQuantized(ref hash, max);

                if ((box.OwnerPart != null || box.Owner is GeometryCutter) && cut.Targets != LiveBuilderCutTargets.Everything)
                    hash.Append((int)cut.Targets);
            }

            return hash;
        }

        private static void AppendQuantized(ref Hash128 hash, Vector3 point)
        {
            LiveBuilderStableHash.AppendQuantized(ref hash, point.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, point.y);
            LiveBuilderStableHash.AppendQuantized(ref hash, point.z);
        }

        private static Vector3[] CornersOf(Vector3 min, Vector3 max)
        {
            var corners = new Vector3[8];

            for (var index = 0; index < 8; index++)
                corners[index] = new Vector3((index & 1) == 0 ? min.x : max.x, (index & 2) == 0 ? min.y : max.y, (index & 4) == 0 ? min.z : max.z);

            return corners;
        }

        // Габарит со всеми копиями повтора, в осях оригинала: коробка, что режет хоть одну копию, — в подписи.
        private static Bounds WithArrayCopies(Component owner, Bounds originalBounds)
        {
            if (!LiveBuilderArrayCopyPoses.HasArrayCopies(owner.transform))
                return originalBounds;

            var bounds = new Bounds(originalBounds.center, Vector3.zero);

            foreach (var copy in LiveBuilderArrayCopyPoses.CopiesOf(owner.transform))
                bounds.Encapsulate(new Bounds(originalBounds.center + LiveBuilderArrayCopyPoses.LocalOffsetOf(owner.transform, copy), originalBounds.size));

            return bounds;
        }

        // Где может быть стена или здание до вырезов, в их осях, — по точкам линии с усами, с запасом на толщину стен; по высоте — от низа линии
        // до верха стены или до крыши здания. Считается из чисел, а не из меша: подпись не зависит от того, собран ли он.
        private static Bounds UncutLocalBoundsOf(Component owner)
        {
            var container = owner.GetComponent<SplineContainer>();
            var bounds = new Bounds();
            var hasPoints = false;

            foreach (var spline in container.Splines)
            {
                foreach (var knot in spline.Knots)
                {
                    Vector3 position = knot.Position;
                    Quaternion rotation = knot.Rotation;

                    foreach (var point in new[] { position, position + rotation * (Vector3)knot.TangentIn, position + rotation * (Vector3)knot.TangentOut })
                    {
                        if (hasPoints)
                            bounds.Encapsulate(point);
                        else
                            bounds = new Bounds(point, Vector3.zero);

                        hasPoints = true;
                    }
                }
            }

            var isBuilding = owner is SplineBuilding;
            var thickness = isBuilding ? ((SplineBuilding)owner).WallThickness : ((SplineWall)owner).Thickness;
            var top = isBuilding
                ? ((SplineBuilding)owner).TotalHeight + ((SplineBuilding)owner).ParapetRise
                : ((SplineWall)owner).Height;
            var margin = thickness + OwnerBoundsMargin;
            var min = bounds.min - new Vector3(margin, OwnerBoundsMargin, margin);
            var max = bounds.max + new Vector3(margin, top + OwnerBoundsMargin, margin);
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
