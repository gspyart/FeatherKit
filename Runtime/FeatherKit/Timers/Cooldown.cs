using UnityEngine;

namespace FeatherKit.Timers
{
    /// <summary>
    /// Лёгкий таймер отсчёта без MonoBehaviour/Coroutine — кулдауны атак, таймеры волн.
    /// Тикает его владелец: так кулдаун замирает вместе с объектом, ушедшим в пул.
    /// </summary>
    public class Cooldown
    {
        private float duration;
        private float remaining;


        public float Duration => duration;
        public float Remaining => remaining;
        public float Progress => duration <= 0f ? 1f : 1f - Mathf.Clamp01(remaining / duration);
        public bool IsReady => remaining <= 0f;


        public Cooldown(float duration)
        {
            SetDuration(duration);
        }

        // Отрицательные значения приводятся к 0.
        public void SetDuration(float newDuration)
        {
            duration = Mathf.Max(0f, newDuration);
        }


        public void OnUpdate(float deltaTime)
        {
            if (remaining > 0f)
                remaining = Mathf.Max(0f, remaining - deltaTime);
        }

        // Запустить отсчёт заново на всю длительность.
        public void Reset()
        {
            remaining = duration;
        }

        // Сразу сделать готовым (например, первая атака без задержки).
        public void ForceReady()
        {
            remaining = 0f;
        }
    }
}
