using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Короткое дрожание элемента интерфейса — жест «нельзя». Не влезло в сумку, надеть
    /// не выходит, кнопка недоступна: действие не случилось, и сказать об этом надо там,
    /// куда игрок и смотрит — под его пальцем.
    ///
    /// Трясёт ПОВОРОТОМ, а не позицией, и это не прихоть: элементы интерфейса почти всегда
    /// лежат в раскладке (Grid, Horizontal), а она распоряжается их позицией сама
    /// и перетирает чужие правки на первом же пересчёте. Поворот раскладке не нужен —
    /// значит он наш.
    ///
    /// Без DOTween: своё затухающее колебание — это десяток строк, а тянуть в кит сторонний
    /// пакет ради них значит привязать к нему всю библиотеку. В игре DOTween остаётся
    /// там, где он уже есть.
    ///
    /// Время НЕигровое: удары замедляют игру, а интерфейс замедляться вместе с ней
    /// не должен.
    /// </summary>
    [FDescription("Короткое дрожание элемента — жест «нельзя». Зовут, когда действие не вышло: не влезло, нельзя надеть.")]
    public class UiShake : MonoBehaviour
    {
        [Tooltip("Насколько сильно мотнёт, градусов")]
        [SerializeField] private float strength = 8f;

        [Tooltip("Сколько длится, сек")]
        [Min(0f)]
        [SerializeField] private float duration = 0.25f;

        [Tooltip("Сколько раз мотнёт туда-обратно за это время")]
        [Min(0.5f)]
        [SerializeField] private float cycles = 3f;

        private Quaternion home;
        private float timeLeft;


        public bool IsShaking => timeLeft > 0f;


        // ── Дрожание ──────────────────────────────────────────────────────

        /// <summary>
        /// Дрогнуть. Повторный вызов начинает заново, а не складывается со старым:
        /// два отказа подряд — это два одинаковых ответа, а не двойная амплитуда.
        /// </summary>
        public void Play()
        {
            if (duration <= 0f)
                return;

            timeLeft = duration;
        }

        /// <summary>
        /// Дрогнуть тем элементом, если он это умеет. Для тех, у кого на руках только
        /// сама ячейка или кнопка: спрашивать компонент на месте вызова — лишняя строка
        /// в каждом отказе.
        /// </summary>
        public static void Play(Component target)
        {
            var shake = target != null ? target.GetComponent<UiShake>() : null;

            if (shake != null)
                shake.Play();
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            home = transform.localRotation;
        }

        // Возвращаем на место и при выключении: элемент может уехать в пул или спрятаться
        // прямо посреди дрожания, а вернуться должен ровным.
        private void OnDisable()
        {
            timeLeft = 0f;

            transform.localRotation = home;
        }

        private void Update()
        {
            if (timeLeft <= 0f)
                return;

            timeLeft -= Time.unscaledDeltaTime;

            if (timeLeft <= 0f)
            {
                transform.localRotation = home;

                return;
            }

            // Затухает к концу: колебание с постоянной амплитудой обрывается рывком,
            // и глаз читает это как сбой, а не как ответ.
            var passed = 1f - timeLeft / duration;
            var fade = 1f - passed;
            var angle = Mathf.Sin(passed * Mathf.PI * 2f * cycles) * strength * fade;

            transform.localRotation = home * Quaternion.Euler(0f, 0f, angle);
        }
    }
}
