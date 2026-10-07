using System;
using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Жесты пальца по элементу интерфейса: тап, долгое нажатие, перетаскивание — и спор между
    /// ними в одном месте: сработало удержание — ни тапа, ни перетаскивания; поехал палец —
    /// ни тапа, ни удержания. Сам ничего не рисует: ход удержания отдаёт событием.
    /// Тащит, только если палец придержали и перетаскивание слушают; иначе жест уходит
    /// прокрутке под элементом.
    /// </summary>
    [FDescription("Жесты по элементу: тап, долгое нажатие, перетаскивание. Сам ничего не рисует — ход удержания отдаёт событием.")]
    public class UiGestureRecognizer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IPointerClickHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("Долгое нажатие")]
        [Tooltip("Сколько держать, сек. Меньше 0.3 путается с обычным тапом, больше 0.6 — " +
                 "игрок успевает решить, что не сработало")]
        [Min(0.05f)]
        [SerializeField] private float holdSeconds = 0.4f;

        [Tooltip("Насколько пальцу позволено съехать, пикселей. Уехал дальше — это уже " +
                 "протяжка, и удержание отменяется")]
        [Min(0f)]
        [SerializeField] private float moveTolerance = 20f;

        [Header("Перетаскивание")]
        [Tooltip("Сколько придержать палец, чтобы тащить элемент, а не листать список под ним. " +
                 "Меньше 0.1 срабатывает случайно при листании, больше 0.25 — рука не успевает")]
        [Min(0f)]
        [SerializeField] private float grabDelay = 0.15f;

        private Selectable selectable;

        // Само касание, а не его копия: EventSystem держит один объект на палец
        // и обновляет его каждый кадр — по нему и видно, уехал палец или нет.
        private PointerEventData pointer;
        private Vector2 pressPosition;
        private float pressTime;

        private bool isLongPressEnabled = true;
        private bool holding;
        private bool longPressFired;

        // Этот жест отдан прокрутке. Помним на весь жест: середину и конец нельзя
        // разослать не туда, куда ушло начало.
        private bool passedToScroll;


        /// <summary>Коротко нажали и отпустили, не сдвинув палец.</summary>
        public event Action Tapped;

        /// <summary>Подержали достаточно долго. Один раз за касание; тапа после него не будет.</summary>
        public event Action LongPressed;

        /// <summary>Ход удержания: каждый кадр от 0 до 1, пока держат; 0 — удержание кончилось.</summary>
        public event Action<float> HoldProgressChanged;

        public event Action<PointerEventData> DragStarted;

        public event Action<PointerEventData> Dragged;

        public event Action<PointerEventData> DragEnded;

        /// <summary>На элемент бросили то, что тащили. Приходит ДО конца чужого перетаскивания.</summary>
        public event Action<PointerEventData> DroppedOn;


        /// <summary>
        /// Есть ли сейчас долгое нажатие. Гасят там, где оно ничего не сделает — у пустой
        /// ячейки: иначе вид удержания обещал бы действие, которого нет.
        /// </summary>
        public bool IsLongPressEnabled
        {
            get => isLongPressEnabled;
            set
            {
                isLongPressEnabled = value;

                if (!value)
                    CancelHold();
            }
        }

        /// <summary>
        /// Можно ли сейчас тащить элемент. Выключенное перетаскивание уходит прокрутке под ним:
        /// иначе придержанный палец не листал бы список там, где тащить нечего.
        /// </summary>
        public bool IsDragEnabled { get; set; } = true;

        private bool CanHold => isLongPressEnabled && LongPressed != null && IsInteractable;

        // Тащим, только если есть кому и палец придержали: быстрый мах — это листание списка.
        private bool CanGrab => IsDragEnabled && DragStarted != null && Time.unscaledTime - pressTime >= grabDelay;

        // Кнопка на элементе выключена — жестов нет: иначе недоступное нажималось бы.
        private bool IsInteractable => selectable == null || selectable.IsInteractable();


        // ── Касание ───────────────────────────────────────────────────────

        // IPointerDownHandler
        public void OnPointerDown(PointerEventData eventData)
        {
            pointer = eventData;
            pressPosition = eventData.position;
            pressTime = Time.unscaledTime;
            longPressFired = false;
            passedToScroll = false;
            holding = CanHold;
        }

        // IPointerUpHandler
        public void OnPointerUp(PointerEventData eventData)
        {
            pointer = null;

            CancelHold();
        }

        // IPointerExitHandler: палец ушёл с элемента — целились уже не сюда.
        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHold();
        }

        // IPointerClickHandler: после протяжки его не шлёт сам EventSystem, после удержания гасим мы.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (longPressFired || !IsInteractable)
                return;

            Tapped?.Invoke();
        }


        // ── Удержание ─────────────────────────────────────────────────────

        // Время неигровое: удары замедляют игру, а рука человека замедляться вместе с ней не должна.
        private void UpdateHold()
        {
            if (!holding)
                return;

            if (pointer == null || pointer.dragging || HasMoved())
            {
                CancelHold();
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - pressTime) / holdSeconds);

            if (progress < 1f)
            {
                HoldProgressChanged?.Invoke(progress);
                return;
            }

            // Касание потрачено на удержание: поехавший после него палец не должен ещё и утащить элемент.
            pointer.pointerDrag = null;
            longPressFired = true;

            CancelHold();

            LongPressed?.Invoke();
        }

        private bool HasMoved()
        {
            return (pointer.position - pressPosition).sqrMagnitude > moveTolerance * moveTolerance;
        }

        // Сброс вида приходит ровно один раз, чем бы удержание ни кончилось.
        private void CancelHold()
        {
            if (!holding)
                return;

            holding = false;

            HoldProgressChanged?.Invoke(0f);
        }


        // ── Перетаскивание ────────────────────────────────────────────────

        // IInitializePotentialDragHandler: прокрутке под нами оно нужно, чтобы остановить инерцию под пальцем.
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            PassUp(eventData, ExecuteEvents.initializePotentialDrag);
        }

        // IBeginDragHandler
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Поехали — значит это уже не тап: иначе пролиставший палец нажал бы то, над чем прошёл.
            eventData.eligibleForClick = false;

            CancelHold();

            passedToScroll = !CanGrab;

            if (passedToScroll)
            {
                PassUp(eventData, ExecuteEvents.beginDragHandler);
                return;
            }

            DragStarted?.Invoke(eventData);
        }

        // IDragHandler
        public void OnDrag(PointerEventData eventData)
        {
            if (passedToScroll)
                PassUp(eventData, ExecuteEvents.dragHandler);
            else
                Dragged?.Invoke(eventData);
        }

        // IEndDragHandler
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!passedToScroll)
            {
                DragEnded?.Invoke(eventData);
                return;
            }

            passedToScroll = false;

            PassUp(eventData, ExecuteEvents.endDragHandler);
        }

        // IDropHandler
        public void OnDrop(PointerEventData eventData)
        {
            DroppedOn?.Invoke(eventData);
        }

        // Отдаём все события жеста одинаково: прокрутка, получившая одно начало, считала бы
        // сдвиг от точки, которую больше не двигают.
        private void PassUp<THandler>(PointerEventData eventData, ExecuteEvents.EventFunction<THandler> handler)
            where THandler : IEventSystemHandler
        {
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, handler);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        // Элемент мог уехать в пул или спрятаться посреди жеста — вернуться он должен без хвостов.
        private void OnDisable()
        {
            CancelHold();

            pointer = null;
            passedToScroll = false;
        }

        private void Update()
        {
            UpdateHold();
        }
    }
}
