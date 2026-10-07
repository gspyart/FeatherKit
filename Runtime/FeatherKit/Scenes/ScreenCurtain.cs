using System;
using FeatherKit.Attributes;
using FeatherKit.Timers;
using UnityEngine;

namespace FeatherKit.Scenes
{
    /// <summary>
    /// Чёрная заслонка поверх всей игры: закрылась — экрана не видно, открылась — видно.
    /// Ею закрывают смену сцены, чтобы вместо мигания игрок видел плавное затемнение.
    ///
    /// Сам занавес НИЧЕГО не решает: когда закрываться, знает тот, кто просит, —
    /// обычно <see cref="LevelTransition"/>. Ему же достаётся ответ «доехал»: заслонка
    /// зовёт колбэк, когда движение кончилось.
    ///
    /// Живёт в корне игры, а не в сцене: закрыться надо ДО выгрузки, а пережить —
    /// саму выгрузку. Сценовый канвас погас бы вместе с уровнем, и вспышка всё равно
    /// была бы видна.
    ///
    /// Идёт по НЕмасштабированному времени: экран затемняют и на паузе, а с
    /// остановленным временем занавес просто замер бы на середине.
    ///
    /// Пока закрыт хоть немного, ловит нажатия на себя: игрок не должен попадать
    /// по интерфейсу уровня, которого уже нет.
    ///
    /// Содержимое — экран загрузки со своей анимацией — гасит вместе с собой.
    ///
    /// Настройки экрана загрузки тоже здесь: сколько он висит как минимум и кому
    /// отдать ход переезда. Выдерживает срок не занавес, а тот, кто переезжает.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    [FDescription("Чёрная заслонка поверх игры: закрывает экран на смену сцены. Когда закрываться, решает не она.")]
    public class ScreenCurtain : MonoBehaviour
    {
        [Header("Заслонка")]
        [Tooltip("За сколько секунд экран затемняется, сек")]
        [Min(0f)]
        [SerializeField] private float closeDuration = 0.25f;

        [Tooltip("За сколько секунд экран проявляется обратно, сек. Обычно дольше " +
                 "затемнения: уходить в темноту приятнее быстро, а выходить из неё — плавно")]
        [Min(0f)]
        [SerializeField] private float openDuration = 0.35f;

        [Tooltip("Начинать игру с закрытого экрана. Тогда первая сцена тоже проявляется " +
                 "плавно, а заодно прячется неровный первый кадр")]
        [SerializeField] private bool closedOnStart = true;

        [Header("Экран загрузки")]
        [Tooltip("Что показывать на закрытом экране: спиннер загрузки, подпись. Гаснет " +
                 "вместе с занавесом, чтобы анимация не крутилась впустую весь забег")]
        [SerializeField] private GameObject content;

        [Tooltip("Экран загрузки внутри занавеса: ему достаётся ход переезда. Пусто — " +
                 "ход не показывается")]
        [SerializeField] private LoadingScreenView loadingScreen;

        [Tooltip("Сколько секунд экран загрузки висит как минимум, сек. Быстрая сцена " +
                 "грузится мгновенно, и без этого переезд читается как мигание")]
        [Min(0f)]
        [SerializeField] private float minimumLoadingTime = 4f;

        private Canvas canvas;
        private CanvasGroup group;

        // Кого позвать, когда доехали. Одна заявка, а не список: занавесом командует
        // один переход, и вторая просьба посреди движения означает, что первую отменили.
        private Action onArrived;

        private float target;
        private float speed = 1f;


        /// <summary>Сколько экран загрузки висит как минимум — ждёт этого тот, кто переезжает.</summary>
        public float MinimumLoadingTime => minimumLoadingTime;


        // ── Заслонка ──────────────────────────────────────────────────────

        /// <summary>Затемнить экран. Колбэк зовётся, когда стало совсем черно.</summary>
        public void Close(Action onClosed = null)
        {
            MoveTo(1f, closeDuration, onClosed);
        }

        /// <summary>Проявить экран обратно. Колбэк зовётся, когда игру снова видно.</summary>
        public void Open(Action onOpened = null)
        {
            MoveTo(0f, openDuration, onOpened);
        }

        private void MoveTo(float value, float duration, Action onDone)
        {
            target = value;
            onArrived = onDone;

            if (duration <= 0f)
            {
                SetAlpha(value);
                Arrive();

                return;
            }

            speed = 1f / duration;

            // Уже на месте — движения не будет, и ждать колбэка бессмысленно.
            if (Mathf.Approximately(group.alpha, value))
                Arrive();
        }

        private void Arrive()
        {
            var callback = onArrived;
            onArrived = null;

            callback?.Invoke();
        }

        private void SetAlpha(float value)
        {
            group.alpha = value;

            // Прозрачный занавес не должен ни ловить нажатия, ни рисоваться: канвас
            // на весь экран стоит кадра даже пустой.
            group.blocksRaycasts = value > 0f;
            canvas.enabled = value > 0f;

            if (content != null)
                content.SetActive(value > 0f);
        }


        // ── Экран загрузки ────────────────────────────────────────────────

        /// <summary>Показать на экране загрузки ход этого переезда.</summary>
        public void TrackLoading(IProgressable loading)
        {
            if (loadingScreen != null)
                loadingScreen.TrackLoading(loading, minimumLoadingTime);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            group = GetComponent<CanvasGroup>();

            SetAlpha(closedOnStart ? 1f : 0f);
            target = group.alpha;
        }

        private void Start()
        {
            // Первую сцену открываем сами: просить об этом некому — переход был не наш.
            if (closedOnStart)
                Open();
        }

        private void Update()
        {
            if (Mathf.Approximately(group.alpha, target))
                return;

            SetAlpha(Mathf.MoveTowards(group.alpha, target, speed * Time.unscaledDeltaTime));

            if (Mathf.Approximately(group.alpha, target))
                Arrive();
        }
    }
}
