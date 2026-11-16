using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FeatherKit.Scenarios
{
    /// <summary>Нода «Random»: идёт в один из выходов Out 1..N — с вероятностью по весу выхода. Все веса нулевые — никуда.</summary>
    [Serializable]
    [ScenarioBlock("Flow/Random", Description = "Go to one of the outputs at random, by weight")]
    public class ScenarioRandomNode : ScenarioFlowNode
    {
        private const string OutIdPrefix = "out";

        [Tooltip("One weight per output: an output with weight 2 comes up twice as often as one with weight 1")]
        [ScenarioLabel("Weights")]
        [SerializeField] private List<float> weights = new List<float> { 1f, 1f };


        public List<float> Weights => weights;


        public static ScenarioOutput OutOutput(int number)
        {
            return ScenarioNumberedOutputs.Create(OutIdPrefix, "Out", number);
        }


        public override void DescribePorts(ScenarioPortList ports)
        {
            ScenarioNumberedOutputs.Describe(this, ports, OutIdPrefix, "Out", weights.Count);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var chosenNumber = ChooseOutputNumber();

            if (chosenNumber > 0)
                Fire(run, OutOutput(chosenNumber));

            yield break;
        }

        private int ChooseOutputNumber()
        {
            var total = 0f;

            foreach (var weight in weights)
                total += Mathf.Max(0f, weight);

            if (total <= 0f)
                return 0;

            var roll = Random.value * total;

            for (var index = 0; index < weights.Count; index++)
            {
                roll -= Mathf.Max(0f, weights[index]);

                if (roll < 0f)
                    return index + 1;
            }

            return weights.Count;
        }
    }
}
