using System.Collections.Generic;
using FeatherKit.UI;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Летящие числа поверх мира — урон, лечение, монеты: один цикл на все надписи вместо аниматоров.
    /// - надпись встаёт в мировую точку и висит в ней; за краем экрана не заводится вовсе;
    /// - вид задаёт префаб, полёт — <see cref="FloatingTextAnimationConfig"/>; когда и каким — решает зовущий;
    /// - надписи по стопке <see cref="UiObjectPool{T}"/> на префаб: из холста они не уходят.
    /// Вешать на объект со своим Canvas: иначе каждая надпись перестраивала бы весь HUD.
    /// </summary>
    public class FloatingTextController : MonoBehaviour
    {
        [Header("Префабы")]
        [Tooltip("Чем показывать, если вызывающий не передал свой префаб. Им же набивается пул")]
        [SerializeField] private FloatingText defaultPrefab;

        [Header("Анимация по умолчанию")]
        [Tooltip("Для префабов, у которых не задана своя")]
        [SerializeField] private FloatingTextAnimationConfig defaultAnimation;

        [Header("Иерархия")]
        [Tooltip("Куда складывать надписи; если пусто — RectTransform этого объекта")]
        [SerializeField] private RectTransform root;

        [Header("Пул")]
        [Tooltip("Сколько создать заранее, чтобы не было рывка на первых попаданиях")]
        [SerializeField] private int prewarmCount = 30;

        [Tooltip("Сверх лимита самая старая надпись уступает место новой")]
        [SerializeField] private int maxActiveCount = 50;

        [Header("Прочее")]
        [Tooltip("Запас за краем экрана в долях от него: 0.15 = 15%. Дальше надпись не показываем")]
        [SerializeField] private float offScreenMargin = 0.15f;

        [SerializeField] private bool newestOnTop = true;

        [Tooltip("Не замирать на игровой паузе — например, пока открыт экран выбора. " +
                 "По умолчанию включено: летящая надпись — это фидбек, а он доигрывает всегда")]
        [SerializeField] private bool useUnscaledTime = true;

        private struct FlyingText
        {
            public FloatingText View;
            public FloatingText Prefab;
            public FloatingTextAnimationConfig Animation;
            public FloatingTextAnimationConfig.Variation Variation;
            public Vector3 WorldPosition;
            public float Timer;
        }

        private readonly List<FlyingText> flyingTexts = new List<FlyingText>();
        private readonly Dictionary<FloatingText, UiObjectPool<FloatingText>> pools =
            new Dictionary<FloatingText, UiObjectPool<FloatingText>>();

        private Camera worldCamera;
        private Camera uiCamera;
        private bool ready;


        /// <summary>Показать число префабом по умолчанию.</summary>
        public void Show(Vector3 worldPosition, int value)
        {
            Show(worldPosition, value, defaultPrefab);
        }

        /// <summary>Показать своим префабом. Не передали — возьмётся тот, что по умолчанию.</summary>
        public void Show(Vector3 worldPosition, int value, FloatingText prefab)
        {
            if (!TryTake(prefab, worldPosition, out var view, out var usedPrefab))
                return;

            view.Prepare(value);
            Launch(view, usedPrefab, worldPosition);
        }

        public void HideAll()
        {
            for (var i = flyingTexts.Count - 1; i >= 0; i--)
                ReleaseAt(i);

            flyingTexts.Clear();
        }


        private bool TryTake(FloatingText prefab, Vector3 worldPosition, out FloatingText view,
            out FloatingText usedPrefab)
        {
            view = null;
            usedPrefab = prefab != null ? prefab : defaultPrefab;

            if (usedPrefab == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(FloatingTextController)}: не задан префаб по умолчанию!", this);
#endif
                return false;
            }

            EnsureReady();

            // За экраном не спавним вовсе — незачем гонять пул и считать анимацию.
            if (!TryGetWorldCamera(out var cam) || IsOffscreen(cam, cam.WorldToScreenPoint(worldPosition)))
                return false;

            if (flyingTexts.Count >= maxActiveCount && flyingTexts.Count > 0)
                ReleaseAt(0);

            view = Take(usedPrefab);

            return view != null;
        }

        private void Launch(FloatingText view, FloatingText prefab, Vector3 worldPosition)
        {
            var animation = view.AnimationConfig != null ? view.AnimationConfig : defaultAnimation;

            if (animation == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(FloatingTextController)}: не задан FloatingTextAnimationConfig!", this);
#endif
                Return(prefab, view);
                return;
            }

            if (newestOnTop)
                view.SelfRect.SetAsLastSibling();

            var item = new FlyingText
            {
                View = view,
                Prefab = prefab,
                Animation = animation,
                Variation = animation.CreateVariation(),
                WorldPosition = worldPosition,
                Timer = 0f
            };

            // Сразу на место, иначе первый кадр надпись вспыхнет там, где закончила прошлую жизнь.
            if (TryGetWorldCamera(out var cam))
                ApplyFrame(ref item, cam);

            flyingTexts.Add(item);
        }

        private void ApplyFrame(ref FlyingText item, Camera cam)
        {
            var normalizedTime = item.Timer / item.Variation.LifeTime;
            var screenPoint = cam.WorldToScreenPoint(item.WorldPosition);

            if (IsOffscreen(cam, screenPoint))
            {
                item.View.SetVisible(false);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, uiCamera, out var localPoint);
            localPoint += item.Variation.SpawnOffset + item.Animation.EvaluateOffset(item.Variation, normalizedTime);

            item.View.SetVisible(true);
            item.View.SetTransform(
                localPoint,
                item.Animation.EvaluateScale(item.Variation, normalizedTime) * item.View.ExtraScale,
                item.Animation.EvaluateRotation(item.Variation, normalizedTime));
            item.View.SetColor(item.Animation.EvaluateColor(item.View.BaseColor, normalizedTime));
        }

        private void ReleaseAt(int index)
        {
            var item = flyingTexts[index];
            flyingTexts.RemoveAt(index);

            Return(item.Prefab, item.View);
        }

        private FloatingText Take(FloatingText prefab)
        {
            return PoolOf(prefab).Take(root);
        }

        private void Return(FloatingText prefab, FloatingText view)
        {
            if (view == null)
                return;

            if (prefab == null)
            {
                Destroy(view.gameObject);
                return;
            }

            PoolOf(prefab).Release(view);
        }

        // Стопка своя у каждого префаба: огонь и порез — разные надписи.
        private UiObjectPool<FloatingText> PoolOf(FloatingText prefab)
        {
            if (!pools.TryGetValue(prefab, out var pool))
            {
                pool = new UiObjectPool<FloatingText>(prefab);
                pools[prefab] = pool;
            }

            return pool;
        }

        /// <summary>Набиваем стопку заранее: первые попадания не должны стоить Instantiate.</summary>
        private void Prewarm()
        {
            if (prewarmCount > 0 && defaultPrefab != null)
                PoolOf(defaultPrefab).Prewarm(prewarmCount, root);
        }

        private void EnsureReady()
        {
            if (ready)
                return;

            ready = true;

            if (root == null)
                root = transform as RectTransform;

            ResolveUiCamera();
            Prewarm();
        }

        private bool TryGetWorldCamera(out Camera cam)
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            cam = worldCamera;

            return cam != null;
        }

        /// <summary>z &lt;= 0 — точка за спиной камеры, там WorldToScreenPoint зеркалит координаты.</summary>
        private bool IsOffscreen(Camera cam, Vector3 screenPoint)
        {
            if (screenPoint.z <= 0f)
                return true;

            var width = cam.pixelWidth;
            var height = cam.pixelHeight;
            var marginX = width * offScreenMargin;
            var marginY = height * offScreenMargin;

            return screenPoint.x < -marginX || screenPoint.x > width + marginX ||
                   screenPoint.y < -marginY || screenPoint.y > height + marginY;
        }

        /// <summary>Overlay-канвасу нужен null, остальным — камера канваса.</summary>
        private void ResolveUiCamera()
        {
            if (root == null)
                return;

            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            canvas = canvas.rootCanvas;
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void Awake()
        {
            EnsureReady();
        }

        private void OnDisable()
        {
            HideAll();
        }

        // LateUpdate, а не Update: камера уже доехала, иначе надписи дрожат относительно мира.
        private void LateUpdate()
        {
            if (flyingTexts.Count == 0)
                return;

            if (!TryGetWorldCamera(out var cam))
                return;

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            for (var i = flyingTexts.Count - 1; i >= 0; i--)
            {
                var item = flyingTexts[i];
                item.Timer += deltaTime;

                if (item.View == null || item.Timer >= item.Variation.LifeTime)
                {
                    ReleaseAt(i);
                    continue;
                }

                ApplyFrame(ref item, cam);

                flyingTexts[i] = item; // структура — кладём обратно
            }
        }
    }
}
