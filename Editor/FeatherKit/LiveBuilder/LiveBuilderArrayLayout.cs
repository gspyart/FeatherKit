using System.Collections.Generic;
using System.Globalization;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Места копий одного повтора (<see cref="LiveBuilderArray"/>) по его числам — чистая математика, без сцены.
    /// - копии по X, Y, Z с шагом в осях повтора; копия 1 по всем осям — место самого оригинала
    /// - пропуски по каждой оси (<see cref="LiveBuilderArraySkipList"/>); шаг и число Y по этажам — из <see cref="LiveBuilderArrayContext"/>
    /// - Fill по X: сколько влезет в длину, шаг растягивается поровну; вариант узора по номеру X (<see cref="LiveBuilderArrayPattern"/>)
    /// </summary>
    public static class LiveBuilderArrayLayout
    {
        // Длина, в которую копии влезают ровно, — тоже «влезают»: 20 м по шагу 2 м — 11 копий, а не 10 из-за дробей.
        private const float FillTolerance = 1e-4f;


        // Копии по порядку: X быстрее всего, потом Y, потом Z. Пропущенных нет; больше limit — тоже.
        public static List<LiveBuilderArrayPose> PosesOf(LiveBuilderArray array, LiveBuilderArrayContext context, int limit = LiveBuilderArray.MaxCopies)
        {
            var counts = CountsOf(array, context);
            var steps = StepsOf(array, context);
            var keptX = KeptNumbersOf(array, 0, counts.x);
            var keptY = KeptNumbersOf(array, 1, counts.y);
            var keptZ = KeptNumbersOf(array, 2, counts.z);
            var poses = new List<LiveBuilderArrayPose>();

            foreach (var z in keptZ)
                foreach (var y in keptY)
                    foreach (var x in keptX)
                    {
                        if (poses.Count >= limit)
                            return poses;

                        var number = x + (y - 1) * counts.x + (z - 1) * counts.x * counts.y;
                        var offset = new Vector3(context.FirstCopyShift + steps.x * (x - 1), steps.y * (y - 1), steps.z * (z - 1));
                        poses.Add(new LiveBuilderArrayPose(new Vector3Int(x, y, z), number, offset, LiveBuilderArrayPattern.VariantOf(array, x)));
                    }

            return poses;
        }

        // Номера копий по оси без пропущенных — цикл идёт только по ним, даже если пропущено почти всё.
        private static List<int> KeptNumbersOf(LiveBuilderArray array, int axis, int count)
        {
            LiveBuilderArraySkipList.TryParse(array.SkipOf(axis), count, out var skipped, out _);
            var kept = new List<int>(count);

            for (var number = 1; number <= count; number++)
                if (!skipped.Contains(number))
                    kept.Add(number);

            return kept;
        }


        // Сколько копий по осям: Y «до верха здания» — по этажам от этажа повтора до верхнего. В этаже здания копии по этажам выше верхнего не встают.
        public static Vector3Int CountsOf(LiveBuilderArray array, LiveBuilderArrayContext context)
        {
            var countY = array.CountYToBuildingTop && context.StoreysToTop > 0 ? context.StoreysToTop : array.CountY;

            if (context.IsInBuildingStorey && array.StepYIsStoreyHeight && context.StoreysToTop > 0)
                countY = Mathf.Min(countY, context.StoreysToTop);

            var countX = array.Mode == LiveBuilderArrayMode.Fill ? FillCountOf(context.FillLength, array.Margin, context.CopyWidth, array.StepX) : array.CountX;
            return new Vector3Int(countX, countY, array.CountZ);
        }


        // Сколько всего мест, вместе с пропущенными, — для предела копий.
        public static int TotalCountOf(LiveBuilderArray array, LiveBuilderArrayContext context)
        {
            var counts = CountsOf(array, context);
            return counts.x * counts.y * counts.z;
        }


        // Шаг по осям, м: Y — высота этажа, если так сказано и она известна; X у Fill — растянутый, чтобы ряд лёг в длину ровно.
        public static Vector3 StepsOf(LiveBuilderArray array, LiveBuilderArrayContext context)
        {
            var stepY = array.StepYIsStoreyHeight && context.StoreyHeight > 0f ? context.StoreyHeight : array.StepY;
            var stepX = array.Mode == LiveBuilderArrayMode.Fill ? FillStepOf(context.FillLength, array.Margin, context.CopyWidth, array.StepX) : array.StepX;
            return new Vector3(stepX, stepY, array.StepZ);
        }


        // ── Fill ─────────────────────────────────────────────────────────

        // Сколько копий шириной copyWidth влезает в длину с отступами по краям при шаге не меньше step; хоть одна — всегда.
        public static int FillCountOf(float length, float margin, float copyWidth, float step)
        {
            var room = FillRoomOf(length, margin, copyWidth);

            if (room <= 0f || step <= 0f)
                return 1;

            return Mathf.Clamp(Mathf.FloorToInt(room / step + FillTolerance) + 1, 1, LiveBuilderArray.MaxCountPerAxis);
        }


        // Шаг, растянутый так, чтобы первая и последняя копии встали ровно по отступам.
        public static float FillStepOf(float length, float margin, float copyWidth, float step)
        {
            var count = FillCountOf(length, margin, copyWidth, step);
            return count > 1 ? FillRoomOf(length, margin, copyWidth) / (count - 1) : step;
        }

        // Сколько метров между опорами первой и последней копий.
        private static float FillRoomOf(float length, float margin, float copyWidth)
        {
            return length - 2f * margin - copyWidth;
        }
    }


    /// <summary>
    /// Что повтор берёт со сцены: высоту этажа (здания над ним или уровня), сколько этажей от его этажа до верха здания (0 — не в здании)
    /// и лежит ли он в этаже здания — тогда копии по этажам не выходят выше верхнего.
    /// </summary>
    public readonly struct LiveBuilderArrayContext
    {
        public LiveBuilderArrayContext(float storeyHeight, int storeysToTop, bool isInBuildingStorey = false)
        {
            StoreyHeight = storeyHeight;
            StoreysToTop = storeysToTop;
            IsInBuildingStorey = isInBuildingStorey;
            FillLength = 0f;
            CopyWidth = 0f;
            FirstCopyShift = 0f;
        }

        private LiveBuilderArrayContext(LiveBuilderArrayContext storeys, float fillLength, float copyWidth, float firstCopyShift)
            : this(storeys.StoreyHeight, storeys.StoreysToTop, storeys.IsInBuildingStorey)
        {
            FillLength = fillLength;
            CopyWidth = copyWidth;
            FirstCopyShift = firstCopyShift;
        }


        public float StoreyHeight { get; }
        public int StoreysToTop { get; }
        public bool IsInBuildingStorey { get; }

        // Fill: длина ряда и ширина копии по X, м.
        public float FillLength { get; }
        public float CopyWidth { get; }

        // Где по X первая копия от оригинала, м: вдоль стены «вся сторона» — от её начала; иначе ноль.
        public float FirstCopyShift { get; }


        public LiveBuilderArrayContext WithFill(float fillLength, float copyWidth, float firstCopyShift)
        {
            return new LiveBuilderArrayContext(this, fillLength, copyWidth, firstCopyShift);
        }
    }


    /// <summary>
    /// Одна копия повтора: номера по осям с 1, общий номер с 1 (X быстрее всего), сдвиг от оригинала в осях повтора, м,
    /// и вариант узора (−1 — без узора: копируется всё). Вдоль стены X сдвига — расстояние по линии.
    /// </summary>
    public readonly struct LiveBuilderArrayPose
    {
        public LiveBuilderArrayPose(Vector3Int numbers, int number, Vector3 offset, int variant = LiveBuilderArrayPattern.AllVariants)
        {
            Numbers = numbers;
            Number = number;
            Offset = offset;
            Variant = variant;
        }


        public Vector3Int Numbers { get; }
        public int Number { get; }
        public Vector3 Offset { get; }
        public int Variant { get; }

        public bool IsOriginal => Numbers == Vector3Int.one;

        // Копия 1 там же, где оригинал: вдоль стены «вся сторона» первая копия может стоять в стороне, пока оригинал не переехал.
        public bool IsAtOriginalPlace => IsOriginal && Offset == Vector3.zero;
    }


    /// <summary>
    /// Узор повтора: какой дочерний вариант (A, B, C… — по списку <see cref="LiveBuilderArray.Variants"/>) показывает копия с номером X.
    /// Строка «ABBA» идёт по кругу; пусто — всегда первый; вариантов нет — копируется всё. Ошибка — по-английски, для инспектора.
    /// </summary>
    public static class LiveBuilderArrayPattern
    {
        // Без узора: копия показывает всё, что под повтором.
        public const int AllVariants = -1;

        // Буквы узора без варианта — вариант, которого нет: такая копия показывает только общее.
        public const int MissingVariant = -2;


        // Вариант копии номер numberX (с 1): узор по кругу, пропуски его не сдвигают.
        public static int VariantOf(LiveBuilderArray array, int numberX)
        {
            if (array.Variants.Count == 0)
                return AllVariants;

            var letters = LettersOf(array.Pattern);

            if (letters.Length == 0)
                return 0;

            var variant = letters[(numberX - 1) % letters.Length] - 'A';
            return variant < array.Variants.Count ? variant : MissingVariant;
        }


        // Только буквы, заглавными: «a b, a» — «ABA».
        public static string LettersOf(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return "";

            var letters = new System.Text.StringBuilder(pattern.Length);

            foreach (var character in pattern)
                if (char.IsLetter(character) && character < 128)
                    letters.Append(char.ToUpperInvariant(character));

            return letters.ToString();
        }


        // Номер варианта, под которым лежит узел, — ближайший к повтору ребёнок из списка; не под вариантом — AllVariants.
        public static int VariantIndexOf(LiveBuilderArray array, Transform child)
        {
            return array.Variants.Count == 0 || child == null ? AllVariants : array.Variants.IndexOf(child);
        }


        // Что не так с узором: буквы без варианта, вариант — не ребёнок повтора.
        public static string ProblemOf(LiveBuilderArray array)
        {
            foreach (var variant in array.Variants)
                if (variant == null || variant.parent != array.transform)
                    return "Every variant must be a child of the array object.";

            if (array.Variants.Count == 0)
                return LettersOf(array.Pattern).Length > 0 ? "Pattern has no variants: add children to Variants." : null;

            foreach (var letter in LettersOf(array.Pattern))
                if (letter - 'A' >= array.Variants.Count)
                    return "Pattern letter " + letter + " has no variant: there are " + array.Variants.Count + ".";

            return null;
        }
    }


    /// <summary>
    /// Строка пропусков одной оси повтора: номера с 1 через запятую, диапазоны «2-4», «last» — последняя копия.
    /// Ошибка — по-английски, для инспектора; номера за пределами числа копий просто ничего не пропускают.
    /// </summary>
    public static class LiveBuilderArraySkipList
    {
        private const string LastWord = "last";


        // Пропущенные номера; строка с ошибкой — всё, что разобралось до неё, и текст ошибки.
        public static bool TryParse(string text, int count, out HashSet<int> skipped, out string error)
        {
            skipped = new HashSet<int>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            foreach (var rawPart in text.Split(',', ';'))
            {
                var part = rawPart.Trim();

                if (part.Length == 0)
                    continue;

                if (!TryParseRange(part, count, out var from, out var to))
                {
                    error = "Can't read '" + part + "': use numbers from 1, ranges like 2-4 or 'last'.";
                    return false;
                }

                for (var number = Mathf.Max(1, from); number <= Mathf.Min(count, to); number++)
                    skipped.Add(number);
            }

            return true;
        }

        // Номер, «last» или «от-до»; «от» больше «до» — тоже диапазон, в обратную сторону.
        private static bool TryParseRange(string part, int count, out int from, out int to)
        {
            from = to = 0;
            var dash = part.IndexOf('-', 1);

            if (dash < 0)
            {
                if (!TryParseNumber(part, count, out from))
                    return false;

                to = from;
                return true;
            }

            if (!TryParseNumber(part.Substring(0, dash).Trim(), count, out from) || !TryParseNumber(part.Substring(dash + 1).Trim(), count, out to))
                return false;

            if (from > to)
                (from, to) = (to, from);

            return true;
        }

        private static bool TryParseNumber(string text, int count, out int number)
        {
            if (string.Equals(text, LastWord, System.StringComparison.OrdinalIgnoreCase))
            {
                number = count;
                return true;
            }

            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
        }
    }
}
