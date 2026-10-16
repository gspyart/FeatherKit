using System;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значения флагов сценариев на весь запуск игры.
    /// Флаг, который ещё не трогали, равен своему начальному значению.
    /// </summary>
    public static class ScenarioFlags
    {
        private static readonly Dictionary<ScenarioFlag, bool> Values = new Dictionary<ScenarioFlag, bool>();


        public static event Action<ScenarioFlag, bool> Changed;


        public static bool Get(ScenarioFlag flag)
        {
            if (flag == null)
                return false;

            return Values.TryGetValue(flag, out var value) ? value : flag.InitialValue;
        }

        public static void Set(ScenarioFlag flag, bool value)
        {
            if (flag == null || Get(flag) == value)
                return;

            Values[flag] = value;

            Changed?.Invoke(flag, value);
        }


        /// <summary>Все флаги к начальным значениям — для новой игры. Changed не зовётся.</summary>
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
