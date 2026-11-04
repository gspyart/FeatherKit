using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Подпись» объекта из коробок — короткий хеш всего, от чего зависит его меш. Совпала с той, по которой собран меш, —
    /// пересобирать незачем (<see cref="BoxModelRebuilder"/>). Хеш одинаковый на любом запуске и машине (<see cref="LiveBuilderStableHash"/>).
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
            hash.Append(BoxModelSideMaterials.KeyOf(model.DefaultMaterial));
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
    }
}
