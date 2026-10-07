using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Мягкий край у прокрутки: гаснет та сторона, за которой есть ещё содержимое.
    /// - в начале списка затенение только в конце, долистали — только в начале;
    /// - нарастает по мере прокрутки, без рывка;
    /// - работает мягким краем маски окошка (RectMask2D), своих картинок не рисует.
    /// </summary>
    [FDescription("Мягкий край у прокрутки: плавно гасит ту сторону, за которой есть ещё содержимое.")]
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollEdgeFade : MonoBehaviour
    {
        [Tooltip("Длина затенения, пикселей раскладки")]
        [Min(0f)]
        [SerializeField] private float fadeLength = 80f;

        private readonly Vector3[] corners = new Vector3[4];

        private ScrollRect scroll;
        private RectMask2D mask;

        // Что считали в прошлый раз: пересчитываем маску, только когда сдвинулось.
        private Vector4 appliedPadding = new Vector4(float.NaN, 0f, 0f, 0f);


        private RectTransform Viewport => scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;


        // ── Затенение ─────────────────────────────────────────────────────

        private void Refresh()
        {
            if (scroll == null || mask == null || scroll.content == null)
                return;

            var viewport = Viewport;

            scroll.content.GetWorldCorners(corners);

            var min = (Vector2)viewport.InverseTransformPoint(corners[0]);
            var max = (Vector2)viewport.InverseTransformPoint(corners[2]);
            var area = viewport.rect;

            // Сколько содержимого спрятано за каждым краем.
            var padding = new Vector4(
                EdgePadding(scroll.horizontal, area.xMin - min.x),
                EdgePadding(scroll.vertical, area.yMin - min.y),
                EdgePadding(scroll.horizontal, max.x - area.xMax),
                EdgePadding(scroll.vertical, max.y - area.yMax));

            if (padding == appliedPadding)
                return;

            appliedPadding = padding;

            // Мягкий край маски растёт в обе стороны от её границы на половину softness:
            // поэтому softness вдвое длиннее затенения.
            var softness = Mathf.RoundToInt(fadeLength * 2f);

            mask.softness = new Vector2Int(scroll.horizontal ? softness : 0, scroll.vertical ? softness : 0);
            mask.padding = padding;
        }

        // Край, за которым пусто, уводим наружу на длину затенения — там всё равно ничего нет,
        // и видимое содержимое у края не гаснет. Чем больше спрятано, тем ближе край.
        private float EdgePadding(bool scrolls, float hidden)
        {
            if (!scrolls || fadeLength <= 0f)
                return 0f;

            return -fadeLength * (1f - Mathf.Clamp01(hidden / fadeLength));
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            scroll = GetComponent<ScrollRect>();
            mask = Viewport.GetComponent<RectMask2D>();

            if (mask == null)
                Debug.LogWarning($"ScrollEdgeFade «{name}»: у окошка прокрутки нет RectMask2D — затенять нечем.", this);
        }

        private void OnEnable()
        {
            appliedPadding = new Vector4(float.NaN, 0f, 0f, 0f);

            Refresh();
        }

        // Каждый кадр, а не по событию прокрутки: содержимое меняется и без неё — список
        // добили пустыми ячейками, вещь купили, — и край должен ответить сразу.
        private void LateUpdate()
        {
            Refresh();
        }
    }
}
