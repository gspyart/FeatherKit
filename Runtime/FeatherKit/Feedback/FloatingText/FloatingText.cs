using TMPro;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Одна летящая надпись. Префаб и есть стиль: цвет, размер и шрифт настраиваются в TMP.
    /// Ничего не считает — <see cref="FloatingTextController"/> каждый кадр говорит, где быть.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingText : MonoBehaviour
    {
        /// <summary> TMP пересобирает меш на каждое изменение цвета, поэтому меняем шагами. </summary>
        private const float ColorStep = 1f / 32f;

        [Tooltip("Сама надпись. Здесь же настраивается весь вид надписи — шрифт, размер, цвет: " +
                 "анимация к этому цвету сходится. Если пусто — ищется в детях")]
        [SerializeField] private TextMeshProUGUI textMesh;

        [Header("Полёт")]
        [Tooltip("Необязательно. Если пусто - анимация по умолчанию из контроллера.")]
        [SerializeField] private FloatingTextAnimationConfig animationConfig;

        [Tooltip("Множитель размера поверх кривой из анимации.")]
        [SerializeField, Min(0.01f)] private float extraScale = 1f;

        [Tooltip("Синтаксис TMP: {0} или {0:0} - целое, {0:00} - с ведущим нулём, {0:0.0} - с дробной частью.\n" +
                 "Текст вокруг пишется как есть: \"+{0:0}\", \"{0:0}!\".")]
        [SerializeField] private string numberFormat = "{0:0}";

        private RectTransform selfRect;
        private Color baseColor = Color.white;
        private Color appliedColor;
        private bool isCached;

        /// <summary> Цвет, настроенный в префабе - к нему сходится анимация. </summary>
        public Color BaseColor
        {
            get
            {
                EnsureCached();
                return baseColor;
            }
        }

        public FloatingTextAnimationConfig AnimationConfig => animationConfig;

        public float ExtraScale => extraScale;

        public RectTransform SelfRect
        {
            get
            {
                EnsureCached();
                return selfRect;
            }
        }

        /// <summary> Без аллокаций: TMP форматирует во внутренний буфер. </summary>
        public void Prepare(int value)
        {
            EnsureCached();

            if (textMesh == null)
                return;

            appliedColor = Color.clear; // после пула цвет остался с прошлой жизни - применим заново
            textMesh.SetText(numberFormat, value);
        }

        /// <summary> localPosition, а не anchoredPosition - не зависит от якорей префаба. </summary>
        public void SetTransform(Vector2 localPosition, float scale, float rotation)
        {
            selfRect.localPosition = localPosition;
            selfRect.localScale = new Vector3(scale, scale, 1f);
            selfRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        public void SetColor(Color color)
        {
            if (textMesh == null)
                return;

            if (Mathf.Abs(color.r - appliedColor.r) < ColorStep &&
                Mathf.Abs(color.g - appliedColor.g) < ColorStep &&
                Mathf.Abs(color.b - appliedColor.b) < ColorStep &&
                Mathf.Abs(color.a - appliedColor.a) < ColorStep)
                return;

            appliedColor = color;
            textMesh.color = color;
        }

        /// <summary> Сверяемся с текущим состоянием: лишний SetActive - это ребилд канваса. </summary>
        public void SetVisible(bool value)
        {
            if (gameObject.activeSelf == value)
                return;

            gameObject.SetActive(value);
        }

        /// <summary> Лениво: пул может дёрнуть вьюху до её Awake. </summary>
        private void EnsureCached()
        {
            if (isCached)
                return;

            isCached = true;
            selfRect = (RectTransform)transform;

            if (textMesh == null)
                textMesh = GetComponentInChildren<TextMeshProUGUI>(true);

            if (textMesh != null)
            {
                // Сотня райкаст-таргетов на канвасе бьёт по каждому тапу.
                textMesh.raycastTarget = false;
                baseColor = textMesh.color;
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogError($"FloatingText '{name}': не назначен TextMeshProUGUI!", this);
            }
#endif
            if (string.IsNullOrEmpty(numberFormat))
                numberFormat = "{0:0}";
        }

        private void Awake()
        {
            EnsureCached();
        }
    }
}
