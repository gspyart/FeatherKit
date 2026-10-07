using System;
using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FeatherKit.UI
{
    /// <summary>
    /// Карусель карточек: выбранная стоит посередине, соседи выглядывают по бокам — меньше
    /// и тусклее, дальние гаснут совсем.
    /// - листается пальцем: отпустил — встаёт к ближайшей, короткий мах — к соседней;
    /// - тап по соседу выбирает его;
    /// - «по кругу» — за последней снова первая; без этого ряд упирается в края;
    /// - ближняя к середине карточка рисуется поверх остальных: соседи могут заходить под неё;
    /// - что на карточках, не знает: карточки — включённые дети <see cref="CardsRoot"/>,
    ///   наполняет их владелец и зовёт <see cref="Rebuild"/>.
    /// Ловит касания сама, поэтому на объекте нужна графика-приёмник — хотя бы прозрачная Image.
    /// </summary>
    [FDescription("Карусель карточек: выбранная посередине, соседи по бокам. Листается пальцем и тапом по соседу, умеет идти по кругу.")]
    public class UiCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // Насколько ряд тянется за крайнюю карточку: треть хода пальца — край чувствуется,
        // но не каменный.
        private const float EdgeResistance = 0.3f;

        // Ближе этого ряд считается доехавшим: дальше доводка уже не видна глазу.
        private const float SettledDistance = 0.001f;

        [Header("Карточки")]
        [Tooltip("Где лежат карточки. Пусто — прямо на этом объекте")]
        [SerializeField] private RectTransform cardsRoot;

        [Tooltip("Расстояние между серединами соседних карточек, в единицах холста. Больше " +
                 "ширины карточки — ряд с просветами, меньше — соседи заходят под выбранную: " +
                 "она всегда рисуется поверх")]
        [Min(1f)]
        [SerializeField] private float spacing = 700f;

        [Tooltip("По кругу: за последней карточкой снова первая. Снято — ряд идёт от первой " +
                 "до последней и упирается в края")]
        [SerializeField] private bool isLooped;

        [Header("Соседи")]
        [Tooltip("Размер соседней карточки от выбранной: 0.8 — на пятую часть меньше")]
        [Range(0.1f, 1f)]
        [SerializeField] private float sideScale = 0.82f;

        [Tooltip("Непрозрачность соседней карточки: 1 — как у выбранной. Карточки дальше " +
                 "соседей гаснут совсем")]
        [Range(0f, 1f)]
        [SerializeField] private float sideAlpha = 0.6f;

        [Header("Листание")]
        [Tooltip("За сколько ряд доезжает до места, сек. Меньше 0.08 читается как рывок, " +
                 "больше 0.3 — как вязкость")]
        [Min(0.01f)]
        [SerializeField] private float snapTime = 0.15f;

        [Tooltip("Какую долю шага протянуть пальцем, чтобы перелистнуть на соседа. Меньше 0.1 " +
                 "листает от дрожи пальца, больше 0.3 — приходится тянуть через весь экран")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float swipeShare = 0.15f;

        private readonly List<RectTransform> cards = new List<RectTransform>();
        private readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();

        // Где стоит ряд сейчас, в карточках: 1.5 — ровно между второй и третьей.
        // По кругу он не ограничен — номер карточки получается остатком от деления.
        private float position;

        // Куда ряд едет: всегда целое число.
        private float targetPosition;
        private float snapVelocity;

        private int selectedIndex;

        private bool isDragging;
        private float dragStartPosition;
        private Vector2 dragStartPoint;

        // Где ряд стоял при прошлой раскладке: не двигался — раскладывать заново незачем.
        private float laidOutPosition = float.NaN;

        // Карточка, поднятая поверх остальных, и её место среди детей. Поднята всегда одна:
        // тогда вернуть порядок — один шаг, а по порядку детей ряд читает, кто за кем.
        private RectTransform frontCard;
        private int frontCardHomeIndex = -1;


        /// <summary>Выбрали другую карточку: пальцем, тапом или из кода. Приходит номер новой.</summary>
        public event Action<int> SelectionChanged;


        /// <summary>Сколько карточек в ряду. Считается на <see cref="Rebuild"/>.</summary>
        public int Count => cards.Count;

        /// <summary>Номер выбранной карточки. Во время протяжки — прежний: новая выбирается, когда палец отпустят.</summary>
        public int SelectedIndex => selectedIndex;

        public RectTransform CardsRoot => cardsRoot != null ? cardsRoot : (RectTransform)transform;

        /// <summary>По кругу ли идёт ряд. Переключение не сбивает выбор.</summary>
        public bool IsLooped
        {
            get => isLooped;
            set
            {
                if (isLooped == value)
                    return;

                isLooped = value;

                JumpTo(selectedIndex);
            }
        }


        // ── Карточки ──────────────────────────────────────────────────────

        /// <summary>
        /// Перечитать карточки: включённые дети <see cref="CardsRoot"/> в их порядке. Зовёт владелец,
        /// когда наполнил ряд. Выбор остаётся на своём номере, если такой ещё есть.
        /// </summary>
        public void Rebuild()
        {
            // Поднятую — на место, иначе она прочиталась бы последней, и номера поехали бы.
            SendFrontCardHome();

            cards.Clear();
            cardGroups.Clear();

            var root = CardsRoot;

            for (var i = 0; i < root.childCount; i++)
            {
                var card = root.GetChild(i) as RectTransform;

                if (card == null || !card.gameObject.activeSelf)
                    continue;

                cards.Add(card);
                cardGroups.Add(GroupOf(card));
            }

            JumpTo(selectedIndex);
        }

        // Прозрачность соседей держит CanvasGroup: он гасит карточку целиком, со всем, что на ней.
        private static CanvasGroup GroupOf(RectTransform card)
        {
            var group = card.GetComponent<CanvasGroup>();

            return group != null ? group : card.gameObject.AddComponent<CanvasGroup>();
        }


        // ── Выбор ─────────────────────────────────────────────────────────

        /// <summary>Выбрать карточку. Сразу — без проезда: так встают на открытии окна.</summary>
        public void Select(int index, bool animated = true)
        {
            if (!animated)
            {
                JumpTo(index);
                return;
            }

            if (cards.Count == 0)
                return;

            MoveTo(TargetFor(index));
        }


        // Встать на место без проезда: ряд уже стоит там, куда ехал.
        private void JumpTo(int index)
        {
            if (cards.Count == 0)
            {
                selectedIndex = Mathf.Max(0, index);
                return;
            }

            index = NormalizeIndex(index);

            position = index;
            targetPosition = index;
            snapVelocity = 0f;

            // Раскладываем, даже если ряд стоит там же: карточки могли смениться.
            laidOutPosition = float.NaN;

            LayOut();

            SetSelected(index);
        }

        // Ехать к месту и сразу сказать, кто выбран: ждать, пока ряд доедет, незачем.
        private void MoveTo(float target)
        {
            targetPosition = target;

            SetSelected(NormalizeIndex(Mathf.RoundToInt(target)));
        }

        private void SetSelected(int index)
        {
            if (selectedIndex == index)
                return;

            selectedIndex = index;

            SelectionChanged?.Invoke(index);
        }

        // Куда ехать, чтобы встать на эту карточку. По кругу — коротким путём: из последней
        // в первую ряд идёт на шаг вперёд, а не пролистывает всё назад.
        private float TargetFor(int index)
        {
            index = NormalizeIndex(index);

            if (!isLooped)
                return index;

            var current = Mathf.RoundToInt(targetPosition);

            return current + WrappedOffset(index - current);
        }


        // ── Касания ───────────────────────────────────────────────────────

        // IPointerClickHandler: тап по соседу выбирает его. По выбранной — ничего: он уже выбран.
        public void OnPointerClick(PointerEventData eventData)
        {
            var index = CardIndexOf(eventData.pointerPressRaycast.gameObject);

            if (index >= 0 && index != selectedIndex)
                Select(index);
        }

        // Какая карточка под пальцем: поднимаемся от задетого до ребёнка корня карточек.
        private int CardIndexOf(GameObject hit)
        {
            var root = CardsRoot;
            var current = hit != null ? hit.transform : null;

            while (current != null && current.parent != root)
                current = current.parent;

            return current != null ? cards.IndexOf(current as RectTransform) : -1;
        }


        // IBeginDragHandler
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Поехали — значит это уже не тап: иначе протяжка кончилась бы выбором карточки под пальцем.
            eventData.eligibleForClick = false;

            if (cards.Count == 0)
                return;

            isDragging = true;
            dragStartPosition = position;
            dragStartPoint = LocalPointOf(eventData);
            snapVelocity = 0f;
        }

        // IDragHandler
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            // Палец влево — ряд вперёд: следующая карточка приезжает справа.
            var shift = (LocalPointOf(eventData).x - dragStartPoint.x) / spacing;

            position = ResistEdges(dragStartPosition - shift);

            LayOut();
        }

        // IEndDragHandler
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            isDragging = false;

            MoveTo(ClampTarget(ReleaseTarget()));
        }

        // Протянул больше половины шага — встаём к ближайшей, так и длинная протяжка листает
        // на несколько. Меньше, но дальше порога — на соседа в сторону пальца. Иначе — назад.
        private float ReleaseTarget()
        {
            var start = Mathf.Round(dragStartPosition);
            var moved = position - dragStartPosition;

            if (Mathf.Abs(moved) >= 0.5f)
                return Mathf.Round(position);

            if (Mathf.Abs(moved) >= swipeShare)
                return start + Mathf.Sign(moved);

            return start;
        }

        private float ClampTarget(float target)
        {
            return isLooped ? target : Mathf.Clamp(target, 0f, cards.Count - 1);
        }

        // За крайней карточкой ряд тянется туже: без кольца дальше ехать некуда.
        private float ResistEdges(float wanted)
        {
            if (isLooped)
                return wanted;

            var last = cards.Count - 1;

            if (wanted < 0f)
                return wanted * EdgeResistance;

            if (wanted > last)
                return last + (wanted - last) * EdgeResistance;

            return wanted;
        }

        private Vector2 LocalPointOf(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CardsRoot, eventData.position,
                eventData.pressEventCamera, out var point);

            return point;
        }


        // ── Раскладка ─────────────────────────────────────────────────────

        // Время неигровое: окно может стоять поверх замедленной игры, а палец не замедляется.
        private void UpdateSnap()
        {
            if (isDragging || cards.Count == 0)
                return;

            position = Mathf.SmoothDamp(position, targetPosition, ref snapVelocity, snapTime,
                Mathf.Infinity, Time.unscaledDeltaTime);

            if (Mathf.Abs(position - targetPosition) < SettledDistance)
            {
                position = targetPosition;
                snapVelocity = 0f;

                KeepLoopNearZero();
            }

            LayOut();
        }

        // По кругу ряд уходит сколько угодно далеко от нуля. Доехав, возвращаем его в первый
        // круг: картинка та же, а число не копит погрешность.
        private void KeepLoopNearZero()
        {
            if (!isLooped)
                return;

            var turns = Mathf.Floor(targetPosition / cards.Count) * cards.Count;

            if (turns == 0f)
                return;

            position -= turns;
            targetPosition -= turns;
        }


        private void LayOut()
        {
            if (Mathf.Approximately(laidOutPosition, position))
                return;

            laidOutPosition = position;

            for (var i = 0; i < cards.Count; i++)
                LayOutCard(i);

            BringNearestToFront();
        }

        // Выбранная — на месте и целиком; сосед — сбоку, меньше и тусклее; дальше — гаснет.
        private void LayOutCard(int index)
        {
            var card = cards[index];
            var group = cardGroups[index];

            if (card == null)
                return;

            var offset = OffsetOf(index);
            var distance = Mathf.Abs(offset);

            card.anchoredPosition = new Vector2(offset * spacing, card.anchoredPosition.y);
            card.localScale = Vector3.one * Mathf.Lerp(1f, sideScale, Mathf.Clamp01(distance));

            if (group == null)
                return;

            var alpha = distance <= 1f
                ? Mathf.Lerp(1f, sideAlpha, distance)
                : Mathf.Lerp(sideAlpha, 0f, distance - 1f);

            group.alpha = alpha;

            // Погасшая карточка не должна перехватывать тап у той, что под ней видна.
            group.blocksRaycasts = alpha > 0.01f;
        }

        // Где карточка стоит относительно середины, в шагах: минус — слева, плюс — справа.
        private float OffsetOf(int index)
        {
            return isLooped ? WrappedOffset(index - position) : index - position;
        }


        // ── Кто поверх ────────────────────────────────────────────────────

        // Поверх — ближняя к середине: при листании её сменяет та, что пересекла середину.
        // Остальных не трогаем — соседи по обе стороны друг на друга не заходят.
        private void BringNearestToFront()
        {
            var nearest = NearestCard();

            if (nearest == frontCard)
                return;

            SendFrontCardHome();

            if (nearest == null)
                return;

            frontCard = nearest;
            frontCardHomeIndex = nearest.GetSiblingIndex();

            nearest.SetAsLastSibling();
        }

        private RectTransform NearestCard()
        {
            RectTransform nearest = null;
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < cards.Count; i++)
            {
                var distance = Mathf.Abs(OffsetOf(i));

                if (cards[i] == null || distance >= nearestDistance)
                    continue;

                nearest = cards[i];
                nearestDistance = distance;
            }

            return nearest;
        }


        // Вернуть поднятую туда, где она лежала. Её могли убрать из ряда или уничтожить —
        // тогда возвращать нечего и некуда.
        private void SendFrontCardHome()
        {
            if (frontCard != null && frontCard.parent == CardsRoot)
                frontCard.SetSiblingIndex(frontCardHomeIndex);

            frontCard = null;
            frontCardHomeIndex = -1;
        }


        // ── Номера ────────────────────────────────────────────────────────

        // Номер в пределах ряда: по кругу — остатком, иначе — упором в края.
        private int NormalizeIndex(int index)
        {
            if (cards.Count == 0)
                return 0;

            return isLooped
                ? (int)Mathf.Repeat(index, cards.Count)
                : Mathf.Clamp(index, 0, cards.Count - 1);
        }

        // Смещение по кругу коротким путём: от минус половины ряда до плюс половины.
        private float WrappedOffset(float offset)
        {
            var count = cards.Count;

            return Mathf.Repeat(offset + count * 0.5f, count) - count * 0.5f;
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Rebuild();
        }

        // Спрятали посреди протяжки — вернуться ряд должен стоящим на месте, а не на полпути.
        private void OnDisable()
        {
            isDragging = false;

            JumpTo(selectedIndex);
        }

        private void Update()
        {
            UpdateSnap();
        }
    }
}
