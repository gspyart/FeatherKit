using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Дёрнуть триггер аниматора: дверь открывается, люк падает.</summary>
    [Serializable]
    [ScenarioBlock("Animation/Animator Trigger")]
    public class AnimatorTriggerAction : ScenarioInstantAction
    {
        [Tooltip("Whose animator. Empty — the animator on the scenario object")]
        [ScenarioLabel("Animator")]
        [SerializeField] private Animator animator;

        [Tooltip("Name of the trigger parameter, as in the Animator window")]
        [ScenarioLabel("Trigger")]
        [SerializeField] private string triggerName;


        public override string Summary => string.IsNullOrEmpty(triggerName) ? "no trigger set" : triggerName;


        protected override void Apply(ScenarioRun run)
        {
            var targetAnimator = run.ComponentOrOwners(animator);

            if (targetAnimator == null)
                throw new InvalidOperationException("Animator not found");

            targetAnimator.SetTrigger(triggerName);
        }
    }
}
