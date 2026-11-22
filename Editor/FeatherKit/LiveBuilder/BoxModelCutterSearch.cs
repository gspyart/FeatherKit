using System.Collections.Generic;
using FeatherKit.LiveBuilder;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Кто режет каждую добавляющую коробку объекта по правилу солвера (<see cref="BoxModelCutRule"/>) — одно место на это правило.
    /// - пересечения ищет проходом по коробкам, отсортированным по X, а не перебором каждой с каждой
    /// - резчики коробки — в том порядке, в каком они режут её в солвере
    /// </summary>
    public static class BoxModelCutterSearch
    {
        // Резчики каждой коробки по её номеру; у вырезающей и пустой — null: кусков у них нет.
        public static List<int>[] FindCutters(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut = null)
        {
            var cutters = new List<int>[parts.Count];

            for (var index = 0; index < parts.Count; index++)
                if (IsAdd(parts[index]))
                    cutters[index] = new List<int>();

            var sorted = NonEmptySortedByMinX(parts);

            for (var position = 0; position < sorted.Count; position++)
            {
                var first = sorted[position];
                var firstBox = parts[first].Box;

                for (var next = position + 1; next < sorted.Count && parts[sorted[next]].Box.Min.x < firstBox.Max.x; next++)
                {
                    var second = sorted[next];

                    if (firstBox.Overlaps(parts[second].Box))
                        AddPair(parts, rule, canCut, cutters, System.Math.Min(first, second), System.Math.Max(first, second));
                }
            }

            System.Comparison<int> byCutOrder = (first, second) => CutOrderOf(parts, rule, first).CompareTo(CutOrderOf(parts, rule, second));

            foreach (var list in cutters)
                list?.Sort(byCutOrder);

            return cutters;
        }

        private static List<int> NonEmptySortedByMinX(IReadOnlyList<BoxModelPartShape> parts)
        {
            var sorted = new List<int>(parts.Count);

            for (var index = 0; index < parts.Count; index++)
                if (!parts[index].Box.IsEmpty)
                    sorted.Add(index);

            sorted.Sort((first, second) =>
            {
                var byMinX = parts[first].Box.Min.x.CompareTo(parts[second].Box.Min.x);
                return byMinX != 0 ? byMinX : first.CompareTo(second);
            });

            return sorted;
        }

        // Пересекаются коробки earlier и later (earlier выше в списке): кто из них кого режет.
        private static void AddPair(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, BoxModelCutFilter canCut, List<int>[] cutters,
            int earlier, int later)
        {
            var isEarlierAdd = IsAdd(parts[earlier]);
            var isLaterAdd = IsAdd(parts[later]);

            if (rule == BoxModelCutRule.LaterBoxesCut || isEarlierAdd && isLaterAdd)
            {
                if (isEarlierAdd)
                    cutters[earlier].Add(later);

                return;
            }

            if (isEarlierAdd && CanCut(canCut, later, earlier))
                cutters[earlier].Add(later);
            else if (isLaterAdd && CanCut(canCut, earlier, later))
                cutters[later].Add(earlier);
        }

        private static bool CanCut(BoxModelCutFilter canCut, int cutIndex, int addIndex)
        {
            return canCut == null || canCut(cutIndex, addIndex);
        }

        // Место резчика в очереди: по старому правилу — номер в списке, по новому — сначала добавляющие, потом вырезы.
        private static long CutOrderOf(IReadOnlyList<BoxModelPartShape> parts, BoxModelCutRule rule, int index)
        {
            var isCutAfterAdds = rule == BoxModelCutRule.CutsAfterAllAdds && !IsAdd(parts[index]);
            return isCutAfterAdds ? (long)parts.Count + index : index;
        }

        private static bool IsAdd(BoxModelPartShape part)
        {
            return part.Mode == BoxModelPartMode.Add && !part.Box.IsEmpty;
        }
    }
}
