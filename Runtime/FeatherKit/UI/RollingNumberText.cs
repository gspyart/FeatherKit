using System;
using System.Globalization;
using FeatherKit.Attributes;
using FeatherKit.Helpers;
using TMPro;
using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Подпись с бегущим числом: новое значение не подставляется разом, а докручивается
    /// от прежнего — так видно и то, что число изменилось, и в какую сторону.
    /// Умеет: догонять названное значение за заданное время, красить число на росте
    /// и на убыли, возвращать обычный цвет, когда докрутило, сокращать большие числа.
    /// Откуда число, не знает: его называют снаружи.
    /// </summary>
    [FDescription("Подпись с бегущим числом: докручивается от прежнего значения к новому и красит рост и убыль.")]
    public class RollingNumberText : MonoBehaviour
    {
        [Tooltip("Сама подпись. Пусто — берётся с этого же объекта")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Формат числа: 0 — просто цифры, N0 — с разделителем тысяч")]
        [SerializeField] private string format = "0";

        [Tooltip("Сокращать большие числа: 101000 → 101K. Для счётчиков в тесных плашках; " +
                 "формат выше тогда не используется")]
        [SerializeField] private bool shortened;

        [Header("Прокрутка")]
        [Tooltip("За сколько секунд число догоняет новое значение. Ноль — меняется разом")]
        [Min(0f)]
        [SerializeField] private float rollTime = 0.4f;

        [Header("Подкраска")]
        [Tooltip("Красить число, пока оно бежит: вверх — одним цветом, вниз — другим. " +
                 "Выключено — цвет подписи не трогаем вовсе")]
        [SerializeField] private bool tintWhileRolling = true;

        [Tooltip("Цвет растущего числа")]
        [SerializeField] private Color growColor = new Color(0.36f, 0.84f, 0.42f);

        [Tooltip("Цвет убывающего числа")]
        [SerializeField] private Color fallColor = new Color(0.90f, 0.27f, 0.22f);

        [Tooltip("За сколько секунд цвет возвращается к обычному после остановки")]
        [Min(0f)]
        [SerializeField] private float tintFadeTime = 0.35f;

        private long target;

        // Показанное число — дробное: за кадр прокрутка сдвигается меньше чем на единицу.
        private double shown;
        private double rollFrom;
        private float rollTimePassed;
        private bool rolling;

        // Обычный цвет подписи — тот, что стоял на ней при первом показе.
        private Color homeColor = Color.white;
        private Color rollColor = Color.white;
        private float tintLeft;

        private bool drawn;


        /// <summary>К какому числу идём. Это и есть последнее названное значение.</summary>
        public long Value => target;

        /// <summary>Число, которое сейчас на подписи: прокрутка за ним отстаёт.</summary>
        public long Shown => (long)Math.Round(shown);


        // ── Показ ─────────────────────────────────────────────────────────

        /// <summary>
        /// Назвать число. Первое за жизнь подписи ставится разом, без прокрутки: показывать
        /// разгон от нуля при появлении плашки не за что.
        /// </summary>
        public void SetValue(long value, bool animated = true)
        {
            if (drawn && value == target)
                return;

            var wasShown = Shown;

            target = value;

            if (!animated || !drawn || rollTime <= 0f)
            {
                Stop();

                return;
            }

            // Вернулись к тому, что на подписи и так стоит: за кадр правок бывает
            // несколько, и игрок видел только первую из них — красить нечего.
            if (value == wasShown)
            {
                Stop();

                return;
            }

            rollFrom = shown;
            rollTimePassed = 0f;
            rolling = true;

            // Сторона — от ПОКАЗАННОГО числа, а не от прежде названного: иначе размен
            // «отняли и тут же прибавили» читается как заработок.
            rollColor = value > wasShown ? growColor : fallColor;
            tintLeft = tintFadeTime;

            Draw();
        }

        /// <summary>Досрочно оказаться на названном числе: без прокрутки и без подкраски.</summary>
        public void Stop()
        {
            shown = target;
            rolling = false;
            tintLeft = 0f;

            Draw();
        }

        private void Draw()
        {
            if (label == null)
                return;

            drawn = true;

            var number = (long)Math.Round(shown);

            label.text = shortened ? number.ToShortText() : number.ToString(format, CultureInfo.InvariantCulture);

            if (!tintWhileRolling)
                return;

            // Пока бежим — цвет стороны, дальше он возвращается к обычному.
            label.color = rolling
                ? rollColor
                : tintFadeTime <= 0f
                    ? homeColor
                    : Color.Lerp(homeColor, rollColor, tintLeft / tintFadeTime);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (label == null)
                label = GetComponent<TMP_Text>();

            // Обычный цвет — тот, с которым подпись собрали: вернуться после подкраски
            // больше не к чему.
            if (label != null)
                homeColor = label.color;
        }

        // Время НЕигровое: добивание замедляет мир, а прокрутка числа — это фидбек,
        // и доигрывать она должна с обычной скоростью.
        private void Update()
        {
            if (!rolling && tintLeft <= 0f)
                return;

            var delta = Time.unscaledDeltaTime;

            if (rolling)
            {
                rollTimePassed += delta;

                var share = Mathf.Clamp01(rollTimePassed / rollTime);

                shown = rollFrom + (target - rollFrom) * Mathf.SmoothStep(0f, 1f, share);

                if (share >= 1f)
                {
                    shown = target;
                    rolling = false;
                }
            }
            else
            {
                tintLeft = Mathf.Max(0f, tintLeft - delta);
            }

            Draw();
        }
    }
}
