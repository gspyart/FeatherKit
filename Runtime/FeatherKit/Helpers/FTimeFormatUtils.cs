using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Время словами для подписей в интерфейсе.
    /// - часы для отсчёта: «1:05:30», без часов — «12:34»;
    /// - длительность для обещания: «45 s», «5 min», «1 h 30 min».
    /// </summary>
    public static class FTimeFormatUtils
    {
        // ── Отсчёт ────────────────────────────────────────────────────────

        /// <summary>
        /// Часы показываем, только пока они идут: «00:12:34» на узкой плашке читается хуже,
        /// а нули впереди игроку ничего не говорят. Как округлять секунды, решает зовущий.
        /// </summary>
        public static string ToClockText(int totalSeconds)
        {
            var seconds = Mathf.Max(0, totalSeconds);
            var hours = seconds / 3600;
            var minutes = seconds / 60 % 60;

            return hours > 0
                ? $"{hours}:{minutes:00}:{seconds % 60:00}"
                : $"{seconds / 60}:{seconds % 60:00}";
        }


        // ── Длительность ──────────────────────────────────────────────────

        /// <summary>
        /// Обещание, а не отсчёт: секунды в нём лишние, кроме совсем коротких сроков.
        /// Округляем вверх — обещать меньше, чем выйдет, нельзя.
        /// </summary>
        public static string ToDurationText(float seconds)
        {
            if (seconds < 60f)
                return $"{Mathf.CeilToInt(seconds)} s";

            var minutes = Mathf.CeilToInt(seconds / 60f);

            if (minutes < 60)
                return $"{minutes} min";

            return minutes % 60 == 0
                ? $"{minutes / 60} h"
                : $"{minutes / 60} h {minutes % 60} min";
        }
    }
}
