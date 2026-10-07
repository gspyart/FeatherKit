using System.Collections.Generic;
using FeatherKit.Contexts;
using FeatherKit.Pooling;
using FeatherKit.Helpers;
using FeatherKit.UI;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Нижний слой иконок: держит UI-объекты в точках мира. Показать по ключу, убрать по ключу.
    /// - показ разовый: точку и живость цели каждый кадр сообщает вызывающий;
    /// - за краем прячет или прижимает к месту, свободному от интерфейса (<see cref="IconEdgeBounds"/>);
    /// - провожает анимацией ухода, объекты берёт из пула сцены;
    /// - разводит наехавшие иконки — это его часть <see cref="IconOverlapSeparation"/>.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class IconsRenderer : MonoBehaviour
    {
        [Tooltip("Куда класть иконки. Пусто — сам объект")]
        [SerializeField] private RectTransform iconsRoot;

        [Tooltip("Докуда прижимать иконки, ушедшие за край: рамка свободного места, " +
                 "элементы интерфейса, которые она обходит, и отступ внутрь. Без рамки " +
                 "полем остаётся весь экран")]
        [SerializeField] private IconEdgeBounds edgeBounds = new IconEdgeBounds();

        [Tooltip("Запас за краем экрана, пикселей: иконка гаснет не на самой границе, " +
                 "а отойдя на этот запас. Без него метка мигает у края, когда цель " +
                 "покачивается на ходу")]
        [SerializeField] private float offscreenMargin = 120f;

        [Header("Расталкивание")]
        [Tooltip("Отступ между разъехавшимися иконками, в единицах холста — тех же, " +
                 "в которых заданы их размеры. Ноль — встанут вплотную")]
        [SerializeField] private float overlapPadding = 8f;

        [Tooltip("За сколько иконка доезжает до места, на которое её отодвинули, сек. " +
                 "Ноль — рывком, и тогда соседи дрожат, когда цели ходят рядом")]
        [Min(0f)]
        [SerializeField] private float separationSmoothing = 0.12f;

        // Ключ — то, что заказчик назвал «этой иконкой»: повторный показ обновляет, а не заводит.
        private readonly Dictionary<object, IconRenderInfo> shownIcons = new Dictionary<object, IconRenderInfo>();
        private readonly List<object> toRemove = new List<object>();

        private IconOverlapSeparation separation;
        private Canvas canvas;
        private Camera worldCamera;


        public RectTransform Root => iconsRoot != null ? iconsRoot : (RectTransform)transform;

        public int Count => shownIcons.Count;

        // Настройки части «Расталкивание»: поля остаются здесь, в одном инспекторе с рендером.
        internal float OverlapPadding => overlapPadding;

        internal float SeparationSmoothing => separationSmoothing;

        // Пул своей сцены: иконки мигают постоянно, и Instantiate с Destroy давали бы мусор каждый кадр.
        private PoolManager Pool => this.GetService<PoolManager>();

        // Кеш: камеру спрашивают каждый кадр. Лениво — она может подняться позже интерфейса.
        private Camera WorldCamera
        {
            get
            {
                if (worldCamera == null)
                    worldCamera = Camera.main;

                return worldCamera;
            }
        }

        // Overlay-холсту камера для пересчёта не нужна и мешает: ему передают null.
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


        // ── Показать и убрать ─────────────────────────────────────────────

        /// <summary>
        /// Показать иконку. Повторный вызов с тем же ключом обновляет её, поэтому звать
        /// каждый кадр можно. Возвращает объект на экране — через него добираются до кнопки.
        /// </summary>
        public RectTransform Show(object key, RectTransform prefab, ScreenPoint point, OffscreenMode offscreen = OffscreenMode.Hide, RectTransform edgePrefab = null, bool clampByIconEdge = false, bool avoidOverlap = false)
        {
            if (key == null || prefab == null || !point.IsValid)
                return null;

            if (!shownIcons.TryGetValue(key, out var icon))
            {
                icon = new IconRenderInfo();
                shownIcons[key] = icon;
            }

            // Уже провожали, а её снова просят: возвращаем ту же. Появление гасит уход,
            // и возврат в пул по его колбеку не случится.
            if (icon.IsFading)
            {
                icon.IsFading = false;

                icon.Animation?.Show();
            }

            // Сменили префаб на ходу — старый объект больше не тот, что просили.
            if (icon.Prefab != prefab)
            {
                ReleaseInstance(icon);
                icon.Prefab = prefab;
            }

            icon.Point = point;
            icon.EdgePrefab = edgePrefab;
            icon.Offscreen = offscreen;
            icon.ClampByIconEdge = clampByIconEdge;
            icon.AvoidOverlap = avoidOverlap;

            if (icon.Instance == null)
                SetInstance(icon, Create(prefab));

            return icon.Instance;
        }

        /// <summary>
        /// Убрать иконку этого ключа. С анимацией ухода объект уходит в пул, когда она
        /// доиграет, а до тех пор едет за целью. Нет такой — ничего не происходит.
        /// </summary>
        public void Hide(object key)
        {
            if (key == null || !shownIcons.TryGetValue(key, out var icon))
                return;

            if (icon.IsFading)
                return;

            var animation = icon.Animation;

            if (animation == null)
            {
                ReleaseInstance(icon);
                shownIcons.Remove(key);

                return;
            }

            icon.IsFading = true;

            animation.Hide(() => Forget(key, icon));
        }

        // Пока уход шёл, иконку могли показать снова — тогда она уже не наша.
        private void Forget(object key, IconRenderInfo icon)
        {
            if (!icon.IsFading)
                return;

            ReleaseInstance(icon);
            shownIcons.Remove(key);
        }


        public bool IsShown(object key) => key != null && shownIcons.ContainsKey(key);


        /// <summary>Объект иконки этого ключа. Null — не показана.</summary>
        public RectTransform Get(object key)
        {
            return key != null && shownIcons.TryGetValue(key, out var icon) ? icon.Instance : null;
        }


        /// <summary>
        /// Будет ли иконка в этой точке мира видна, а не спрятана за краем: тот же запас, что
        /// у режима Hide. Правилу — чтобы не занимать цель иконкой, которую не увидят.
        /// </summary>
        public bool CanShowAt(Vector3 worldPoint)
        {
            var camera = WorldCamera;

            return camera == null || camera.IsOnScreen(worldPoint, offscreenMargin);
        }


        // ── Объекты ───────────────────────────────────────────────────────

        private RectTransform Create(RectTransform prefab)
        {
            var pool = Pool;

            if (prefab == null || pool == null)
                return null;

            var instance = pool.Get(prefab, Vector3.zero, Quaternion.identity);

            if (instance == null)
                return null;

            // Родителя после выдачи: пул хранит инстансы у себя. worldPositionStays false —
            // иначе приедет мировой масштаб.
            instance.SetParent(Root, false);
            instance.localScale = Vector3.one;

            // Из пула объект мог прийти выключенным — его прятали в прошлой жизни.
            if (!instance.gameObject.activeSelf)
                instance.gameObject.SetActive(true);

            return instance;
        }

        // Позицию ведём мы и перебьём её в том же кадре — анимации говорим не писать её самой.
        private void SetInstance(IconRenderInfo icon, RectTransform instance)
        {
            icon.Instance = instance;
            icon.Animation = instance != null ? instance.GetComponent<IShowHideAnimation>() : null;

            if (icon.Animation == null)
                return;

            icon.Animation.PositionDriven = true;
            icon.Animation.Show();
        }

        // Сейчас же, без анимации ухода: подмена краевого вида и смена префаба мгновенны.
        private void ReleaseInstance(IconRenderInfo icon)
        {
            if (icon.Instance == null)
                return;

            var pool = Pool;

            if (pool != null)
                pool.Release(icon.Instance);
            else
                Destroy(icon.Instance.gameObject);

            icon.Instance = null;
            icon.Animation = null;
            icon.IsAtEdge = false;
            icon.IsFading = false;
        }

        private static void SetVisible(IconRenderInfo icon, bool visible)
        {
            if (icon.Instance != null && icon.Instance.gameObject.activeSelf != visible)
                icon.Instance.gameObject.SetActive(visible);
        }

        // Подменяем на краевую и обратно, только когда состояние действительно сменилось.
        private void SwapIfNeeded(IconRenderInfo icon, bool atEdge)
        {
            var edge = atEdge && icon.EdgePrefab != null;

            if (icon.Instance != null && icon.IsAtEdge == edge)
                return;

            ReleaseInstance(icon);

            SetInstance(icon, Create(edge ? icon.EdgePrefab : icon.Prefab));

            icon.IsAtEdge = edge;
        }


        // ── Расстановка на экране ─────────────────────────────────────────

        // Граница с запасом одна на оба режима: у самого края цель качается, и иконка мигала бы.
        private void PlaceOnScreen(IconRenderInfo icon)
        {
            var screen = icon.Point.Resolve(WorldCamera, out var behind);
            var outside = behind || IsOutside(screen, offscreenMargin);

            // Прячем, но НЕ в пул: заказчик зовёт Show каждый кадр, и объект создавался бы заново.
            if (outside && icon.Offscreen == OffscreenMode.Hide)
            {
                SetVisible(icon, false);
                return;
            }

            // Указателю в кадре делать нечего — цель и так видно.
            if (!outside && icon.Offscreen == OffscreenMode.EdgeOnly)
            {
                SetVisible(icon, false);
                return;
            }

            SwapIfNeeded(icon, outside && icon.Offscreen != OffscreenMode.Hide);
            SetVisible(icon, true);

            MoveTo(icon, screen);
        }

        // Доигрывающую уход ведём за целью, но ничего не подменяем и не прячем.
        private void Follow(IconRenderInfo icon)
        {
            var screen = icon.Point.Resolve(WorldCamera, out _);

            MoveTo(icon, screen);
        }

        // Прижимаем к полю каждый кадр, а не только за порогом: порог дальше поля, и на нём
        // иконку рвануло бы внутрь сразу на весь отступ.
        private void MoveTo(IconRenderInfo icon, Vector3 screen)
        {
            if (icon.Instance == null)
                return;

            if (icon.Offscreen != OffscreenMode.Hide)
            {
                var placed = edgeBounds.Clamp(screen, UiCamera);

                screen.x = placed.x;
                screen.y = placed.y;
            }

            // Через локальные координаты холста: так одинаково работают Overlay и холст на камере.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, UiCamera, out var local))
                return;

            icon.Anchored = icon.ClampByIconEdge ? FitInsideRoot(icon.Instance, local) : local;
            icon.ApplyPosition();
        }

        // Точка ещё в кадре, а половина панели за ним: двигаем ровно на то, сколько не влезло.
        // В координатах слоя — и точка, и габариты иконки уже живут в них.
        private Vector2 FitInsideRoot(RectTransform instance, Vector2 local)
        {
            var area = Root.rect;
            var iconRect = instance.rect;

            return new Vector2(FitAxis(local.x, iconRect.xMin, iconRect.xMax, area.xMin, area.xMax),
                               FitAxis(local.y, iconRect.yMin, iconRect.yMax, area.yMin, area.yMax));
        }

        // Иконка шире поля — любое место одинаково плохо: лишь бы не выехала за обе границы.
        private static float FitAxis(float point, float from, float to, float low, float high)
        {
            var nearest = low - from;
            var farthest = high - to;

            return nearest <= farthest
                ? Mathf.Clamp(point, nearest, farthest)
                : Mathf.Clamp(point, farthest, nearest);
        }

        // Запас расширяет прямоугольник экрана: точка чуть за краем ещё считается своей.
        private static bool IsOutside(Vector3 screen, float margin)
        {
            return screen.x < -margin || screen.x > UnityEngine.Screen.width + margin
                || screen.y < -margin || screen.y > UnityEngine.Screen.height + margin;
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            separation = new IconOverlapSeparation(this);
        }

        // После всех и в LateUpdate: камеру доводит Cinemachine, и раньше точка на экране
        // отставала бы на кадр — иконка дрожала бы на ходу.
        private void LateUpdate()
        {
            toRemove.Clear();

            foreach (var pair in shownIcons)
            {
                var icon = pair.Value;

                // Цель уничтожили, а иконку убрать забыли — снимаем сами. Уходящую не трогаем:
                // её уберёт колбек ухода.
                if (!icon.Point.IsValid)
                {
                    if (!icon.IsFading)
                        toRemove.Add(pair.Key);

                    continue;
                }

                if (icon.IsFading)
                    Follow(icon);
                else
                    PlaceOnScreen(icon);
            }

            for (var i = 0; i < toRemove.Count; i++)
                Hide(toRemove[i]);

            separation.Separate(shownIcons.Values, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            foreach (var pair in shownIcons)
                ReleaseInstance(pair.Value);

            shownIcons.Clear();
        }
    }
}
