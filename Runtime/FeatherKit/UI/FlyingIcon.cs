using System;
using FeatherKit.Attributes;
using FeatherKit.Helpers;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Картинка, которая появляется и улетает в цель; когда и как долго лететь, решает заказчик.
    /// - появляется сверху и из прозрачности, сдвиг раскладки догоняет плавно;
    /// - летит по кривой с дугой, ужимаясь к прилёту, и следует за уехавшей целью (<see cref="ScreenPoint"/>);
    /// - корень держит место в раскладке, ездит видимая часть; что на картинке — дело наследника.
    /// </summary>
    [FDescription("Картинка, которая появляется и улетает в цель — в ячейку на холсте, в объект мира или в точку экрана. Срок полёта задаёт заказчик.")]
    public class FlyingIcon : MonoBehaviour
    {
        [Header("Вид")]
        [Tooltip("Сама картинка")]
        [SerializeField] private Image picture;

        [Tooltip("Чем гасим иконку целиком. Пусто — возьмётся с этого же объекта")]
        [SerializeField] private CanvasGroup fade;

        [Tooltip("Видимая часть иконки — она и ездит при появлении. Корень трогать нельзя: " +
                 "им распоряжается раскладка. Пусто — появление не играется")]
        [SerializeField] private RectTransform visual;

        [Header("Появление")]
        [Tooltip("С какой высоты иконка падает на своё место, пикселей раскладки")]
        [SerializeField] private float appearDrop = 70f;

        [Tooltip("Сколько длится появление, сек")]
        [Min(0f)]
        [SerializeField] private float appearDuration = 0.22f;

        [Tooltip("Ход появления: 0 — иконка наверху, 1 — на своём месте")]
        [SerializeField] private AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Прозрачность по ходу появления")]
        [SerializeField] private AnimationCurve appearFade = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Сдвиг в раскладке")]
        [Tooltip("За сколько иконка догоняет своё новое место, когда соседняя ушла и " +
                 "раскладка сдвинулась, сек. Ноль — сдвиг рывком, как его и делает раскладка")]
        [Min(0f)]
        [SerializeField] private float slideDuration = 0.18f;

        [Tooltip("Разгон сдвига")]
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Полёт")]
        [Tooltip("Ход полёта: 0 — иконка на месте, 1 — она в цели. Провал в начале " +
                 "и рывок в конце читается как «притянуло»")]
        [SerializeField] private AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Насколько путь выгибается дугой вбок, пикселей раскладки. Ноль — по прямой")]
        [SerializeField] private float flightArc = 50f;

        [Tooltip("Размер иконки по ходу полёта: 1 — свой, меньше — ужимается к прилёту")]
        [SerializeField] private AnimationCurve flightScale = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f);

        [Tooltip("Прозрачность иконки по ходу полёта")]
        [SerializeField] private AnimationCurve flightFade = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.65f, 1f), new Keyframe(1f, 0f));

        private Vector3 visualHome;

        // Два смещения видимой части от её места: одно от появления, другое от сдвига
        // раскладки. Складываются, потому что случиться могут и вместе.
        private Vector3 appearOffset;
        private Vector3 slideOffset;

        private float appearTime;
        private bool appearing;
        private Vector3 slideFrom;
        private float slideTime;
        private Vector3 lastPlace;
        private bool placed;

        private ScreenPoint target;
        private Vector3 startPoint;
        private Vector3 endPoint;
        private Vector3 arcDirection;
        private float flightTime;
        private float flightDuration;
        private Action landed;

        private Canvas canvas;
        private Camera worldCamera;


        /// <summary>Летит прямо сейчас. Долетевшая и не начинавшая — обе нет.</summary>
        public bool IsFlying => flightDuration > 0f;


        /// <summary>Что рисует картинку. Наследнику — чтобы поменять её по-своему.</summary>
        protected Image Picture => picture;

        // Чем проецировать точку сцены. Камеры может не быть вовсе, если цель наэкранная, —
        // спрашиваем лениво и не ругаемся.
        private Camera WorldCamera
        {
            get
            {
                if (worldCamera == null)
                    worldCamera = Camera.main;

                return worldCamera;
            }
        }

        // Чем переводить точку экрана в координаты раскладки. У наэкранного холста камеры
        // нет вовсе, и подсунутая ему чужая сдвинула бы цель.
        private Camera UiCamera
        {
            get
            {
                if (canvas == null)
                    canvas = GetComponentInParent<Canvas>();

                if (canvas == null)
                    return null;

                return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }


        // ── Показ ─────────────────────────────────────────────────────────

        /// <summary>
        /// Показать картинку с начала: прошлый полёт кончился на нуле прозрачности
        /// и трети размера, и вернуть иконку к себе некому, кроме нас.
        /// </summary>
        public void Show(Sprite sprite)
        {
            StopFlight();

            // Место иконке назначает раскладка, но переиспользованная пришла из цели:
            // до первого пересчёта она стояла бы там же.
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;

            slideOffset = Vector3.zero;
            slideTime = 0f;
            placed = false;

            if (picture != null)
            {
                picture.enabled = sprite != null;
                picture.sprite = sprite;
            }

            StartAppear();
        }


        // ── Место в раскладке ─────────────────────────────────────────────

        private void StartAppear()
        {
            if (visual == null || appearDuration <= 0f)
            {
                FinishAppear();

                return;
            }

            appearing = true;
            appearTime = 0f;

            Emerge(0f);
        }

        private void Appear(float delta)
        {
            appearTime += delta;

            var t = Mathf.Clamp01(appearTime / appearDuration);

            Emerge(t);

            if (t >= 1f)
                appearing = false;
        }

        // Сверху вниз и из прозрачности: единица хода — иконка дома и видна целиком.
        private void Emerge(float t)
        {
            appearOffset = Vector3.up * (appearDrop * (1f - appearCurve.Evaluate(t)));

            if (fade != null)
                fade.alpha = appearFade.Evaluate(t);

            ApplyVisual();
        }

        private void FinishAppear()
        {
            appearing = false;
            appearOffset = Vector3.zero;

            if (fade != null)
                fade.alpha = 1f;

            ApplyVisual();
        }

        // Раскладка переставляет КОРЕНЬ мгновенно: забираем её прыжок смещением
        // и отдаём плавно — со стороны это и есть сдвиг соседей.
        private void Follow(float delta)
        {
            if (visual == null || IsFlying)
                return;

            var place = transform.localPosition;

            // Пока иконка появляется, раскладка ещё только ставит её на место: догонять нечего.
            if (appearing || !placed)
            {
                lastPlace = place;
                placed = true;
                slideOffset = Vector3.zero;

                return;
            }

            if (place != lastPlace)
            {
                slideFrom = slideOffset + (lastPlace - place);
                slideOffset = slideFrom;
                slideTime = 0f;
                lastPlace = place;
            }

            if (slideOffset == Vector3.zero)
                return;

            if (slideDuration <= 0f)
            {
                slideOffset = Vector3.zero;
            }
            else
            {
                slideTime += delta;

                slideOffset = Vector3.LerpUnclamped(slideFrom, Vector3.zero,
                    slideCurve.Evaluate(Mathf.Clamp01(slideTime / slideDuration)));
            }

            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (visual != null)
                visual.localPosition = visualHome + appearOffset + slideOffset;
        }


        // ── Полёт ─────────────────────────────────────────────────────────

        /// <summary>Улететь в цель за этот срок. Срок снаружи: у заказчика один шаг очереди
        /// на полёт, отклик цели и подпись, и делит его он.</summary>
        public void FlyTo(ScreenPoint target, float duration, Action done)
        {
            // Появление и сдвиг досматривать некогда: дальше иконкой распоряжается полёт,
            // и три анимации писали бы в одно место.
            FinishAppear();

            slideOffset = Vector3.zero;
            placed = false;

            ApplyVisual();

            var point = duration > 0f ? Resolve(target) : null;

            // Лететь неоткуда или некогда — считаем, что уже долетели: просивший ждёт
            // ответа в любом случае, иначе его очередь встанет навсегда.
            if (point == null)
            {
                done?.Invoke();

                return;
            }

            this.target = target;

            startPoint = transform.localPosition;
            endPoint = point.Value;

            flightDuration = duration;
            flightTime = 0f;
            landed = done;
        }

        /// <summary>Оборвать полёт, никому не отвечая: иконку забирают обратно себе.</summary>
        public void StopFlight()
        {
            flightDuration = 0f;
            flightTime = 0f;
            landed = null;
        }

        // Где цель СЕЙЧАС, в координатах раскладки. Null — лететь некуда: точка пустая
        // или иконка лежит вне холста.
        private Vector3? Resolve(ScreenPoint point)
        {
            var parent = transform.parent as RectTransform;

            if (parent == null || !point.IsValid)
                return null;

            var screen = point.Resolve(WorldCamera, out _);

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, UiCamera, out var local)
                ? new Vector3(local.x, local.y, 0f)
                : (Vector3?)null;
        }

        private void Fly(float delta)
        {
            flightTime += delta;

            // Цель могла уехать — карта сдвинулась, ячейка переехала: спрашиваем заново.
            var moved = Resolve(target);

            if (moved != null)
                endPoint = moved.Value;

            var t = Mathf.Clamp01(flightTime / flightDuration);
            var path = endPoint - startPoint;

            // Дуга откладывается вбок ОТ ПУТИ, а не по экрану: цель бывает прямо над
            // иконкой, и вдоль экранной оси выгибать было бы нечего.
            arcDirection = new Vector3(-path.y, path.x, 0f).normalized;

            var position = Vector3.LerpUnclamped(startPoint, endPoint, flightCurve.Evaluate(t));

            transform.localPosition = position + arcDirection * (flightArc * Mathf.Sin(Mathf.PI * t));
            transform.localScale = Vector3.one * flightScale.Evaluate(t);

            if (fade != null)
                fade.alpha = flightFade.Evaluate(t);

            if (t < 1f)
                return;

            // Ответ отдаём последним и через копию: по нему заказчик забирает иконку себе,
            // и до этого момента она должна быть в покое.
            var done = landed;

            StopFlight();
            done?.Invoke();
        }


        // ── Unity ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (fade == null)
                fade = GetComponent<CanvasGroup>();

            if (visual != null)
                visualHome = visual.localPosition;
        }

        // Время НЕигровое: замедление мира — дело игры, а интерфейс отвечает с обычной
        // скоростью.
        private void Update()
        {
            var delta = Time.unscaledDeltaTime;

            if (appearing)
                Appear(delta);

            Follow(delta);

            if (flightDuration > 0f)
                Fly(delta);
        }
    }
}
