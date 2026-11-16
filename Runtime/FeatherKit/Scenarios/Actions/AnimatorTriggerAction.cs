using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Дёрнуть триггер аниматора: дверь открывается, люк падает. Старое поле «animator» переносится в новое при загрузке.</summary>
    [Serializable]
    [ScenarioBlock("Animation/Animator Trigger", Description = "Fire a Trigger parameter of an Animator")]
    public class AnimatorTriggerAction : ScenarioInstantAction, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private Animator animator;

        [Tooltip("Whose animator. Empty — the animator on the scenario object")]
        [ScenarioLabel("Animator")]
        [SerializeField] private ScenarioValue<Animator> targetAnimator = new ScenarioValue<Animator>();

        [Tooltip("Name of the trigger parameter, as in the Animator window")]
        [ScenarioLabel("Trigger")]
        [SerializeField] private string triggerName;


        public ScenarioValue<Animator> TargetAnimator => targetAnimator;

        public override string Summary => string.IsNullOrEmpty(triggerName) ? "no trigger set" : triggerName;


        protected override void Apply(ScenarioRun run)
        {
            ScenarioAnimators.Require(run, targetAnimator).SetTrigger(triggerName);
        }


        // ISerializationCallbackReceiver
        public void OnBeforeSerialize()
        {
        }

        // ISerializationCallbackReceiver
        public void OnAfterDeserialize()
        {
            targetAnimator ??= new ScenarioValue<Animator>();

            if (ScenarioFieldMigration.IsAssigned(animator) && !ScenarioFieldMigration.IsAssigned(targetAnimator.Value) && !targetAnimator.UsesVariable)
                targetAnimator.Value = animator;

            animator = null;
        }
    }
}
