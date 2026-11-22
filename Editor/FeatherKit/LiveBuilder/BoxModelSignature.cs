using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Подпись» объекта из коробок — короткий хеш всего, от чего зависит его меш. Совпала с той, по которой собран меш, —
    /// пересобирать незачем (<see cref="BoxModelRebuilder"/>). Хеш одинаковый на любом запуске и машине (<see cref="LiveBuilderStableHash"/>).
    /// Отдельно — подпись геометрии: по ней решают, пересчитывать ли поверхности.
    /// У нового правила вырезов в обеих ещё и то, кто кого может резать (<see cref="LiveBuilderCutRules"/>): кусок и постройки над каждой коробкой.
    /// Текстура — только в полной (<see cref="LiveBuilderTextureSignature"/>); мировой ключ считается отдельно — он зависит от места объекта.
    /// </summary>
    public static class BoxModelSignature
    {
        // Поменял, что входит в подпись, — подними: все объекты пересоберутся один раз.
        private const int Version = 2;


        // Коробки расчёта по порядку, их место, режим, материалы и скосы, скаты, материал объекта, коллайдер, фаска.
        public static string Of(BoxModel model, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            var hash = new Hash128();
            hash.Append(Version);
            AppendCutRule(ref hash, model, boxes);
            hash.Append(BoxModelSideMaterials.KeyOf(model.DefaultMaterial));
            AppendTexture(ref hash, model, boxes);
            hash.Append(model.HasCollider ? 1 : 0);
            hash.Append(boxes.Count);

            foreach (var box in boxes)
            {
                var min = box.Box.Min;
                var max = box.Box.Max;
                hash.Append((int)box.Mode);
                hash.Append(ref min);
                hash.Append(ref max);
                hash.Append(box.Materials.Key);
                hash.Append(box.Chamfers.Count);

                foreach (var chamfer in box.Chamfers)
                {
                    hash.Append((int)chamfer.FirstFace);
                    hash.Append((int)chamfer.SecondFace);
                    hash.Append(chamfer.SizeOnFirstFace);
                    hash.Append(chamfer.SizeOnSecondFace);
                }
            }

            foreach (var ramp in ramps)
            {
                var low = MillimeterBox.ToMillimeters(ramp.Corners[0]);
                var high = MillimeterBox.ToMillimeters(ramp.Corners[6]);
                hash.Append(ref low);
                hash.Append(ref high);
            }

            if (BoxModelBevel.HasBevel(model))
            {
                hash.Append(model.BevelSize);
                hash.Append(model.BevelSegments);
            }

            return hash.ToString();
        }


        // Выравнивание World и плитки использованных материалов — только не по умолчанию; подгонки — в ключе материалов коробки.
        private static void AppendTexture(ref Hash128 hash, BoxModel model, List<BoxModelSolverBox> boxes)
        {
            LiveBuilderTextureSignature.AppendAlignment(ref hash, model.TextureAlignment);
            if (!LiveBuilderMaterialTiling.HasAnySize)
                return;

            LiveBuilderTextureSignature.AppendTile(ref hash, model.DefaultMaterial);
            var seen = new HashSet<Material> { model.DefaultMaterial };

            foreach (var box in boxes)
                for (var face = 0; face < BoxFaces.Count; face++)
                {
                    var material = box.Materials.ForSide(face);

                    if (material != null && seen.Add(material))
                        LiveBuilderTextureSignature.AppendTile(ref hash, material);
                }
        }


        // Мировой ключ объекта (World): сдвиг по трём осям по модулю всех периодов рисунка его граней. Object — пусто.
        public static string WorldKeyOf(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes)
        {
            var space = LiveBuilderTextureSpace.Of(model.transform, model.TextureAlignment);

            if (!space.IsWorld)
                return "";

            var periods = new LiveBuilderTexturePeriods();
            periods.Add(null, model.DefaultMaterial);

            foreach (var box in boxes)
                for (var face = 0; face < BoxFaces.Count; face++)
                {
                    var material = box.MaterialForSurface(face);
                    periods.Add(box.FitForSurface(face), material != null ? material : model.DefaultMaterial);
                }

            return LiveBuilderTextureSignature.WorldKeyOf(space, periods, LiveBuilderTextureSignature.AxisShiftsOf(space));
        }


        // Только места и режимы коробок по порядку — от них зависят поверхности; материалы, скосы и фаска их не меняют.
        public static Hash128 GeometryOf(BoxModel model, List<BoxModelSolverBox> boxes)
        {
            var hash = new Hash128();
            AppendCutRule(ref hash, model, boxes);
            hash.Append(boxes.Count);

            foreach (var box in boxes)
            {
                var min = box.Box.Min;
                var max = box.Box.Max;
                hash.Append((int)box.Mode);
                hash.Append(ref min);
                hash.Append(ref max);
            }

            return hash;
        }


        // Новое правило: кусок и постройки над каждой коробкой — номерами по порядку появления, они одинаковы на любом запуске.
        // Старое ничего не добавляет: подписи старых объектов те же.
        private static void AppendCutRule(ref Hash128 hash, BoxModel model, List<BoxModelSolverBox> boxes)
        {
            if (LiveBuilderCutRules.IsLegacy(model))
                return;

            hash.Append(BoxModel.CurrentDataVersion);
            var constructionNumbers = new Dictionary<Transform, int>();

            foreach (var box in boxes)
            {
                hash.Append(box.PieceKey);
                hash.Append(box.ConstructionsAbove.Count);

                foreach (var construction in box.ConstructionsAbove)
                {
                    if (!constructionNumbers.TryGetValue(construction, out var number))
                        constructionNumbers[construction] = number = constructionNumbers.Count;

                    hash.Append(number);
                }
            }
        }
    }
}
