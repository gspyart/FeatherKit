using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Номер «случая» — одной рассылки сигнала, флага, вызова или триггера по слушателям.
    /// Ожидание, начатое посреди рассылки, этот же случай не засчитывает: стук, начавший запуск, — не второй стук.
    /// </summary>
    internal static class ScenarioEventOccurrences
    {
        private static readonly Stack<int> ActiveOccurrences = new Stack<int>();

        private static int latestStarted;


        /// <summary>Номер идущей рассылки, самой вложенной; 0 — рассылки нет.</summary>
        public static int Current => ActiveOccurrences.Count > 0 ? ActiveOccurrences.Peek() : 0;

        /// <summary>Номер последней начатой рассылки: всё, что не больше, началось раньше.</summary>
        public static int LatestStarted => latestStarted;


        /// <summary>Начать рассылку. Каждому Begin — свой End, в finally.</summary>
        public static void Begin()
        {
            ActiveOccurrences.Push(++latestStarted);
        }

        public static void End()
        {
            if (ActiveOccurrences.Count > 0)
                ActiveOccurrences.Pop();
        }


        // Статика: без перезагрузки домена недоконченная рассылка прошлой игры осталась бы «идущей».
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            ActiveOccurrences.Clear();
            latestStarted = 0;
        }
    }
}
