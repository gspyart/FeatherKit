using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Насколько трудно игроку: легко, на пределе, нельзя — для цвета надписи линейки. Unknown — проверка прыжков выключена.</summary>
    public enum LiveBuilderJumpVerdict
    {
        Unknown,
        Easy,
        Hard,
        Impossible
    }


    /// <summary>Строка таблицы прыжков: насколько ниже цель, какая щель с разбега легко и какая на пределе, м.</summary>
    [System.Serializable]
    public struct LiveBuilderJumpGap
    {
        [Tooltip("Насколько ниже места прыжка цель, м. 0 — тот же уровень.")]
        [Min(0f)]
        [SerializeField] private float drop;

        [Tooltip("Щель до этой ширины — легко, м.")]
        [Min(0f)]
        [SerializeField] private float easyGap;

        [Tooltip("Шире этой щели — не допрыгнуть, м.")]
        [Min(0f)]
        [SerializeField] private float maxGap;


        public LiveBuilderJumpGap(float drop, float easyGap, float maxGap)
        {
            this.drop = drop;
            this.easyGap = easyGap;
            this.maxGap = maxGap;
        }


        public float Drop => drop;
        public float EasyGap => easyGap;
        public float MaxGap => maxGap;
    }


    /// <summary>
    /// Допрыгнет ли игрок с точки на точку — по таблице прыжков из настроек проекта (<see cref="LiveBuilderProjectSettings"/>):
    /// щель с разбега на том же уровне и ниже, край выше — только в вис, и чем грозит падение.
    /// Цифры — грубая сверка на глаз, а не расчёт прыжка. Проверка выключена — вердикта нет.
    /// </summary>
    public static class LiveBuilderJumpCheck
    {
        private static LiveBuilderProjectSettings Settings => LiveBuilderProjectSettings.instance;


        // Вердикт и строка для надписи: «легко», «на пределе», «не допрыгнуть» и про падение. Проверка выключена — Unknown и пустая строка.
        public static LiveBuilderJumpVerdict Judge(float horizontal, float rise, out string text)
        {
            if (!Settings.IsJumpCheckEnabled)
            {
                text = "";
                return LiveBuilderJumpVerdict.Unknown;
            }

            if (rise > 0.5f)
                return JudgeUpwards(horizontal, rise, out text);

            var drop = -rise;

            if (drop >= Settings.DeadlyFall)
            {
                text = "падение с " + Format(drop) + " м — смерть";
                return LiveBuilderJumpVerdict.Impossible;
            }

            GetGapLimits(drop, out var easy, out var limit);
            var fallNote = drop > Settings.SafeFall ? ", жёсткое приземление" : "";

            if (horizontal <= easy)
            {
                text = "легко (до " + Format(easy) + " м)" + fallNote;
                return LiveBuilderJumpVerdict.Easy;
            }

            if (horizontal <= limit)
            {
                text = "на пределе (предел " + Format(limit) + " м)" + fallNote;
                return LiveBuilderJumpVerdict.Hard;
            }

            text = "не допрыгнуть (предел " + Format(limit) + " м)";
            return LiveBuilderJumpVerdict.Impossible;
        }

        // Цель выше: край не выше досягаемого в вис — в вис, при щели не шире предела того же уровня.
        private static LiveBuilderJumpVerdict JudgeUpwards(float horizontal, float rise, out string text)
        {
            GetGapLimits(0f, out _, out var limit);

            if (rise > Settings.MaxHangReach || horizontal > limit)
            {
                text = "не достать — выше " + Format(Settings.MaxHangReach) + " м или дальше " + Format(limit) + " м; нужен уступ или бег по стене";
                return LiveBuilderJumpVerdict.Impossible;
            }

            text = "только в вис на край";
            return LiveBuilderJumpVerdict.Hard;
        }

        // Легко и предел для щели на эту глубину вниз — между строками таблицы по прямой. «Легко» не шире предела.
        private static void GetGapLimits(float drop, out float easy, out float limit)
        {
            InterpolateGapLimits(drop, out easy, out limit);
            easy = Mathf.Min(easy, limit);
        }

        private static void InterpolateGapLimits(float drop, out float easy, out float limit)
        {
            var gaps = GapsByDrop();
            var clamped = Mathf.Clamp(drop, gaps[0].Drop, gaps[^1].Drop);

            for (var index = 0; index < gaps.Count - 1; index++)
            {
                var upper = gaps[index];
                var lower = gaps[index + 1];

                if (clamped > lower.Drop)
                    continue;

                var fraction = Mathf.InverseLerp(upper.Drop, lower.Drop, clamped);
                easy = Mathf.Lerp(upper.EasyGap, lower.EasyGap, fraction);
                limit = Mathf.Lerp(upper.MaxGap, lower.MaxGap, fraction);
                return;
            }

            easy = gaps[^1].EasyGap;
            limit = gaps[^1].MaxGap;
        }

        // Строки таблицы по глубине: в настройках их могли вписать вразнобой. Копия — настройки не трогаем.
        private static List<LiveBuilderJumpGap> GapsByDrop()
        {
            var gaps = new List<LiveBuilderJumpGap>(Settings.JumpGaps);
            gaps.Sort((first, second) => first.Drop.CompareTo(second.Drop));
            return gaps;
        }


        private static string Format(float meters)
        {
            return LiveBuilderSceneText.FormatMeters(meters);
        }
    }
}
