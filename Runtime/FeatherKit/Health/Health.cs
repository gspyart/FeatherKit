using System;
using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Health
{
    /// <summary>
    /// Запас здоровья: число, которое меняется двумя способами — текущее и максимум.
    ///
    /// Про урон тут нет ни слова намеренно — что такое урон, в каждой игре своё.
    /// Не знает и на ком висит: враг, игрок, ящик, дверь.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Tooltip("Запас здоровья на старте и потолок для лечения")]
        [SerializeField] private int maxHealth = 100;

        private int current;
        private IObjectHeight objectHeight;


        /// <summary>Сколько сняли.</summary>
        public event Action<int> Damaged;

        /// <summary>Сколько восстановили.</summary>
        public event Action<int> Healed;

        public event Action Died;


        public int Current => current;

        public int Max => maxHealth;

        public bool IsAlive => current > 0;


        /// <summary>
        /// Изменить текущее здоровье. Результат сам обрезается по границам, поэтому
        /// вычитание ниже нуля и лечение выше максимума безопасны.
        /// </summary>
        public void ModifyHealth(HealthChange change, float value)
        {
            if (!IsAlive && change != HealthChange.Set)
                return;

            var next = Mathf.Clamp(Calculate(current, change, value), 0, maxHealth);
            var delta = next - current;

            if (delta == 0)
                return;

            current = next;

            Report(delta);

            if (current <= 0)
                Died?.Invoke();
        }

        /// <summary>
        /// Изменить максимум. Текущее едет следом на ту же величину: «+20 к запасу»
        /// обычно значит и «+20 сейчас», иначе прибавка не чувствуется.
        /// </summary>
        public void ModifyMaxHealth(HealthChange change, float value)
        {
            var next = Mathf.Max(1, Calculate(maxHealth, change, value));
            var delta = next - maxHealth;

            maxHealth = next;

            current = Mathf.Clamp(current + Mathf.Max(0, delta), 0, maxHealth);
        }

        /// <summary>Полный запас — при спавне и на старте забега.</summary>
        public void ResetState()
        {
            current = maxHealth;
        }


        private static int Calculate(int from, HealthChange change, float value)
        {
            switch (change)
            {
                case HealthChange.Add: return from + Mathf.RoundToInt(value);
                case HealthChange.Subtract: return from - Mathf.RoundToInt(value);
                case HealthChange.Multiply: return Mathf.RoundToInt(from * value);
                default: return Mathf.RoundToInt(value);
            }
        }

        private void Report(int delta)
        {
            if (delta < 0)
                Damaged?.Invoke(-delta);
            else
                Healed?.Invoke(delta);
            
            // В шину — для тех, кто один на весь экран и на каждого подписаться не может.
            // Точку считаем здесь: рост знает наш же объект, а подписчик про него не знает.
            Events.EventBus.Publish(new Events.HealthChangedEvent
            {
                Target = this,
                Point = objectHeight != null ? objectHeight.HeadPoint : transform.position,
                Delta = delta,
            });
        }


        protected virtual void Awake()
        {
            current = maxHealth;

            // Рост ищем один раз: он приезжает компонентом на том же объекте и меняться
            // ему незачем, а спрашивают точку на каждом попадании.
            objectHeight = GetComponent<IObjectHeight>();
        }
    }
}
