using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значения флагов сценариев: на всю игру или до загрузки следующего уровня — по времени жизни флага.
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

            if (Changed == null)
                return;

            ScenarioEventOccurrences.Begin();

            try
            {
                Changed.Invoke(flag, value);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }


        /// <summary>Все флаги к начальным значениям — для новой игры. Changed не зовётся.</summary>
        public static void ResetAll()
        {
            Values.Clear();
        }


        // Уровень — сцена, загруженная целиком: догруженные поверх куски уровень не меняют.
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            var levelKeys = new List<ScenarioFlag>();

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
