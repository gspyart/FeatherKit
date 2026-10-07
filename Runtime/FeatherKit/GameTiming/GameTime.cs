using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.GameTiming
{
    /// <summary>
    /// Игровое время: пауза и скорость. Обёртка над UnityEngine.Time, чтобы игровой код
    /// не решал сам, идёт ли время и с какой скоростью — он просто берёт GameTime.DeltaTime.
    ///
    /// Два времени, и это главное:
    /// DeltaTime — геймплейное, слушается паузы и замедления;
    /// UnscaledDeltaTime — реальное, для всего, что обязано идти всегда: UI, попапы, фидбек,
    /// сама анимация замедления.
    ///
    /// Пауза считается по ИСТОЧНИКАМ, а не флагом: автомат и экран результата могут попросить
    /// её одновременно, и снятие одним не должно отпускать игру, пока держит другой.
    ///
    /// Скорость и пауза — разные вещи и не мешают друг другу: снятие паузы вернёт ту скорость,
    /// что была до неё, а не единицу.
    /// </summary>
    public static class GameTime
    {
        private static readonly HashSet<object> pauseSources = new HashSet<object>();

        // Шаг физики из настроек проекта. Считываем один раз: свою константу сюда писать
        // нельзя — затрёт то, что выставил проект.
        private static float projectFixedStep = UnityEngine.Time.fixedDeltaTime;
        private static bool fixedStepCaptured;

        private static float scale = 1f;
        private static float targetScale = 1f;
        private static float blendFrom = 1f;
        private static float blendDuration;
        private static float blendElapsed;
        private static float holdLeft;
        private static float holdReturnBlend;
        private static bool holding;


        public static event Action<bool> PauseChanged;

        public static event Action<float> ScaleChanged;


        public static bool IsPaused => pauseSources.Count > 0;

        /// <summary>Текущая скорость игры: 1 — обычная, 0.3 — замедление, 2 — ускорение.</summary>
        public static float Scale => scale;

        /// <summary>Время геймплея: на паузе ноль, в замедлении — меньше реального.</summary>
        public static float DeltaTime => IsPaused ? 0f : UnityEngine.Time.deltaTime;

        /// <summary>Шаг физики геймплея: на паузе ноль.</summary>
        public static float FixedDeltaTime => IsPaused ? 0f : UnityEngine.Time.fixedDeltaTime;

        /// <summary>Реальное время. Не зависит ни от паузы, ни от замедления.</summary>
        public static float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;


        /// <summary>Попросить паузу. Источник — обычно this того, кто просит.</summary>
        public static void Pause(object source)
        {
            if (source == null)
                return;

            var wasPaused = IsPaused;

            if (pauseSources.Add(source) && !wasPaused)
                ApplyPause(true);
        }

        public static void Resume(object source)
        {
            if (source == null || !pauseSources.Remove(source))
                return;

            if (!IsPaused)
                ApplyPause(false);
        }

        /// <summary>
        /// Снять все паузы. Нужно при выгрузке сцены: источник мог уехать вместе с ней,
        /// не отпустив паузу, и следующая сцена осталась бы замороженной.
        /// </summary>
        public static void ForceResume()
        {
            if (pauseSources.Count == 0)
                return;

            pauseSources.Clear();
            ApplyPause(false);
        }


        /// <summary>
        /// Задать скорость игры. blendDuration — за сколько реальных секунд доехать до неё;
        /// ноль означает мгновенно. Плавный переход нужен почти всегда: рывок скорости
        /// читается как лаг, а не как эффект.
        /// </summary>
        public static void SetScale(float value, float blendDuration = 0f)
        {
            holding = false;

            StartBlend(Mathf.Max(value, 0f), blendDuration);
        }

        /// <summary>Вернуть обычную скорость.</summary>
        public static void ResetScale(float blendDuration = 0f)
        {
            SetScale(1f, blendDuration);
        }

        /// <summary>
        /// Замедление на время, с автоматическим возвратом — попадание по боссу, добивание,
        /// вход в замес. Длительность считается в реальных секундах: иначе замедление
        /// растягивало бы само себя.
        /// </summary>
        public static void SlowMotion(float value, float duration, float blendDuration = 0.08f)
        {
            SetScale(value, blendDuration);

            holding = true;
            holdLeft = Mathf.Max(duration, 0f);
            holdReturnBlend = blendDuration;
        }

        /// <summary>
        /// Сброс при входе в игру. Нужен из-за того, что класс статический: с выключенной
        /// перезагрузкой домена (Enter Play Mode Options) поля переживают выход из плей-мода,
        /// и следующий запуск начался бы с чужой паузой или замедлением.
        ///
        /// SubsystemRegistration — самая ранняя точка, до Awake любой сцены.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            // Шаг физики запоминаем при самом первом запуске: к следующему мы его уже
            // масштабировали, и читать текущее значение значило бы запомнить масштабированное.
            if (!fixedStepCaptured)
            {
                projectFixedStep = UnityEngine.Time.fixedDeltaTime;
                fixedStepCaptured = true;
            }

            pauseSources.Clear();
            PauseChanged = null;
            ScaleChanged = null;

            scale = 1f;
            targetScale = 1f;
            blendFrom = 1f;
            blendDuration = 0f;
            blendElapsed = 0f;
            holdLeft = 0f;
            holdReturnBlend = 0f;
            holding = false;

            ApplyToUnity(1f);
        }


        /// <summary>Тикается раннером апдейта по реальному времени. Игровому коду звать не надо.</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            TickBlend(unscaledDeltaTime);
            TickHold(unscaledDeltaTime);
        }


        private static void StartBlend(float value, float duration)
        {
            targetScale = value;

            if (duration <= 0f)
            {
                blendDuration = 0f;
                SetScaleImmediate(value);
                return;
            }

            blendFrom = scale;
            blendDuration = duration;
            blendElapsed = 0f;
        }

        private static void TickBlend(float unscaledDeltaTime)
        {
            if (blendDuration <= 0f)
                return;

            blendElapsed += unscaledDeltaTime;

            var t = Mathf.Clamp01(blendElapsed / blendDuration);
            var finished = t >= 1f;

            if (finished)
                blendDuration = 0f;

            // На последнем шаге ставим ровно цель и настаиваем на этом: иначе Approximately
            // ниже съест остаток перехода, и скорость навсегда останется «почти единицей».
            SetScaleImmediate(finished ? targetScale : Mathf.Lerp(blendFrom, targetScale, t), finished);
        }

        // Отсчёт удержания идёт только когда доехали до нужной скорости — иначе короткое
        // замедление успело бы кончиться, ещё не начавшись.
        private static void TickHold(float unscaledDeltaTime)
        {
            if (!holding || blendDuration > 0f)
                return;

            holdLeft -= unscaledDeltaTime;

            if (holdLeft > 0f)
                return;

            holding = false;
            StartBlend(1f, holdReturnBlend);
        }

        private static void SetScaleImmediate(float value, bool force = false)
        {
            // Approximately, чтобы не дёргать событие на каждую тысячную перехода.
            // force — для последнего шага: там надо встать ровно на цель.
            if (!force && Mathf.Approximately(scale, value))
                return;

            if (scale == value)
                return;

            scale = value;

            if (!IsPaused)
                ApplyToUnity(scale);

            ScaleChanged?.Invoke(scale);
        }

        // timeScale трогаем здесь и только здесь. Он нужен, чтобы вместе с игровым временем
        // замедлились физика и аниматоры — их своим дельтатаймом не накормишь.
        private static void ApplyPause(bool paused)
        {
            ApplyToUnity(paused ? 0f : scale);

            PauseChanged?.Invoke(paused);
        }

        // Шаг физики двигаем следом за скоростью: без этого в замедлении физика считается
        // теми же большими шагами, только реже, и движение начинает дёргаться.
        //
        // На паузе шаг НЕ трогаем: физика всё равно стоит, а крошечное значение опасно —
        // стоит скорости вернуться раньше шага, и Unity попытается досчитать тысячи шагов.
        private static void ApplyToUnity(float value)
        {
            UnityEngine.Time.timeScale = value;

            if (value <= 0f)
                return;

            // На обычной скорости возвращаем РОВНО шаг из настроек проекта, а не результат
            // умножения на «почти единицу». Unity записывает fixedDeltaTime обратно
            // в TimeManager.asset, поэтому такой остаток не исчезал бы с выходом из плей-мода:
            // настройка проекта уползала бы на доли процента с каждым запуском.
            UnityEngine.Time.fixedDeltaTime = Mathf.Approximately(value, 1f)
                ? projectFixedStep
                : projectFixedStep * value;
        }
    }
}
