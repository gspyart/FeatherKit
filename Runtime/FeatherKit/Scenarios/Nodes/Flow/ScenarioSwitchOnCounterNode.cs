using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Switch on Counter»: идёт в выход случая с текущим значением счётчика; такого случая нет — Default.
    /// - у случая постоянный id: поменяли число — провод остаётся
    /// - одно значение в двух случаях — первый побеждает, проверка графа о повторе предупреждает
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Switch on Counter", Description = "Go to the output of the counter's current value")]
    public class ScenarioSwitchOnCounterNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Default = new ScenarioOutput("default", "Default");

        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounterReference counter = new ScenarioCounterReference();

        [Tooltip("One output per case; the counter value picks the case")]
        [ScenarioLabel("Cases")]
        [SerializeField] private List<ScenarioSwitchCase> cases = new List<ScenarioSwitchCase>();


        public ScenarioCounterReference Counter => counter;

        public IReadOnlyList<ScenarioSwitchCase> Cases => cases;

        public override string Summary => counter.Summary;


        // ── Случаи ───────────────────────────────────────────────────────

        public ScenarioSwitchCase AddCase(int value)
        {
            var switchCase = new ScenarioSwitchCase(value);
            cases.Add(switchCase);

            return switchCase;
        }


        /// <summary>Убрать случай. Провода его выхода снимает тот, кто правит граф: ScenarioGraph.DisconnectPort.</summary>
        public bool RemoveCase(string caseId)
        {
            var switchCase = FindCase(caseId);

            return switchCase != null && cases.Remove(switchCase);
        }


        public ScenarioSwitchCase FindCase(string caseId)
        {
            foreach (var switchCase in cases)
            {
                if (switchCase != null && string.Equals(switchCase.Id, caseId, StringComparison.Ordinal))
                    return switchCase;
            }

            return null;
        }


        /// <summary>Выдать новые id пустым и повторным: копия элемента в инспекторе несёт id соседа. True — повторы были.</summary>
        public bool EnsureUniqueCaseIds()
        {
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var hadRepeats = false;

            foreach (var switchCase in cases)
            {
                if (switchCase == null)
                    continue;

                if (string.IsNullOrEmpty(switchCase.Id) || usedIds.Contains(switchCase.Id))
                {
                    switchCase.AssignNewId();
                    hadRepeats = true;
                }

                usedIds.Add(switchCase.Id);
            }

            return hadRepeats;
        }


        /// <summary>Одно значение в двух случаях — для проверки графа: второй случай никогда не сработает.</summary>
        public bool TryFindRepeatedValue(out int repeatedValue)
        {
            var seenValues = new HashSet<int>();

            foreach (var switchCase in cases)
            {
                if (switchCase != null && !seenValues.Add(switchCase.Value))
                {
                    repeatedValue = switchCase.Value;
                    return true;
                }
            }

            repeatedValue = 0;
            return false;
        }


        // ── Порты и ход ──────────────────────────────────────────────────

        // Выходы случаев — до Default: на ноде он стоит последним.
        public override void DescribePorts(ScenarioPortList ports)
        {
            foreach (var input in ScenarioNodePorts.InputsOf(GetType()))
                ports.AddInput(input);

            foreach (var switchCase in cases)
            {
                if (switchCase != null)
                    ports.AddOutput(switchCase.Output);
            }

            ports.AddOutput(Default);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var current = counter.Get(run);

            foreach (var switchCase in cases)
            {
                if (switchCase != null && switchCase.Value == current)
                {
                    Fire(run, switchCase.Output);
                    yield break;
                }
            }

            Fire(run, Default);
        }
    }
}
