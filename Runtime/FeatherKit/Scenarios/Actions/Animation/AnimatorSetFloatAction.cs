using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить параметр Float аниматора: скорость, сила дыхания, приоткрытость двери.</summary>
    [Serializable]
    [ScenarioBlock("Animation/Animator Set Float", Description = "Set a Float parameter of an Animator")]
    public class AnimatorSetFloatAction : ScenarioInstantAction
    {
        [Tooltip("Whose animator. Empty — the animator on the scenario object")]
        [ScenarioLabel("Animator")]
        [SerializeField] private ScenarioValue<Animator> animator = new ScenarioValue<Animator>();

        [Tooltip("Name of the Float parameter, as in the Animator window")]
        [ScenarioLabel("Parameter")]
        [SerializeField] private string parameterName;

        [ScenarioLabel("Value")]
        [SerializeField] private ScenarioValue<float> value = new ScenarioValue<float>();


        public ScenarioValue<Animator> TargetAnimator => animator;

        public ScenarioValue<float> Value => value;

        public override string Summary => string.IsNullOrEmpty(parameterName) ? "no parameter set"
            : value.UsesVariable ? $"{parameterName} = variable" : $"{parameterName} = {value.Value:0.##}";


        protected override void Apply(ScenarioRun run)
        {
            ScenarioAnimators.Require(run, animator).SetFloat(parameterName, value.Get(run));
        }
    }
}
