using System;
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

        // Номера «общих» случаев этого кадра: блоки одного типа от одного Caller слышат одно и то же.
        private static readonly Dictionary<(Type, GameObject), int> SharedNumbersThisFrame = new Dictionary<(Type, GameObject), int>();

        private static int sharedNumbersFrame = -1;

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

        /// <summary>
        /// Начать рассылку, которую FeatherKit не видит целиком: блоки одного типа с одним Caller в одном кадре —
        /// один случай. Каждому BeginShared — свой End, в finally.
        /// </summary>
        public static void BeginShared(Type eventType, GameObject instigator)
        {
            if (sharedNumbersFrame != Time.frameCount)
            {
                SharedNumbersThisFrame.Clear();
                sharedNumbersFrame = Time.frameCount;
            }

            var key = (eventType, instigator);

            if (!SharedNumbersThisFrame.TryGetValue(key, out var number))
            {
                number = ++latestStarted;
                SharedNumbersThisFrame[key] = number;
            }

            ActiveOccurrences.Push(number);
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
            SharedNumbersThisFrame.Clear();
            sharedNumbersFrame = -1;
            latestStarted = 0;
        }
    }
}
