using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Дёрнуть триггер аниматора: дверь открывается, люк падает.</summary>
    [Serializable]
    [ScenarioBlock("Анимация/Триггер аниматора")]
    public class AnimatorTriggerAction : ScenarioInstantAction
    {
        [Tooltip("Чей аниматор. Пусто — аниматор на объекте сценария")]
        [ScenarioLabel("Аниматор")]
        [SerializeField] private Animator animator;

        [Tooltip("Имя параметра-триггера в аниматоре, как в окне Animator")]
        [ScenarioLabel("Триггер")]
        [SerializeField] private string triggerName;


        public override string Summary => string.IsNullOrEmpty(triggerName) ? "триггер не задан" : triggerName;


        protected override void Apply(ScenarioRun run)
        {
            var targetAnimator = run.ComponentOrOwners(animator);

            if (targetAnimator == null)
                throw new InvalidOperationException("Аниматор не найден");

            targetAnimator.SetTrigger(triggerName);
        }
    }
}
