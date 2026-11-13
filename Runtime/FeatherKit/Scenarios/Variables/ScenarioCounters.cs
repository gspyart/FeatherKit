using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значения счётчиков сценариев: на всю игру или до загрузки следующего уровня — по времени жизни счётчика.
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

            if (Changed == null)
                return;

            ScenarioEventOccurrences.Begin();

            try
            {
                Changed.Invoke(counter, value);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }


        /// <summary>Все счётчики к начальным значениям — для новой игры. Changed не зовётся.</summary>
        public static void ResetAll()
        {
            Values.Clear();
        }


        // Уровень — сцена, загруженная целиком: догруженные поверх куски уровень не меняют.
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            var levelKeys = new List<ScenarioCounter>();

            foreach (var key in Values.Keys)
            {
                if (key == null || key.Lifetime == ScenarioVariableLifetime.Level)
                    levelKeys.Add(key);
            }

            foreach (var key in levelKeys)
                Values.Remove(key);
        }


        // Хранилище статическое: без перезагрузки домена значения дожили бы до следующего запуска.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Values.Clear();
            Changed = null;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }
}
