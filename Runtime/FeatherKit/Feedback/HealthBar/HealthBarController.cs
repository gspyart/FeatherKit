using System.Collections.Generic;
using FeatherKit.UI;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Рисует полосы здоровья на обычном экранном канвасе, каждый кадр проецируя мировые
    /// позиции целей. Полосы берутся из пула и переиспользуются.
    ///
    /// Почему не world-space канвас на каждом юните: тот дороже по драв-коллам и требует
    /// разворота к камере на каждом объекте. Здесь один канвас на все полосы, и они всегда
    /// одного экранного размера независимо от дистанции — для изометрии это то, что нужно.
    ///
    /// Значения опрашиваем, а не слушаем событие: HealthBarView.SetValue от повторного
    /// вызова с тем же числом ничего не делает, зато не нужно подписываться и отписываться
    /// на каждого из десятков врагов.
    /// </summary>
    public class HealthBarController : MonoBehaviour
    {
        // Список статический: цели живут на пулевых префабах и не могут держать ссылку
        // на сценовый контроллер. Контроллер может подняться позже целей, поэтому список
        // именно общий, а не поле экземпляра.
        private static readonly List<HealthBarTarget> Targets = new List<HealthBarTarget>();

        public static void Register(HealthBarTarget target)
        {
            if (target != null && !Targets.Contains(target))
                Targets.Add(target);
        }

        public static void Unregister(HealthBarTarget target)
        {
            Targets.Remove(target);
        }

        /// <summary>
        /// Сброс при входе в игру. Список статический, и с выключенной перезагрузкой домена
        /// цели прошлого запуска дожили бы до следующего.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Targets.Clear();
        }


        [Tooltip("Префаб одной полосы")]
        [SerializeField] private HealthBarView barPrefab;

        [Tooltip("Куда складывать полосы; если пусто — этот объект")]
        [SerializeField] private RectTransform root;

        [Tooltip("Не показывать полосу, пока здоровье целое. Полосы над нетронутой толпой — визуальный шум")]
        [SerializeField] private bool hideWhenFull = true;

        [Tooltip("Сколько создать заранее, чтобы первая волна не стоила Instantiate")]
        [SerializeField] private int prewarmCount = 20;

        [Tooltip("Запас за краем экрана в долях от него: дальше полосу не показываем")]
        [SerializeField] private float offScreenMargin = 0.1f;

        private readonly Dictionary<HealthBarTarget, HealthBarView> active = new Dictionary<HealthBarTarget, HealthBarView>();
        private UiObjectPool<HealthBarView> pool;
        private readonly List<HealthBarTarget> finished = new List<HealthBarTarget>();

        private Camera worldCamera;
        private Camera uiCamera;


        private void Awake()
        {
            if (root == null)
                root = transform as RectTransform;

            pool = new UiObjectPool<HealthBarView>(barPrefab);

            ResolveUiCamera();
            Prewarm();
        }

        private void OnDisable()
        {
            foreach (var pair in active)
                Release(pair.Value);

            active.Clear();
        }

        // LateUpdate: камера уже доехала, иначе полосы дрожат относительно юнитов.
        private void LateUpdate()
        {
            if (!TryGetWorldCamera(out var cam))
                return;

            for (var i = Targets.Count - 1; i >= 0; i--)
            {
                var target = Targets[i];

                // Цель могла быть уничтожена, не успев отписаться.
                if (target == null || target.Health == null)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                UpdateTarget(target, cam);
            }

            DropOrphans();
            DropFinished();
        }

        // Цель, уехавшая в пул или уничтоженная, снимает себя из Targets в OnDisable —
        // и после этого мы бы до неё уже не дошли, а её полоса осталась бы висеть на экране
        // замороженной. Поэтому отдельно проходим по выданным полосам, а не только по целям.
        private void DropOrphans()
        {
            foreach (var pair in active)
            {
                var target = pair.Key;

                if (target != null && target.isActiveAndEnabled)
                    continue;

                Release(pair.Value);
                finished.Add(target);
            }
        }


        private void UpdateTarget(HealthBarTarget target, Camera cam)
        {
            var health = target.Health;
            var normalized = health.Max <= 0 ? 0f : (float)health.Current / health.Max;

            var needsBar = !hideWhenFull || normalized < 1f;
            if (!needsBar)
            {
                if (active.TryGetValue(target, out var idle))
                {
                    Release(idle);
                    finished.Add(target);
                }

                return;
            }

            if (!active.TryGetValue(target, out var view))
            {
                view = Take();
                if (view == null)
                    return;

                // Заводим полосу целой и только потом отдаём текущее значение: полоса
                // появляется уже после первого попадания, и без этого шага хвост от него
                // не показался бы — бар просто возник бы наполовину пустым.
                view.ResetTo(1f);
                view.SetValue(normalized);

                active[target] = view;
            }

            // Каждый кадр, а не только при выдаче: модификатор может поднять запас здоровья
            // посреди боя, и полоса должна вырасти следом. Сам вид отсеет повтор.
            view.SetMaxHealth(health.Max);

            var screenPoint = cam.WorldToScreenPoint(target.WorldPosition);

            if (IsOffscreen(cam, screenPoint))
            {
                view.gameObject.SetActive(false);
                return;
            }

            view.gameObject.SetActive(true);

            if (health.Current <= 0)
                view.SetDead();
            else
                view.SetValue(normalized);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, uiCamera, out var localPoint);
            view.SelfRect.localPosition = localPoint;
        }

        // Убираем из словаря отдельным проходом: нельзя менять его, пока идём по целям.
        private void DropFinished()
        {
            if (finished.Count == 0)
                return;

            for (var i = 0; i < finished.Count; i++)
                active.Remove(finished[i]);

            finished.Clear();
        }


        private HealthBarView Take()
        {
            return pool.Take(root);
        }

        private void Release(HealthBarView view)
        {
            pool.Release(view);
        }

        private void Prewarm()
        {
            if (prewarmCount > 0)
                pool.Prewarm(prewarmCount, root);
        }


        private bool TryGetWorldCamera(out Camera cam)
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            cam = worldCamera;

            return cam != null;
        }

        /// <summary> z &lt;= 0 — точка за спиной камеры, там WorldToScreenPoint зеркалит координаты. </summary>
        private bool IsOffscreen(Camera cam, Vector3 screenPoint)
        {
            if (screenPoint.z <= 0f)
                return true;

            var marginX = cam.pixelWidth * offScreenMargin;
            var marginY = cam.pixelHeight * offScreenMargin;

            return screenPoint.x < -marginX || screenPoint.x > cam.pixelWidth + marginX ||
                   screenPoint.y < -marginY || screenPoint.y > cam.pixelHeight + marginY;
        }

        /// <summary> Overlay-канвасу нужен null, остальным — камера канваса. </summary>
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
    }
}
