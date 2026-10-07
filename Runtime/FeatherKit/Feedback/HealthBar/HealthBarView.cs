using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Полоса здоровья с «хвостом» урона: сама шкала падает мгновенно, а за ней остаётся
    /// белая полоса от прежнего значения к новому. Она держится, пока по персонажу бьют,
    /// и уползает только когда тот отдышался — так видно, сколько сняли за серию попаданий,
    /// а не за последний удар.
    ///
    /// Собирается из трёх картинок, снизу вверх:
    ///   1. фон — статичный, скрипту не нужен;
    ///   2. хвост (trail) — белая полоса;
    ///   3. шкала (fill) — поверх хвоста, закрывает его левую часть.
    /// У обеих Image должен стоять Image Type = Filled с одинаковым origin, иначе
    /// хвост поедет не в ту сторону.
    ///
    /// Про Health не знает намеренно: принимает долю 0..1 и годится и игроку, и врагу.
    /// </summary>
    public class HealthBarView : MonoBehaviour
    {
        [Tooltip("Шкала текущего здоровья. Меняется мгновенно")]
        [SerializeField] private Image fill;

        [Tooltip("Хвост урона — та самая белая полоса. Отстаёт от шкалы и уползает с задержкой")]
        [SerializeField] private Image trail;

        [Tooltip("Сколько секунд без урона ждать, прежде чем убирать хвост. " +
                 "Каждое новое попадание отсчёт перезапускает")]
        [SerializeField] private float damageDelay = 1f;

        [Tooltip("Скорость сползания хвоста в долях полосы за секунду: 0.6 — вся полоса за ~1.7 сек")]
        [SerializeField] private float drainSpeed = 0.6f;

        [Tooltip("Скорость сползания после смерти. Ждать здесь нечего — добиваем сразу и быстро")]
        [SerializeField] private float deathDrainSpeed = 3f;

        [Header("Ширина от запаса здоровья")]
        [Tooltip("Что растягивать; если пусто — собственный RectTransform")]
        [SerializeField] private RectTransform widthTarget;

        [Tooltip("Диапазон запаса HP: сколько у самого хлипкого и сколько у самого жирного")]
        [SerializeField] private Vector2 healthRange = new Vector2(25f, 150f);

        [Tooltip("Во что он превращается: ширина полосы в пикселях для нижней и верхней границы диапазона. " +
                 "За пределами диапазона ширина упирается в край — иначе босс с 5000 HP растянул бы полосу на экран")]
        [SerializeField] private Vector2 widthRange = new Vector2(60f, 140f);

        private float displayed = 1f;
        private float trailValue = 1f;
        private float delayLeft;
        private bool dead;
        private int appliedMax = -1;
        private RectTransform selfRect;


        /// <summary>Контроллер двигает полосу за целью, поэтому её RectTransform нужен снаружи.</summary>
        public RectTransform SelfRect => selfRect != null ? selfRect : selfRect = transform as RectTransform;


        /// <summary>Текущее здоровье долей от максимума. Зовётся на каждое изменение HP.</summary>
        public void SetValue(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);

            if (normalized > displayed)
            {
                // Лечение: хвосту тут делать нечего, иначе он повис бы белым «долгом»
                // над только что восстановленным здоровьем.
                trailValue = normalized;
            }
            else if (normalized < displayed)
            {
                // Само значение хвоста не трогаем: он остаётся на самой ранней отметке серии,
                // поэтому три удара подряд показывают суммарную потерю, а не последнюю.
                delayLeft = damageDelay;
                dead = false;
            }

            displayed = normalized;
            Apply();
        }

        /// <summary>Смерть: задержку пропускаем и добиваем хвост быстрой анимацией до пустой полосы.</summary>
        public void SetDead()
        {
            dead = true;
            delayLeft = 0f;
            displayed = 0f;
            Apply();
        }

        /// <summary>
        /// Подогнать ширину под запас здоровья: у толстяка полоса длиннее, чем у мелочи,
        /// и это читается раньше, чем игрок успеет заметить скорость её убывания.
        /// </summary>
        public void SetMaxHealth(int max)
        {
            // Зовётся каждый кадр, поэтому первым делом сверяемся: запас меняется редко,
            // а SetSizeWithCurrentAnchors каждому бару на кадр — это лишний ребилд канваса.
            if (max == appliedMax)
                return;

            appliedMax = max;

            var target = widthTarget != null ? widthTarget : transform as RectTransform;
            if (target == null)
                return;

            // InverseLerp сам зажимает в 0..1, поэтому значения вне диапазона упираются в край.
            var t = Mathf.InverseLerp(healthRange.x, healthRange.y, max);
            var width = Mathf.Lerp(widthRange.x, widthRange.y, t);

            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        /// <summary>Мгновенно и без анимации — на старте и при возврате из пула.</summary>
        public void ResetTo(float normalized)
        {
            displayed = Mathf.Clamp01(normalized);
            trailValue = displayed;
            delayLeft = 0f;
            dead = false;
            Apply();
        }


        private void Update()
        {
            if (trailValue <= displayed)
                return;

            // Немасштабированное время: полоса должна доиграть и на паузе level-up,
            // и на слоу-мо при смерти — это UI, а не часть игровой симуляции.
            var deltaTime = Time.unscaledDeltaTime;

            if (!dead && delayLeft > 0f)
            {
                delayLeft -= deltaTime;
                return;
            }

            var speed = dead ? deathDrainSpeed : drainSpeed;

            trailValue = Mathf.MoveTowards(trailValue, displayed, speed * deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (fill != null)
                fill.fillAmount = displayed;

            if (trail != null)
                trail.fillAmount = trailValue;
        }
    }
}
