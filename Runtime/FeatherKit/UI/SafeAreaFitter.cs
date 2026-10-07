using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Ужимает RectTransform в безопасную зону экрана: без выреза камеры, «домашней полоски» и углов.
    /// - вешается на контейнер под Canvas, а не на кнопки: ужимать надо один раз весь слой;
    /// - стороны отключаются по отдельности: сверху вырез часто хочется оставить фону.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Учитывать вырез сверху")]
        [SerializeField] private bool top = true;

        [Tooltip("Учитывать «домашнюю полоску» снизу")]
        [SerializeField] private bool bottom = true;

        [Tooltip("Учитывать боковые отступы — нужны в горизонтальной ориентации")]
        [SerializeField] private bool sides = true;

        private RectTransform selfRect;
        private Rect appliedArea;
        private ScreenOrientation appliedOrientation;
        private Vector2Int appliedResolution;


        public void Apply()
        {
            // Свёрнутое окно и смена ориентации дают нулевой размер экрана на кадр-другой.
            // Поделив на него, мы записали бы в якоря NaN — и слой исчез бы навсегда,
            // потому что из NaN он уже не восстановится.
            if (selfRect == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            var area = Screen.safeArea;

            if (!top)
                area.yMax = Screen.height;

            if (!bottom)
            {
                area.height += area.y;
                area.y = 0f;
            }

            if (!sides)
            {
                area.width += area.x;
                area.x = 0f;
                area.xMax = Screen.width;
            }

            // Область переводим в доли экрана: якоря живут в них, и тогда рект сам
            // тянется за размером канваса.
            var min = area.position;
            var max = area.position + area.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            selfRect.anchorMin = min;
            selfRect.anchorMax = max;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;

            appliedArea = Screen.safeArea;
            appliedOrientation = Screen.orientation;
            appliedResolution = new Vector2Int(Screen.width, Screen.height);
        }


        private bool NeedsUpdate()
        {
            return appliedArea != Screen.safeArea
                   || appliedOrientation != Screen.orientation
                   || appliedResolution.x != Screen.width
                   || appliedResolution.y != Screen.height;
        }


        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();

            Apply();
        }

        // Поворот экрана и разворачивание окна меняют зону на ходу, поэтому проверяем
        // каждый кадр. Сравнение трёх значений дешевле, чем ловить события поворота.
        private void Update()
        {
            if (NeedsUpdate())
                Apply();
        }
    }
}
