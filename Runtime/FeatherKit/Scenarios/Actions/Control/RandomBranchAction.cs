using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Случайно одна из веток — с весами: ветка с весом 2 выпадает вдвое чаще ветки с весом 1.</summary>
    [Serializable]
    [ScenarioBlock("Управление/Случайно одно из")]
    public class RandomBranchAction : ScenarioAction
    {
        [ScenarioLabel("Ветки")]
        [SerializeField] private List<WeightedActionBranch> branches = new List<WeightedActionBranch>();


        public override string Summary => $"веток {branches.Count}";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var branch = PickBranch();

            if (branch != null)
                yield return branch.Actions.Perform(run);
        }

        private WeightedActionBranch PickBranch()
        {
            var totalWeight = branches.Sum(branch => Mathf.Max(0f, branch.Weight));

            if (totalWeight <= 0f)
                return null;

            var roll = UnityEngine.Random.value * totalWeight;

            foreach (var branch in branches)
            {
                roll -= Mathf.Max(0f, branch.Weight);

                if (roll < 0f)
                    return branch;
            }

            return branches[branches.Count - 1];
        }
    }


    /// <summary>Ветка «случайно одно из»: её шаги и насколько часто она выпадает.</summary>
    [Serializable]
    public class WeightedActionBranch
    {
        [Tooltip("Насколько часто выпадает относительно других веток. 0 — никогда")]
        [Min(0f)]
        [ScenarioLabel("Вес")]
        [SerializeField] private float weight = 1f;


        [ScenarioLabel("Шаги")]
        [SerializeField] private ScenarioActionList actions = new ScenarioActionList();


        public float Weight => weight;

        public ScenarioActionList Actions => actions;
    }
}
