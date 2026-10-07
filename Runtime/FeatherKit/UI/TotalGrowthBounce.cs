using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Сумма на плашке и прыжок на её рост: одно правило на все плашки с суммой.
    /// - первую сумму ставит разом: появление плашки — не заработок;
    /// - прибавку показывает бегом числа и прыжком, убыль — одним бегом;
    /// - доигрывает и гасит прыжок по просьбе владельца.
    /// Часть плашки: владелец создаёт её в Awake и зовёт из Update.
    /// </summary>
    public class TotalGrowthBounce
    {
        private readonly RollingNumberText totalLabel;
        private readonly UiBounce bounce;

        private bool drawnOnce;


        public TotalGrowthBounce(RollingNumberText totalLabel, UiBounce bounce, Transform bouncing)
        {
            this.totalLabel = totalLabel;
            this.bounce = bounce;

            bounce.Attach(bouncing);
        }


        // ── Сумма ─────────────────────────────────────────────────────────

        /// <summary>
        /// Растёт — прыгаем. Считаем от ПОКАЗАННОГО числа: размен шлёт две правки за кадр,
        /// и трата вместе с приходом — не прибавка.
        /// </summary>
        public void Show(long total)
        {
            if (totalLabel == null)
                return;

            var grew = drawnOnce && total > totalLabel.Shown;

            totalLabel.SetValue(total, drawnOnce);

            if (grew)
                bounce.Play();

            drawnOnce = true;
        }


        /// <summary>Следующую сумму поставить разом: сменился тот, чью сумму показываем.</summary>
        public void ShowNextInstantly()
        {
            drawnOnce = false;
        }


        // ── Прыжок ────────────────────────────────────────────────────────

        public void Tick(float delta)
        {
            bounce.Tick(delta);
        }

        /// <summary>Вернуть плашке её размер: владельца выключили посреди прыжка.</summary>
        public void Rest()
        {
            bounce.Rest();
        }
    }
}
