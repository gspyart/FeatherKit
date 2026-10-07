using System;
using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Прыжок элемента интерфейса: наплыв размером в ответ на хорошее — жест «прибыло».
    /// Вдавливание на нажатие — <see cref="Press"/>.
    /// - запоминает домашний размер и возвращает его;
    /// - прыгает по кривой, ужимаясь вместе со спешкой просящего.
    /// Полем внутри того, кто прыгает: он решает, когда.
    /// </summary>
    [Serializable]
    public class UiBounce
    {
        // Отклик кнопки на нажатие — один на всю игру: вкладки, меню и кнопка сумки
        // должны вдавливаться одинаково.
        private const float PressPeak = 0.85f;
        private const float PressDuration = 0.2f;

        [Tooltip("Размер по ходу прыжка: 1 — свой размер, больше — наплыв")]
        [SerializeField] private AnimationCurve scale = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 1.22f), new Keyframe(1f, 1f));

        [Tooltip("Сколько длится прыжок, сек. Просящий может поторопить его, и тогда " +
                 "прыжок ужимается вместе с ним")]
        [Min(0.05f)]
        [SerializeField] private float duration = 0.3f;

        private Transform target;
        private Vector3 home = Vector3.one;
        private float time;

        // Сколько длится ЭТОТ прыжок: свой срок, помноженный на спешку ленты.
        private float playDuration;


        public UiBounce()
        {
        }

        /// <summary>
        /// Прыжок со своим пиком: больше единицы — наплыв, меньше — вдавливание.
        /// Заданное здесь — только начальное значение, дальше его правят в инспекторе.
        /// </summary>
        public UiBounce(float peak, float duration)
        {
            scale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, peak), new Keyframe(1f, 1f));

            this.duration = duration;
        }


        /// <summary>Вдавливание под пальцем: так кнопки игры отвечают на нажатие.</summary>
        public static UiBounce Press() => new UiBounce(PressPeak, PressDuration);


        // ── Прыжок ────────────────────────────────────────────────────────

        /// <summary>Кто прыгает. Тем же разом запоминаем его домашний размер.</summary>
        public void Attach(Transform value)
        {
            if (target == value)
                return;

            Rest();

            target = value;
            home = target != null ? target.localScale : Vector3.one;
        }

        /// <summary>
        /// <paramref name="hurry"/> — во сколько раз короче обычного. Повтор начинает заново,
        /// а не складывается: две монеты подряд — два одинаковых ответа.
        /// </summary>
        public void Play(float hurry = 1f)
        {
            if (target == null || duration <= 0f)
                return;

            playDuration = duration * Mathf.Max(0.05f, hurry);
            time = 0f;
        }

        public void Tick(float delta)
        {
            if (playDuration <= 0f)
                return;

            time += delta;

            var t = Mathf.Clamp01(time / playDuration);

            target.localScale = home * scale.Evaluate(t);

            if (t >= 1f)
                Rest();
        }

        /// <summary>Вернуть домашний размер: прыжок кончился или владельца выключили.</summary>
        public void Rest()
        {
            playDuration = 0f;
            time = 0f;

            if (target != null)
                target.localScale = home;
        }
    }
}
