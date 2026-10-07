using UnityEngine;

namespace FeatherKit.Randomness
{
    /// <summary>
    /// Случайное число «вокруг центра»: центр и разброс в обе стороны, по желанию с границами.
    /// Поле для инспектора: урон 10 ± 2, задержка 1.5 ± 0.5. Каждое обращение к Value — новый бросок.
    ///
    /// Границы отдельно от разброса, потому что разброс симметричный, а ограничение — нет:
    /// «10 ± 15, но не меньше 0» одним разбросом не описать.
    /// </summary>
    [System.Serializable]
    public class RandomSpread
    {
        [SerializeField] private float center;
        [SerializeField] private float spread;
        [SerializeField] private bool isClamped;
        [SerializeField] private float clampMin;
        [SerializeField] private float clampMax;


        public RandomSpread()
        {
        }

        public RandomSpread(float center, float spread)
        {
            this.center = center;
            this.spread = Mathf.Abs(spread);
        }

        public RandomSpread(float center, float spread, float clampMin, float clampMax)
            : this(center, spread)
        {
            isClamped = true;
            this.clampMin = clampMin;
            this.clampMax = clampMax;
        }


        public float Center => center;
        public float Spread => spread;
        public bool IsClamped => isClamped;
        public float ClampMin => clampMin;
        public float ClampMax => clampMax;

        /// <summary>Наименьшее и наибольшее, что может выпасть, — с учётом границ. Для подписей «8-12».</summary>
        public float Min => Clamp(center - spread);
        public float Max => Clamp(center + spread);

        /// <summary>Новый бросок при каждом обращении.</summary>
        public float Value => Clamp(center + Random.Range(-spread, spread));


        // Границы в любом порядке: меньшая всегда снизу.
        private float Clamp(float value)
        {
            if (!isClamped)
                return value;

            return Mathf.Clamp(value, Mathf.Min(clampMin, clampMax), Mathf.Max(clampMin, clampMax));
        }
    }
}
