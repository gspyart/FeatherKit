using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Тело объекта из коробок «как стало» после обновления — для сверки с телом «как было» клетка в клетку.
    /// - сам объект по новому правилу плюс его вложенные «Kept From Cuts» и копии развёрнутого повтора, которые добавило обновление, — в его осях
    /// - из вложенного вынуто то, что вырезы-коробки режут в нём вычитанием (вырез «Everything it touches» его объекта)
    /// - сверка точная: объём до, после и общий — в миллиметрах
    /// </summary>
    public static class LiveBuilderUpgradeBody
    {
        // Расхождение объёма меньше — то же самое: доля и м³.
        private const double RelativeVolumeTolerance = 1e-4;
        private const double AbsoluteVolumeTolerance = 1e-4;
        private const double CubicMillimetersPerCubicMeter = 1e9;


        // Чем тело сейчас расходится с телом «как было»; совпадает — null.
        public static string DifferenceFrom(BoxModel model, List<BoxModelCell> before, IReadOnlyList<BoxModel> unrolledCopies)
        {
            var after = SolvedCellsOf(model);

            foreach (var copy in unrolledCopies)
                if (copy != null)
                    after = Union(after, InFrameOf(model, copy, SolvedCellsOf(copy)));

            foreach (var kept in KeptFromCutsOf(model))
                after = Union(after, InFrameOf(model, kept, WithoutSubtractedCutters(kept, SolvedCellsOf(kept))));

            var beforeVolume = VolumeOf(before);
            var afterVolume = VolumeOf(after);
            var commonVolume = IntersectionVolumeOf(before, after);

            if (IsSameVolume(beforeVolume, afterVolume) && IsSameVolume(beforeVolume, commonVolume))
                return null;

            return "box body differs from before: " + Format(beforeVolume) + " → " + Format(afterVolume) + " m³, " +
                   Format(beforeVolume + afterVolume - 2.0 * commonVolume) + " m³ not in both";
        }


        // ── Тело ─────────────────────────────────────────────────────────

        // Вложенные «Kept From Cuts» объекта — где бы в нём ни лежали: в группе, в этаже здания.
        internal static List<BoxModel> KeptFromCutsOf(BoxModel model)
        {
            var kept = new List<BoxModel>();

            foreach (var nested in model.GetComponentsInChildren<BoxModel>(true))
                if (nested != model && nested.name == LiveBuilderKeptFromCuts.GroupName && nested.transform.parent != null
                    && LiveBuilderGroups.GroupOf(nested.transform.parent) == model)
                    kept.Add(nested);

            return kept;
        }

        private static List<BoxModelCell> SolvedCellsOf(BoxModel model)
        {
            var boxes = BoxModelShapeCollector.Collect(model, new List<BoxModelRampShape>());
            return BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape), LiveBuilderCutRules.RuleOf(model), LiveBuilderCutRules.SolverFilterOf(model, boxes));
        }

        // Вырезы-коробки, что режут вложенный объект вычитанием, — вынуть из его клеток. Вырез другой формы так не вынуть — тело сверяется без него.
        private static List<BoxModelCell> WithoutSubtractedCutters(BoxModel kept, List<BoxModelCell> cells)
        {
            foreach (var root in kept.gameObject.scene.GetRootGameObjects())
                foreach (var cutter in root.GetComponentsInChildren<GeometryCutter>())
                {
                    if (LiveBuilderCutRules.MethodOf(cutter, kept) != LiveBuilderCutMethod.MeshSubtraction || !GeometryCutterSolverBoxes.IsBoxAlongAxesOf(cutter, kept))
                        continue;

                    foreach (var copy in LiveBuilderArrayCopyPoses.CutterCopiesOf(cutter))
                        foreach (var box in GeometryCutterSolverBoxes.BoxesOf(cutter, kept, kept.transform.InverseTransformVector(copy)))
                        {
                            var remaining = new List<BoxModelCell>(cells.Count);

                            foreach (var cell in cells)
                                BoxModelSolver.KeepOutside(cell, box, 0, remaining);

                            cells = remaining;
                        }
                }

            return cells;
        }


        // ── Клетки ───────────────────────────────────────────────────────

        // Клетки вложенного объекта — в осях внешнего; оси совпадают — без пересчёта, иначе по углам (оси повёрнуты на 90°).
        private static List<BoxModelCell> InFrameOf(BoxModel outer, BoxModel nested, List<BoxModelCell> cells)
        {
            var nestedToOuter = outer.transform.worldToLocalMatrix * nested.transform.localToWorldMatrix;

            if (IsIdentity(nestedToOuter))
                return cells;

            var moved = new List<BoxModelCell>(cells.Count);

            foreach (var cell in cells)
            {
                var first = MillimeterBox.ToMillimeters(nestedToOuter.MultiplyPoint3x4(cell.Box.MinInMeters));
                var second = MillimeterBox.ToMillimeters(nestedToOuter.MultiplyPoint3x4(cell.Box.MaxInMeters));
                moved.Add(new BoxModelCell(new MillimeterBox(Vector3Int.Min(first, second), Vector3Int.Max(first, second)), 0));
            }

            return moved;
        }

        private static bool IsIdentity(Matrix4x4 matrix)
        {
            for (var index = 0; index < 16; index++)
                if (Mathf.Abs(matrix[index] - Matrix4x4.identity[index]) > 1e-5f)
                    return false;

            return true;
        }

        // Клетки каждого списка не пересекаются: от добавленных остаётся то, что снаружи первых.
        private static List<BoxModelCell> Union(List<BoxModelCell> first, List<BoxModelCell> second)
        {
            var union = new List<BoxModelCell>(first);

            foreach (var cell in second)
            {
                var pieces = new List<BoxModelCell> { cell };

                foreach (var existing in first)
                {
                    var remaining = new List<BoxModelCell>(pieces.Count);

                    foreach (var piece in pieces)
                        BoxModelSolver.KeepOutside(piece, existing.Box, 0, remaining);

                    pieces = remaining;
                }

                union.AddRange(pieces);
            }

            return union;
        }


        // ── Объём ────────────────────────────────────────────────────────

        private static double VolumeOf(List<BoxModelCell> cells)
        {
            var volume = 0.0;

            foreach (var cell in cells)
                volume += VolumeOf(cell.Box.Min, cell.Box.Max);

            return volume;
        }

        // Оба списка из непересекающихся клеток — общий объём складывается попарно.
        private static double IntersectionVolumeOf(List<BoxModelCell> first, List<BoxModelCell> second)
        {
            var volume = 0.0;

            foreach (var a in first)
                foreach (var b in second)
                    if (a.Box.Overlaps(b.Box))
                        volume += VolumeOf(Vector3Int.Max(a.Box.Min, b.Box.Min), Vector3Int.Min(a.Box.Max, b.Box.Max));

            return volume;
        }

        private static double VolumeOf(Vector3Int min, Vector3Int max)
        {
            return (double)(max.x - min.x) * (max.y - min.y) * (max.z - min.z) / CubicMillimetersPerCubicMeter;
        }

        private static bool IsSameVolume(double first, double second)
        {
            return System.Math.Abs(first - second) <= AbsoluteVolumeTolerance + RelativeVolumeTolerance * System.Math.Max(System.Math.Abs(first), System.Math.Abs(second));
        }

        private static string Format(double volume)
        {
            return volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
