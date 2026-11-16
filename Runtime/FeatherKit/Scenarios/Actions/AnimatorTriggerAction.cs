using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Дёрнуть триггер аниматора: дверь открывается, люк падает. Старое поле «animator» переносится в новое перед работой и кнопкой «Upgrade Fields».</summary>
    [Serializable]
    [ScenarioBlock("Animation/Animator Trigger", Description = "Fire a Trigger parameter of an Animator")]
    public class AnimatorTriggerAction : ScenarioInstantAction, IScenarioUpgradableBlock
    {
        [SerializeField, HideInInspector] private Animator animator;

        [Tooltip("Whose animator. Empty — the animator on the scenario object")]
        [ScenarioLabel("Animator")]
        [SerializeField] private ScenarioValue<Animator> targetAnimator = new ScenarioValue<Animator>();

        [Tooltip("Name of the trigger parameter, as in the Animator window")]
        [ScenarioLabel("Trigger")]
        [SerializeField] private string triggerName;


        public ScenarioValue<Animator> TargetAnimator
        {
            get
            {
                TryUpgradeLegacyFields();
                return targetAnimator;
            }
        }

        public override string Summary => string.IsNullOrEmpty(triggerName) ? "no trigger set" : triggerName;


        protected override void Apply(ScenarioRun run)
        {
            TryUpgradeLegacyFields();
            ScenarioAnimators.Require(run, targetAnimator).SetTrigger(triggerName);
        }


        // IScenarioUpgradableBlock
        // Новое поле главнее: задано — старое просто очищается, не затирая его.
        public bool TryUpgradeLegacyFields()
        {
            if (!ScenarioFieldMigration.IsAssigned(animator))
                return false;

            if (!ScenarioFieldMigration.IsAssigned(targetAnimator.Value) && !targetAnimator.UsesVariable)
                targetAnimator.Value = animator;

            animator = null;

            return true;
        }
    }
}
