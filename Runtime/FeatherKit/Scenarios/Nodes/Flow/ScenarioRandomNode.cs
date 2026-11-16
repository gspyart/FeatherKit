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
            var chosenNumber = PickOutputNumber(weights, Random.value * TotalWeight(weights));

            if (chosenNumber > 0)
                Fire(run, OutOutput(chosenNumber));

            yield break;
        }


        /// <summary>
        /// Номер выхода по броску от 0 до суммы весов: первый с положительным весом, где накопленная сумма больше броска;
        /// бросок ровно на сумме — последний с положительным весом. 0 — все веса нулевые.
        /// </summary>
        public static int PickOutputNumber(IReadOnlyList<float> outputWeights, float roll)
        {
            var accumulated = 0f;
            var lastPositiveNumber = 0;

            for (var index = 0; index < outputWeights.Count; index++)
            {
                if (outputWeights[index] <= 0f)
                    continue;

                accumulated += outputWeights[index];
                lastPositiveNumber = index + 1;

                if (accumulated > roll)
                    return lastPositiveNumber;
            }

            return lastPositiveNumber;
        }

        private static float TotalWeight(IReadOnlyList<float> outputWeights)
        {
            var total = 0f;

            foreach (var weight in outputWeights)
                total += Mathf.Max(0f, weight);

            return total;
        }
    }
}
