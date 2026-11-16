using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Аниматор блока: из поля или с объекта сценария; нет — ошибка, а не тихий пропуск.</summary>
    public static class ScenarioAnimators
    {
        public static Animator Require(ScenarioRun run, ScenarioValue<Animator> animator)
        {
            var target = run.ComponentOrOwners(animator.Get(run));

            if (target == null)
                throw new InvalidOperationException("Animator not found");

            return target;
        }
    }
}
