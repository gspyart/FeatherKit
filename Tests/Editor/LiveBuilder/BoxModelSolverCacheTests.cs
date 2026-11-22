using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Солвер по коробкам (<see cref="BoxModelSolverCache"/>) и поверхности по плоскостям (<see cref="BoxModelSurfaceCache"/>) отвечают
    /// байт в байт как расчёт заново (<see cref="BoxModelSolver"/>, <see cref="BoxModelSurfaceFinder"/>) на случайных правках,
    /// пересчитывают только задетое, а правило «сначала добавления» не зависит от места выреза в списке.
    /// </summary>
    public class BoxModelSolverCacheTests
    {
        private const int RandomSeed = 20261120;
        private const int RandomEditCount = 400;
        private const int StartBoxCount = 40;
        private const int GridStep = 100;
        private const int GridCells = 24;


        // ── Совпадение с расчётом заново ─────────────────────────────────

        [Test]
        public void RandomEdits_MatchFullSolve_BothRules()
        {
            var random = new System.Random(RandomSeed);
            var parts = new List<BoxModelPartShape>();

            for (var index = 0; index < StartBoxCount; index++)
                parts.Add(RandomPart(random));

            var solverCache = new BoxModelSolverCache();
            var surfaceCache = new BoxModelSurfaceCache();

            for (var edit = 0; edit < RandomEditCount; edit++)
            {
                ApplyRandomEdit(random, parts);
                var rule = edit % 5 == 4 ? BoxModelCutRule.CutsAfterAllAdds : BoxModelCutRule.LaterBoxesCut;
                var expectedCells = BoxModelSolver.Solve(parts, rule);
                var cells = solverCache.Solve(parts, rule);
                AssertSameCells(expectedCells, cells, "правка " + edit);
                AssertSameSurfaces(BoxModelSurfaceFinder.FindOuterSurfaces(expectedCells), surfaceCache.FindOuterSurfaces(cells), "правка " + edit);
            }
        }


        [Test]
        public void PreviewSolve_DoesNotChangeMemory()
        {
            var random = new System.Random(RandomSeed);
            var parts = new List<BoxModelPartShape>();

            for (var index = 0; index < StartBoxCount; index++)
                parts.Add(RandomPart(random));

            var solverCache = new BoxModelSolverCache();
            var surfaceCache = new BoxModelSurfaceCache();
            surfaceCache.FindOuterSurfaces(solverCache.Solve(parts, BoxModelCutRule.LaterBoxesCut));

            var withPreview = new List<BoxModelPartShape>(parts) { RandomPart(random), RandomPart(random) };
            var previewCells = solverCache.SolveWithoutStoring(withPreview, BoxModelCutRule.LaterBoxesCut);
            AssertSameCells(BoxModelSolver.Solve(withPreview), previewCells, "предпросмотр");
            AssertSameSurfaces(BoxModelSurfaceFinder.FindOuterSurfaces(previewCells), surfaceCache.FindOuterSurfacesWithoutStoring(previewCells), "предпросмотр");

            surfaceCache.FindOuterSurfaces(solverCache.Solve(parts, BoxModelCutRule.LaterBoxesCut));
            Assert.AreEqual(0, solverCache.RecomputedBoxCount, "после предпросмотра всё из памяти");
            Assert.AreEqual(0, surfaceCache.RecomputedSliceCount, "плоскости — тоже");
        }


        // ── Пересчёт только задетого ─────────────────────────────────────

        [Test]
        public void MovingOneOf300_RecomputesOnlyIntersecting()
        {
            var parts = ThreeHundredBoxes();
            var solverCache = new BoxModelSolverCache();
            solverCache.Solve(parts, BoxModelCutRule.LaterBoxesCut);

            const int movedIndex = 152;
            var before = parts[movedIndex].Box;
            var after = new MillimeterBox(before.Min + new Vector3Int(GridStep, 0, 0), before.Max + new Vector3Int(GridStep, 0, 0));
            parts[movedIndex] = new BoxModelPartShape(after, BoxModelPartMode.Add);

            var cells = solverCache.Solve(parts, BoxModelCutRule.LaterBoxesCut);
            AssertSameCells(BoxModelSolver.Solve(parts), cells, "после сдвига");

            var expected = 1;

            for (var index = 0; index < movedIndex; index++)
                if (parts[index].Box.Overlaps(before) || parts[index].Box.Overlaps(after))
                    expected++;

            Assert.Greater(expected, 1, "сдвинутая коробка задевает соседей");
            Assert.AreEqual(expected, solverCache.RecomputedBoxCount, "заново — только она и те, кого она режет до и после сдвига");
        }

        // Ряды колонн и плит поверх них: каждая плита пересекает несколько колонн.
        private static List<BoxModelPartShape> ThreeHundredBoxes()
        {
            var parts = new List<BoxModelPartShape>();

            for (var index = 0; index < 300; index++)
            {
                var row = index / 30;
                var column = index % 30;
                var isSlab = column % 3 == 2;
                var min = new Vector3Int(column * 300, isSlab ? 900 : 0, row * 1000);
                var max = isSlab ? min + new Vector3Int(700, 200, 400) : min + new Vector3Int(200, 1000, 200);

                if (isSlab)
                    min -= new Vector3Int(500, 0, 0);

                parts.Add(new BoxModelPartShape(new MillimeterBox(min, max), BoxModelPartMode.Add));
            }

            return parts;
        }


        // ── Объект на сцене ──────────────────────────────────────────────

        [Test]
        public void YardColumnDrag_DraftSurfaces_MatchFullSolve()
        {
            using var testScene = new LiveBuilderTestScene();
            var root = new GameObject("Yard Root").transform;
            var yard = LiveBuilderReferenceYard.Build(root, LiveBuilderTestScene.Origin, out var column);
            LiveBuilderRebuildQueue.RunFinalPassNow();

            using var gesture = LiveBuilderGestureTracker.BeginExplicitGesture();

            for (var step = 0; step < 4; step++)
            {
                column.transform.position += Vector3.right * (step < 2 ? 0.7f : -0.7f);
                LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);

                var shapes = new List<BoxModelPartShape>();

                foreach (var box in BoxModelRebuilder.GetSolverBoxes(yard))
                    shapes.Add(box.Shape);

                var expected = BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(shapes));
                AssertSameSurfaces(expected, new List<BoxModelSurface>(BoxModelRebuilder.GetSurfaces(yard)), "шаг " + step);
            }
        }


        [Test]
        public void MaterialChange_KeepsSurfaces()
        {
            using var testScene = new LiveBuilderTestScene();
            var root = new GameObject("Yard Root").transform;
            var yard = LiveBuilderReferenceYard.Build(root, LiveBuilderTestScene.Origin, out var column);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var surfaces = BoxModelRebuilder.GetSurfaces(yard);

            column.Material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            BoxModelRebuilder.ForgetCachedResults();

            Assert.AreSame(surfaces, BoxModelRebuilder.GetSurfaces(yard), "геометрия та же — поверхности не пересчитаны");
            Object.DestroyImmediate(column.Material);
        }


        // ── Правило «сначала добавления» ─────────────────────────────────

        [Test]
        public void CutsAfterAllAdds_DoesNotDependOnCutPlace()
        {
            var random = new System.Random(RandomSeed + 1);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var adds = new List<BoxModelPartShape>();
                var cuts = new List<BoxModelPartShape>();

                for (var index = 0; index < 30; index++)
                {
                    var part = RandomPart(random);
                    (part.Mode == BoxModelPartMode.Add ? adds : cuts).Add(part);
                }

                var first = Interleave(random, adds, cuts, out var firstIds);
                var second = Interleave(random, adds, cuts, out var secondIds);
                var firstCells = new BoxModelSolverCache().Solve(first, BoxModelCutRule.CutsAfterAllAdds);
                var secondCells = new BoxModelSolverCache().Solve(second, BoxModelCutRule.CutsAfterAllAdds);

                AssertSameCells(WithIds(firstCells, firstIds), WithIds(secondCells, secondIds), "попытка " + attempt);
            }
        }

        // Добавляющие и вырезы в своём порядке, но вперемешку друг с другом. ids — какая это коробка: добавляющие 0.., вырезы 1000..
        private static List<BoxModelPartShape> Interleave(System.Random random, List<BoxModelPartShape> adds, List<BoxModelPartShape> cuts, out List<int> ids)
        {
            var parts = new List<BoxModelPartShape>();
            ids = new List<int>();
            var addIndex = 0;
            var cutIndex = 0;

            while (addIndex < adds.Count || cutIndex < cuts.Count)
            {
                var takesAdd = cutIndex >= cuts.Count || addIndex < adds.Count && random.Next(2) == 0;
                parts.Add(takesAdd ? adds[addIndex] : cuts[cutIndex]);
                ids.Add(takesAdd ? addIndex++ : 1000 + cutIndex++);
            }

            return parts;
        }

        private static List<BoxModelCell> WithIds(List<BoxModelCell> cells, List<int> ids)
        {
            return cells.ConvertAll(cell => cell.WithSources(ids));
        }


        // ── Случайные коробки и правки ───────────────────────────────────

        private static BoxModelPartShape RandomPart(System.Random random)
        {
            var min = new Vector3Int(random.Next(GridCells), random.Next(GridCells / 2), random.Next(GridCells)) * GridStep;
            var size = new Vector3Int(random.Next(1, 8), random.Next(1, 6), random.Next(1, 8)) * GridStep;
            var mode = random.Next(4) == 0 ? BoxModelPartMode.Cut : BoxModelPartMode.Add;
            return new BoxModelPartShape(new MillimeterBox(min, min + size), mode);
        }


        // Сдвиг, размер, вставка, удаление, перестановка, смена режима; изредка — пустая коробка.
        private static void ApplyRandomEdit(System.Random random, List<BoxModelPartShape> parts)
        {
            var index = random.Next(parts.Count);
            var part = parts[index];

            switch (random.Next(7))
            {
                case 0:
                    var offset = new Vector3Int(random.Next(-2, 3), random.Next(-2, 3), random.Next(-2, 3)) * GridStep;
                    parts[index] = new BoxModelPartShape(new MillimeterBox(part.Box.Min + offset, part.Box.Max + offset), part.Mode);
                    break;
                case 1:
                    var grow = new Vector3Int(random.Next(-1, 2), random.Next(-1, 2), random.Next(-1, 2)) * GridStep;
                    parts[index] = new BoxModelPartShape(new MillimeterBox(part.Box.Min, part.Box.Max + grow), part.Mode);
                    break;
                case 2:
                    parts.Insert(random.Next(parts.Count + 1), RandomPart(random));
                    break;
                case 3:
                    if (parts.Count > 2)
                        parts.RemoveAt(index);
                    break;
                case 4:
                    var other = random.Next(parts.Count);
                    (parts[index], parts[other]) = (parts[other], parts[index]);
                    break;
                case 5:
                    var mode = part.Mode == BoxModelPartMode.Add ? BoxModelPartMode.Cut : BoxModelPartMode.Add;
                    parts[index] = new BoxModelPartShape(part.Box, mode);
                    break;
                default:
                    parts[index] = new BoxModelPartShape(new MillimeterBox(part.Box.Min, part.Box.Min + new Vector3Int(GridStep, 0, GridStep)), part.Mode);
                    break;
            }
        }


        // ── Сверка ───────────────────────────────────────────────────────

        private static void AssertSameCells(List<BoxModelCell> expected, List<BoxModelCell> actual, string where)
        {
            Assert.AreEqual(expected.Count, actual.Count, where + ": число кусков");

            for (var index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Box.Min, actual[index].Box.Min, where + ": кусок " + index);
                Assert.AreEqual(expected[index].Box.Max, actual[index].Box.Max, where + ": кусок " + index);
                CollectionAssert.AreEqual(expected[index].FaceSources, actual[index].FaceSources, where + ": грани куска " + index);
            }
        }


        private static void AssertSameSurfaces(List<BoxModelSurface> expected, List<BoxModelSurface> actual, string where)
        {
            Assert.AreEqual(expected.Count, actual.Count, where + ": число поверхностей");

            for (var index = 0; index < expected.Count; index++)
            {
                var message = where + ": поверхность " + index;
                Assert.AreEqual(expected[index].Face, actual[index].Face, message);
                Assert.AreEqual(expected[index].Plane, actual[index].Plane, message);
                Assert.AreEqual(expected[index].Area, actual[index].Area, message);
                Assert.AreEqual(expected[index].SourcePart, actual[index].SourcePart, message);
            }
        }
    }
}
