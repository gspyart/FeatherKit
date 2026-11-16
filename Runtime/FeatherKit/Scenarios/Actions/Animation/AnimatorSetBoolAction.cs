using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить параметр Bool аниматора: дверь «открыта», монстр «бежит».</summary>
    [Serializable]
    [ScenarioBlock("Animation/Animator Set Bool", Description = "Set a Bool parameter of an Animator")]
    public class AnimatorSetBoolAction : ScenarioInstantAction
    {
        [Tooltip("Whose animator. Empty — the animator on the scenario object")]
        [ScenarioLabel("Animator")]
        [SerializeField] private ScenarioValue<Animator> animator = new ScenarioValue<Animator>();

        [Tooltip("Name of the Bool parameter, as in the Animator window")]
        [ScenarioLabel("Parameter")]
        [SerializeField] private string parameterName;

        [ScenarioLabel("Value")]
        [SerializeField] private ScenarioValue<bool> value = new ScenarioValue<bool>(true);


        public ScenarioValue<Animator> TargetAnimator => animator;

        public ScenarioValue<bool> Value => value;

        public override string Summary => string.IsNullOrEmpty(parameterName) ? "no parameter set"
            : value.UsesVariable ? $"{parameterName} = variable" : $"{parameterName} = {value.Value}";


        protected override void Apply(ScenarioRun run)
        {
            ScenarioAnimators.Require(run, animator).SetBool(parameterName, value.Get(run));
        }
    }
}
