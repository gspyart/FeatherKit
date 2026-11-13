using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Guard While»: пускает Body и держит его, пока верно условие.
    /// - на входе условие ложно — сразу Broken
    /// - нарушилось — всё выросшее из Body обрывается, долгие эффекты Body плавно уходят, выход Broken
    /// - всё из Body кончилось само — выход Done
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Guard While")]
    public class ScenarioGuardNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Body = new ScenarioOutput("body", "Body");
        public static readonly ScenarioOutput Broken = new ScenarioOutput("broken", "Broken");
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("How often to check the conditions while Body runs, in seconds. 0 — every frame")]
        [Min(0f)]
        [ScenarioLabel("Check Interval, s")]
        [ScenarioInspectorOnly]
        [SerializeField] private float checkInterval;


        public ScenarioConditionList Conditions => conditions;

        public float CheckInterval
        {
            get => checkInterval;
            set => checkInterval = Mathf.Max(0f, value);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            // TODO этап 1, рантайм: Fire(Body), проверять условия по расписанию, Cancel → Broken, кончилось само → Done.
            yield break;
        }
    }
}
