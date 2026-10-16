using System;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Журнал запусков сценариев за текущую игру: кто сработал, кто нет и почему.
    /// Одинаковые попытки подряд одного сценария сливаются в одну строку со счётчиком —
    /// триггер «пока внутри» иначе писал бы строку каждый кадр.
    /// </summary>
    [InitializeOnLoad]
    internal static class ScenarioRunLog
    {
        private const int MaxEntries = 300;

        private static readonly List<ScenarioRunLogEntry> LogEntries = new List<ScenarioRunLogEntry>();


        static ScenarioRunLog()
        {
            // Снимаем перед подпиской: статический конструктор зовётся и после перезагрузки домена.
            Scenario.RunReported -= Add;
            Scenario.RunReported += Add;

            EditorApplication.playModeStateChanged -= ClearOnEnterPlayMode;
            EditorApplication.playModeStateChanged += ClearOnEnterPlayMode;
        }


        public static event Action Changed;


        /// <summary>Старые сверху, новые снизу.</summary>
        public static IReadOnlyList<ScenarioRunLogEntry> Entries => LogEntries;


        public static void Clear()
        {
            LogEntries.Clear();
            Changed?.Invoke();
        }


        private static void Add(ScenarioRunRecord record)
        {
            var previous = LastEntryOf(record.Scenario);

            if (previous != null && previous.IsSameAs(record))
                previous.Repeat(record);
            else
                LogEntries.Add(new ScenarioRunLogEntry(record));

            if (LogEntries.Count > MaxEntries)
                LogEntries.RemoveAt(0);

            Changed?.Invoke();
        }

        private static ScenarioRunLogEntry LastEntryOf(Scenario scenario)
        {
            for (var index = LogEntries.Count - 1; index >= 0; index--)
            {
                if (LogEntries[index].Record.Scenario == scenario)
                    return LogEntries[index];
            }

            return null;
        }

        private static void ClearOnEnterPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                Clear();
        }
    }


    /// <summary>Строка журнала: последняя попытка и сколько раз подряд она повторилась.</summary>
    internal class ScenarioRunLogEntry
    {
        public ScenarioRunLogEntry(ScenarioRunRecord record)
        {
            Record = record;
            RepeatCount = 1;
        }


        public ScenarioRunRecord Record { get; private set; }

        public int RepeatCount { get; private set; }


        public bool IsSameAs(ScenarioRunRecord other)
        {
            return Record.Outcome == other.Outcome
                   && Record.InstigatorName == other.InstigatorName
                   && Record.FailedActionIndex == other.FailedActionIndex;
        }

        public void Repeat(ScenarioRunRecord latest)
        {
            Record = latest;
            RepeatCount++;
        }
    }
}
