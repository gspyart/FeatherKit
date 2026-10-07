using System.Collections;
using FeatherKit.Coroutines;
using FeatherKit.Lifecycle;
using UnityEngine;

namespace FeatherKit.Scenes
{
    /// <summary>
    /// Переезд на другой уровень целиком: затемнить экран, загрузить, проявить обратно.
    ///
    /// Ради этого класса вызывающие и перестают звать <see cref="SceneLoader"/> напрямую:
    /// правило «перед сменой сцены экран гаснет» живёт в одном месте, а не переписывается
    /// в каждой двери, портале и зоне выхода. Забыть его теперь нельзя.
    ///
    /// Второй раз посреди переезда не срабатывает: между нажатием и сменой сцены проходит
    /// почти секунда, и за это время игрок успевает нажать ещё раз.
    ///
    /// Занавеса может и не быть — тогда переезд просто мгновенный. Игра без него
    /// работает, только моргает.
    ///
    /// Держит закрытый экран не меньше заданного срока: сколько именно, говорит занавес —
    /// там же настраивают и сам экран загрузки. Ему же отдаёт ход загрузки для полоски.
    /// </summary>
    public class LevelTransition : IReleasable
    {
        // Сцена собирается не мгновенно: поднимается контекст, встаёт игрок, выходят мобы
        // из пула. Открой занавес в тот же кадр — игрок увидит эту сборку.
        private const int SettleFrames = 2;

        private readonly SceneLoader scenes;
        private readonly CoroutineRunner coroutines;
        private readonly ScreenCurtain curtain;

        private CoroutineToken routine;


        // Занавес приходит снаружи, из префаба игры.
        public LevelTransition(SceneLoader scenes, CoroutineRunner coroutines, ScreenCurtain curtain)
        {
            this.scenes = scenes;
            this.coroutines = coroutines;
            this.curtain = curtain;
        }


        /// <summary>Идёт ли переезд прямо сейчас.</summary>
        public bool IsRunning { get; private set; }

        // Занавеса нет — переезд и так мгновенный, выдерживать нечего.
        private float MinimumDuration => curtain != null ? curtain.MinimumLoadingTime : 0f;


        // ── Переезд ───────────────────────────────────────────────────────

        /// <summary>Уехать на уровень: экран гаснет, сцена меняется, экран проявляется.</summary>
        public void Go(LevelConfig level)
        {
            if (level == null || coroutines == null || scenes == null || IsRunning)
                return;

            IsRunning = true;
            routine = coroutines.Run(GoRoutine(level));
        }

        private IEnumerator GoRoutine(LevelConfig level)
        {
            if (curtain != null)
            {
                var closed = false;
                curtain.Close(() => closed = true);

                while (!closed)
                    yield return null;
            }

            // Срок считаем с закрытого экрана, а не с нажатия: держать надо ровно то,
            // что игрок видит, — сам экран загрузки.
            var closedAt = Time.unscaledTime;

            // Готовая сцена ждёт выключенной до конца срока: иначе её Start — спавн, катсцена,
            // таймер забега — отыграл бы под закрытым экраном, и игрок увидел бы уже середину.
            // Через пустоту: экран закрыт, и держать оба уровня разом в памяти незачем.
            var loaded = false;
            var started = scenes.UnloadAndLoad(level, () => loaded = true,
                () => Time.unscaledTime - closedAt >= MinimumDuration);

            // Загрузчик мог и отказаться: сцены нет в сборке, идёт другая загрузка.
            // Тогда ждать нечего — иначе экран остался бы чёрным навсегда.
            if (!started)
            {
                Debug.LogWarning($"LevelTransition: «{level.DisplayName}» не загружается — " +
                                 "открываю экран обратно.");

                Finish();

                yield break;
            }

            if (curtain != null)
                curtain.TrackLoading(scenes);

            while (!loaded)
                yield return null;

            for (var i = 0; i < SettleFrames; i++)
                yield return null;

            Finish();
        }

        private void Finish()
        {
            IsRunning = false;

            if (curtain != null)
                curtain.Open();
        }


        // ── IReleasable ───────────────────────────────────────────────────

        public void Release()
        {
            routine.Stop();
            routine = default;

            // Флаг сбрасываем руками: корутину оборвали на середине, и её завершающая
            // часть не отработает. Иначе переезд навсегда остался бы «идущим».
            IsRunning = false;
        }
    }
}
