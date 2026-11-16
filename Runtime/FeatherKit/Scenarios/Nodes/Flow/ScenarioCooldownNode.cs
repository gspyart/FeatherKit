using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Cooldown»: пропускает в Out не чаще раза в N секунд игрового времени; слишком рано — в Blocked.
    /// Время последнего прохода общее для ноды, а не для запуска; Stop его забывает.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Cooldown", Description = "Let the flow through at most once per N seconds")]
    public class ScenarioCooldownNode : ScenarioFlowNode, IScenarioLiveNode
    {
        public static readonly ScenarioOutput Out = new ScenarioOutput("out", "Out");
        public static readonly ScenarioOutput Blocked = new ScenarioOutput("blocked", "Blocked");

        [ScenarioLabel("Seconds")]
        [Min(0f)]
        [SerializeField] private float seconds = 2f;

        [NonSerialized] private bool hasPassed;
        [NonSerialized] private float lastPassTime;


        public float Seconds
        {
            get => seconds;
            set => seconds = Mathf.Max(0f, value);
        }

        public override string Summary => $"{seconds:0.##} s";


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (hasPassed && Time.time - lastPassTime < seconds)
            {
                Fire(run, Blocked);
                yield break;
            }

            hasPassed = true;
            lastPassTime = Time.time;
            Fire(run, Out);
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            hasPassed = false;
        }
    }
}
