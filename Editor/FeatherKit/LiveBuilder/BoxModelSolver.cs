using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Коробка объекта, приведённая к миллиметрам: где она и что делает.</summary>
    public readonly struct BoxModelPartShape
    {
        public BoxModelPartShape(MillimeterBox box, BoxModelPartMode mode)
        {
            Box = box;
            Mode = mode;
        }

        public MillimeterBox Box { get; }
        public BoxModelPartMode Mode { get; }
    }


    /// <summary>
    /// Кусок итогового объёма — коробка, которая ни с кем не пересекается.
    /// У каждой грани запомнено, какая коробка объекта её создала: от неё грань берёт материал, по ней выделяется кликом.
    /// </summary>
    public sealed class BoxModelCell
    {
        public BoxModelCell(MillimeterBox box, IReadOnlyList<int> faceSources)
        {
            Box = box;
            FaceSources = faceSources;
        }

        public BoxModelCell(MillimeterBox box, int source)
            : this(box, new[] { source, source, source, source, source, source })
        {
        }

        public MillimeterBox Box { get; }

        // Номер коробки объекта для каждой грани, по порядку BoxFaces.
        public IReadOnlyList<int> FaceSources { get; }


        // Тот же кусок, номер коробки каждой грани переведён по карте: был k — стал sourceMap[k].
        public BoxModelCell WithSources(IReadOnlyList<int> sourceMap)
        {
            var sources = new int[BoxFaces.Count];

            for (var face = 0; face < BoxFaces.Count; face++)
                sources[face] = sourceMap[FaceSources[face]];

            return new BoxModelCell(Box, sources);
        }


        // Тот же кусок, обрезанный по оси до [from, to]. Сдвинутая грань — грань разреза: её создала коробка cutterIndex.
        public BoxModelCell Slice(int axis, int from, int to, int cutterIndex)
        {
            var min = Box.Min;
            var max = Box.Max;
            var sources = new int[BoxFaces.Count];

            for (var face = 0; face < BoxFaces.Count; face++)
                sources[face] = FaceSources[face];

            if (from != min[axis])
            {
                min[axis] = from;
                sources[BoxFaces.Index(axis, false)] = cutterIndex;
            }

            if (to != max[axis])
            {
                max[axis] = to;
                sources[BoxFaces.Index(axis, true)] = cutterIndex;
            }

            return new BoxModelCell(new MillimeterBox(min, max), sources);
        }
    }


    /// <summary>
    /// Считает, что остаётся от коробок объекта: идёт по ним по порядку, вырезающая выкусывает объём из уже набранного,
    /// добавляющая сперва выкусывает своё место, потом кладёт себя целиком — поэтому что ниже в списке, то главнее.
    /// Результат — набор непересекающихся коробок: из прямоугольников дыр и мусорных треугольников не бывает.
    /// Считает весь список заново — эталон для кеша по коробкам (<see cref="BoxModelSolverCache"/>) и для разовых расчётов.
    /// </summary>
    public static class BoxModelSolver
    {
        // Эталон нового правила: сначала все добавляющие, потом все вырезы; вырез режет только куски тех добавляющих, кого пускает canCut.
        // Номера граней — по исходному списку.
        public static List<BoxModelCell> Solve(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut = null)
        {
            if (rule == BoxModelCutRule.LaterBoxesCut)
                return Solve(parts);

            var cells = new List<BoxModelCell>();
            var cellOwners = new List<int>();

            foreach (var partIndex in AddsThenCuts(parts))
            {
                var part = parts[partIndex];

                if (part.Box.IsEmpty)
                    continue;

                var isAdd = part.Mode == BoxModelPartMode.Add;
                var remaining = new List<BoxModelCell>(cells.Count);
                var remainingOwners = new List<int>(cells.Count);

                for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    var owner = cellOwners[cellIndex];

                    if (isAdd || canCut == null || canCut(partIndex, owner))
                        KeepOutside(cells[cellIndex], part.Box, partIndex, remaining);
                    else
                        remaining.Add(cells[cellIndex]);

                    while (remainingOwners.Count < remaining.Count)
                        remainingOwners.Add(owner);
                }

                cells = remaining;
                cellOwners = remainingOwners;

                if (isAdd)
                {
                    cells.Add(new BoxModelCell(part.Box, partIndex));
                    cellOwners.Add(partIndex);
                }
            }

            return cells;
        }

        private static List<int> AddsThenCuts(IReadOnlyList<BoxModelPartShape> parts)
        {
            var order = new List<int>(parts.Count);

            for (var index = 0; index < parts.Count; index++)
                if (parts[index].Mode == BoxModelPartMode.Add)
                    order.Add(index);

            for (var index = 0; index < parts.Count; index++)
                if (parts[index].Mode != BoxModelPartMode.Add)
                    order.Add(index);

            return order;
        }


        public static List<BoxModelCell> Solve(IReadOnlyList<BoxModelPartShape> parts)
        {
            var cells = new List<BoxModelCell>();

            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var part = parts[partIndex];

                if (part.Box.IsEmpty)
                    continue;

                var remaining = new List<BoxModelCell>(cells.Count);

                foreach (var cell in cells)
                    KeepOutside(cell, part.Box, partIndex, remaining);

                cells = remaining;

                if (part.Mode == BoxModelPartMode.Add)
                    cells.Add(new BoxModelCell(part.Box, partIndex));
            }

            return cells;
        }


        // Кладёт в result то, что от куска осталось снаружи коробки cutter: по каждой оси отрезает слой до неё и после неё.
        // Что осталось в конце — целиком внутри cutter и выбрасывается.
        public static void KeepOutside(BoxModelCell cell, MillimeterBox cutter, int cutterIndex, List<BoxModelCell> result)
        {
            if (!cell.Box.Overlaps(cutter))
            {
                result.Add(cell);
                return;
            }

            var rest = cell;

            for (var axis = 0; axis < 3; axis++)
            {
                var restMin = rest.Box.Min[axis];
                var restMax = rest.Box.Max[axis];

                if (restMin < cutter.Min[axis])
                {
                    result.Add(rest.Slice(axis, restMin, cutter.Min[axis], cutterIndex));
                    rest = rest.Slice(axis, cutter.Min[axis], restMax, cutterIndex);
                    restMin = cutter.Min[axis];
                }

                if (restMax > cutter.Max[axis])
                {
                    result.Add(rest.Slice(axis, cutter.Max[axis], restMax, cutterIndex));
                    rest = rest.Slice(axis, restMin, cutter.Max[axis], cutterIndex);
                }
            }
        }
    }
}
