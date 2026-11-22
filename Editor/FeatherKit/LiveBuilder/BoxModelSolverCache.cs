using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Солвер одного объекта из коробок с памятью по коробкам: ответ тот же, что у <see cref="BoxModelSolver"/>, байт в байт и по порядку.
    /// - куски коробки зависят только от неё и её резчиков (<see cref="BoxModelCutterSearch"/>) — по ним ключ, по ключу кеш
    /// - номера граней в кеше свои: 0 — сама коробка, k — k-й резчик; при сборке — номера в списке
    /// - помнит два прохода: что не спросили два прохода подряд, забывается
    /// </summary>
    public sealed class BoxModelSolverCache
    {
        private Dictionary<Hash128, BoxModelSolvedBox> currentBoxes = new();
        private Dictionary<Hash128, BoxModelSolvedBox> previousBoxes = new();

        // Ключ коробки по ходу расчёта: её углы, число резчиков и их углы.
        private readonly List<int> keyNumbers = new();


        // Сколько коробок последний расчёт считал заново, а не взял из памяти.
        public int RecomputedBoxCount { get; private set; }


        // ── Расчёт ───────────────────────────────────────────────────────

        public List<BoxModelCell> Solve(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut = null)
        {
            (previousBoxes, currentBoxes) = (currentBoxes, previousBoxes);
            currentBoxes.Clear();
            return SolveBoxes(parts, rule, canCut, true);
        }


        // Предпросмотр рисования: память только читается — коробка из предпросмотра не вытесняет настоящие.
        public List<BoxModelCell> SolveWithoutStoring(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut = null)
        {
            return SolveBoxes(parts, rule, canCut, false);
        }


        private List<BoxModelCell> SolveBoxes(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut, bool isStoring)
        {
            var cutters = BoxModelCutterSearch.FindCutters(parts, rule, canCut);
            var cells = new List<BoxModelCell>();
            RecomputedBoxCount = 0;

            for (var index = 0; index < parts.Count; index++)
                if (cutters[index] != null)
                    cells.AddRange(CellsOf(parts, index, cutters[index], isStoring));

            return cells;
        }

        // Куски коробки с номерами граней по списку: из памяти, если коробка и её резчики те же.
        private List<BoxModelCell> CellsOf(IReadOnlyList<BoxModelPartShape> parts, int index, List<int> cutters, bool isStoring)
        {
            var key = KeyOf(parts, index, cutters);
            var solved = Find(key, isStoring);

            if (solved == null)
            {
                RecomputedBoxCount++;
                solved = new BoxModelSolvedBox(keyNumbers.ToArray(), CellsOfBox(parts, index, cutters));

                if (isStoring)
                    currentBoxes[key] = solved;
            }

            return solved.CellsWithSources(SourceMapOf(index, cutters));
        }


        // ── Ключ ─────────────────────────────────────────────────────────

        private Hash128 KeyOf(IReadOnlyList<BoxModelPartShape> parts, int index, List<int> cutters)
        {
            keyNumbers.Clear();
            AddCorners(parts[index].Box);
            keyNumbers.Add(cutters.Count);

            foreach (var cutter in cutters)
                AddCorners(parts[cutter].Box);

            var key = new Hash128();
            key.Append(keyNumbers);
            return key;
        }

        private void AddCorners(MillimeterBox box)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                keyNumbers.Add(box.Min[axis]);
                keyNumbers.Add(box.Max[axis]);
            }
        }


        // Найденное сверяется с ключом по числам: совпадение хеша без совпадения коробок дало бы чужие куски.
        private BoxModelSolvedBox Find(Hash128 key, bool isStoring)
        {
            if (currentBoxes.TryGetValue(key, out var solved) && solved.HasKey(keyNumbers))
                return solved;

            if (!previousBoxes.TryGetValue(key, out solved) || !solved.HasKey(keyNumbers))
                return null;

            if (isStoring)
                currentBoxes[key] = solved;

            return solved;
        }


        // ── Куски одной коробки ──────────────────────────────────────────

        // Та же цепочка, что в солвере: каждый резчик по очереди выкусывает себя из набранных кусков.
        private static List<BoxModelCell> CellsOfBox(IReadOnlyList<BoxModelPartShape> parts, int index, List<int> cutters)
        {
            var cells = new List<BoxModelCell> { new(parts[index].Box, 0) };

            for (var order = 0; order < cutters.Count; order++)
            {
                var remaining = new List<BoxModelCell>(cells.Count + 4);

                foreach (var cell in cells)
                    BoxModelSolver.KeepOutside(cell, parts[cutters[order]].Box, order + 1, remaining);

                cells = remaining;
            }

            return cells;
        }


        private static int[] SourceMapOf(int index, List<int> cutters)
        {
            var sourceMap = new int[cutters.Count + 1];
            sourceMap[0] = index;

            for (var order = 0; order < cutters.Count; order++)
                sourceMap[order + 1] = cutters[order];

            return sourceMap;
        }


        /// <summary>
        /// Куски одной коробки в памяти: ключ числами, куски со своими номерами граней и последний перевод на номера списка —
        /// у коробки, которую не трогали, перевод тот же, и куски отдаются те же самые.
        /// </summary>
        private sealed class BoxModelSolvedBox
        {
            private readonly int[] keyNumbers;
            private readonly List<BoxModelCell> ownCells;
            private int[] lastSourceMap;
            private List<BoxModelCell> lastCells;


            public BoxModelSolvedBox(int[] keyNumbers, List<BoxModelCell> ownCells)
            {
                this.keyNumbers = keyNumbers;
                this.ownCells = ownCells;
            }


            public bool HasKey(List<int> numbers)
            {
                if (numbers.Count != keyNumbers.Length)
                    return false;

                for (var index = 0; index < keyNumbers.Length; index++)
                    if (numbers[index] != keyNumbers[index])
                        return false;

                return true;
            }


            public List<BoxModelCell> CellsWithSources(int[] sourceMap)
            {
                if (lastCells != null && IsSameMap(sourceMap))
                    return lastCells;

                lastSourceMap = sourceMap;
                lastCells = ownCells.ConvertAll(cell => cell.WithSources(sourceMap));
                return lastCells;
            }

            private bool IsSameMap(int[] sourceMap)
            {
                if (sourceMap.Length != lastSourceMap.Length)
                    return false;

                for (var index = 0; index < sourceMap.Length; index++)
                    if (sourceMap[index] != lastSourceMap[index])
                        return false;

                return true;
            }
        }
    }
}
