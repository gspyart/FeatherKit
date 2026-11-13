using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Do»: стопка блоков-шагов, по очереди сверху вниз.
    /// Шаг без «Wait Until Done» уходит в фон; выход Done — когда дошёл последний шаг стопки.
    /// </summary>
    [Serializable]
    public class ScenarioStepsNode : ScenarioNode
    {
        public static readonly ScenarioInput In = new ScenarioInput("in", "In");
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [SerializeField] private ScenarioActionList steps = new ScenarioActionList();


        public override string DefaultTitle => "Do";

        public ScenarioActionList Steps => steps;


        // ── Сборка кодом ─────────────────────────────────────────────────

        public void AddStep(ScenarioAction step)
        {
            steps.Add(step);
        }
    }
}
