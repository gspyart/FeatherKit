using System;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значения счётчиков сценариев на весь запуск игры.
    /// Счётчик, который ещё не трогали, равен своему начальному значению.
    /// </summary>
    public static class ScenarioCounters
    {
        private static readonly Dictionary<ScenarioCounter, int> Values = new Dictionary<ScenarioCounter, int>();


        public static event Action<ScenarioCounter, int> Changed;


        public static int Get(ScenarioCounter counter)
        {
            if (counter == null)
                return 0;

            return Values.TryGetValue(counter, out var value) ? value : counter.InitialValue;
        }

        public static void Set(ScenarioCounter counter, int value)
        {
            if (counter == null || Get(counter) == value)
                return;

            Values[counter] = value;

            Changed?.Invoke(counter, value);
        }


        /// <summary>Все счётчики к начальным значениям — для новой игры. Changed не зовётся.</summary>
        public static void ResetAll()
        {
            Values.Clear();
        }


        // Хранилище статическое: без перезагрузки домена значения дожили бы до следующего запуска.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Values.Clear();
            Changed = null;
        }
    }
}
