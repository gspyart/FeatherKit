using UnityEngine;

namespace FeatherKit.Randomness
{
    /// <summary>
    /// Случайное число «от и до» с уклоном: куда чаще падать, к min или к max.
    /// Поле для инспектора: награда 50..200, но обычно ближе к 50. Каждое обращение к Value — новый бросок.
    ///
    /// Уклон 0.5 — равномерно, 0 — всегда min, 1 — всегда max. В среднем выпадает
    /// min + (max - min) * уклон, так что уклон читается как «ожидаемая доля пути от min к max».
    /// </summary>
    [System.Serializable]
    public class RandomRange
    {
        [SerializeField] private float min;
        [SerializeField] private float max;
        [Range(0f, 1f)]
        [SerializeField] private float bias = 0.5f;


        public RandomRange()
        {
        }

        public RandomRange(float min, float max, float bias = 0.5f)
        {
            this.min = min;
            this.max = max;
            this.bias = Mathf.Clamp01(bias);
        }


        public float Min => min;
        public float Max => max;
        public float Bias => bias;

        /// <summary>Новый бросок при каждом обращении.</summary>
        public float Value
        {
            get
            {
                if (bias <= 0f)
                    return min;

                if (bias >= 1f)
                    return max;

                // Степень подобрана так, что среднее от t равно уклону: при 0.5 — ровно Random.value.
                var exponent = (1f - bias) / bias;
                var t = Mathf.Pow(Random.value, exponent);

                return Mathf.Lerp(min, max, t);
            }
        }
    }
}
