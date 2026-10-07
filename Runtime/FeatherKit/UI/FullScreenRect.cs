using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Растягивает свой прямоугольник на весь холст, что бы ни делал родитель.
    /// - нужен подложкам внутри <see cref="SafeAreaFitter"/>: заливка должна лечь и под вырез;
    /// - меряет края холста, а не вырез: так чинит любое ужимание родителя;
    /// - только для того, что красит экран: подпись и кнопка под вырезом наполовину исчезнут.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class FullScreenRect : MonoBehaviour
    {
        // Массив под углы просит сама Unity, и заводить его каждый кадр незачем.
        private readonly Vector3[] corners = new Vector3[4];

        private RectTransform selfRect;
        private RectTransform canvasRect;

        private Rect appliedParentArea;
        private Rect appliedCanvasArea;


        private RectTransform Parent => selfRect != null ? selfRect.parent as RectTransform : null;


        // ── Размер ────────────────────────────────────────────────────────

        /// <summary>Растянуть прямоугольник по краям холста. Зовётся само при их смене.</summary>
        public void Apply()
        {
            var parent = Parent;

            if (parent == null || canvasRect == null)
                return;

            canvasRect.GetWorldCorners(corners);

            // Углы холста в координатах родителя: свой сдвиг мы задаём именно в них.
            var min = (Vector2)parent.InverseTransformPoint(corners[0]);
            var max = (Vector2)parent.InverseTransformPoint(corners[2]);

            var area = parent.rect;

            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;

            // Сдвиги считаются от краёв РОДИТЕЛЯ: насколько родитель ужат, настолько
            // мы и вылезаем обратно.
            selfRect.offsetMin = min - area.min;
            selfRect.offsetMax = max - area.max;

            appliedParentArea = area;
            appliedCanvasArea = canvasRect.rect;
        }

        // Одним сравнением ловим всё сразу: и появившийся вырез, и поворот экрана,
        // и пересчёт раскладки у родителя.
        private bool NeedsUpdate()
        {
            var parent = Parent;

            return parent != null && canvasRect != null
                   && (appliedParentArea != parent.rect || appliedCanvasArea != canvasRect.rect);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();

            var canvas = GetComponentInParent<Canvas>(true);

            if (canvas != null)
                canvasRect = (RectTransform)canvas.rootCanvas.transform;
        }

        // Подложка живёт вместе с окном и включается позже слоя безопасной зоны —
        // на включении размер берём заново.
        private void OnEnable()
        {
            Apply();
        }

        // Тем же способом, что и слой безопасной зоны: сравнение двух прямоугольников
        // дешевле, чем ловить поворот экрана и пересчёт раскладки событиями.
        private void Update()
        {
            if (NeedsUpdate())
                Apply();
        }
    }
}
