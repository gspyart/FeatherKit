using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Дыры в перекрытиях здания или замкнутой стены с полом и потолком (<see cref="SplineBuildingSlabHole"/>) от вырез-коробок объекта вокруг:
    /// прямоугольник в плане владельца и высоты над его линией. Кто режет перекрытия — решает правило (<see cref="LiveBuilderCutRules"/>);
    /// вырез, который режет их не точно, а вычитанием, дыры не даёт.
    /// </summary>
    public static class SplineBuildingSlabHoles
    {
        // Вырезающие коробки объекта вокруг здания или замкнутой стены с полом и потолком, что режут перекрытия (<see cref="LiveBuilderCutRules"/>):
        // прямоугольник в плане владельца и высоты над его линией.
        public static List<SplineBuildingSlabHole> Collect(Component owner)
        {
            return Collect(owner, Vector3.zero);
        }


        // Дыры копии постройки, сдвинутой повтором на copyOffset в мире.
        public static List<SplineBuildingSlabHole> Collect(Component owner, Vector3 copyOffset)
        {
            var holes = new List<SplineBuildingSlabHole>();
            var model = SplineWallRebuilder.OuterModelOf(owner);

            if (model == null)
                return holes;

            var modelToBuilding = LiveBuilderArrayCopyPoses.WorldToLocalOfCopy(owner.transform, copyOffset) * model.transform.localToWorldMatrix;

            Dictionary<GeometryCutter, bool> isPreciseByCutter = null;

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (box.Mode != BoxModelPartMode.Cut || !box.CutSource.CutsSlabs || !LiveBuilderCutRules.CutBoxCutsConstruction(model, box, owner))
                    continue;

                if (box.Owner is GeometryCutter cutter && !IsCutterPrecise(cutter, model, owner, ref isPreciseByCutter))
                    continue;

                var min = box.Box.MinInMeters;
                var max = box.Box.MaxInMeters;
                var corners = new[]
                {
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)))
                };

                var bottom = modelToBuilding.MultiplyPoint3x4(min).y;
                var top = modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)).y;
                holes.Add(new SplineBuildingSlabHole(corners, bottom, top));
            }

            return holes;
        }

        // Вырез-коробка без точной дыры (свои материалы стенок, у стены — ниша) режет перекрытия вычитанием. Решается раз на вырез.
        private static bool IsCutterPrecise(GeometryCutter cutter, BoxModel model, Component owner, ref Dictionary<GeometryCutter, bool> isPreciseByCutter)
        {
            isPreciseByCutter ??= new Dictionary<GeometryCutter, bool>();

            if (!isPreciseByCutter.TryGetValue(cutter, out var isPrecise))
                isPreciseByCutter[cutter] = isPrecise = LiveBuilderCutRules.CutsSlabsPrecisely(cutter, model, owner);

            return isPrecise;
        }

        private static Vector2 Flat(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }
    }
}
