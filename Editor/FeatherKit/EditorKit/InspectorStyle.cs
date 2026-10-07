// ═══════════════════════════════════════════════════════════════════════════════════════
// ПАМЯТКА
//
// Это ЕДИНСТВЕННОЕ место, где живут цифры и цвета редакторных инспекторов FeatherKit.
// Подбирать их в коде инспектора нельзя: там они разъедутся по файлам, и через месяц
// половина секций будет выглядеть по-своему.
//
// КАК ПОДКРУТИТЬ, не трогая библиотеку: Create > FeatherKit > Configs > Inspector Style,
// положить ассет в ЛЮБУЮ папку Resources под именем InspectorStyle. Подхватится сам.
// Нет ассета — работают значения по умолчанию, отсюда же.
//
// Две палитры, тёмная и светлая, потому что одна на обе выглядит грязно в одной из них.
// Тему спрашиваем у редактора через EditorGUIUtility.isProSkin.
//
// Свойство Current кэширует ассет в статике. После перезагрузки домена кэш сбрасывается
// сам — специально ничего чистить не надо.
// ═══════════════════════════════════════════════════════════════════════════════════════

using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Оформление редакторных инспекторов FeatherKit: отступы, скругления, цвета секций.
    ///
    /// Отдельным ассетом, потому что цифры тут подбираются глазами и зависят от темы
    /// редактора. Менять их пересборкой библиотеки — гарантия, что никто не станет.
    ///
    /// Ассет необязателен: не нашли — берутся эти же значения по умолчанию.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Configs/Inspector Style", fileName = "InspectorStyle")]
    public class InspectorStyle : ScriptableObject
    {
        /// <summary>Имя ассета в любой папке Resources. Положил такой — подхватится сам.</summary>
        public const string Resource = "InspectorStyle";

        private static InspectorStyle current;

        [Header("Секция")]
        [Tooltip("Скругление углов секции, точек")]
        [SerializeField] private float cornerRadius = 6f;

        [Tooltip("Отступ содержимого от края секции, точек")]
        [SerializeField] private float padding = 8f;

        [Tooltip("Просвет между секциями, точек")]
        [SerializeField] private float sectionGap = 6f;

        [Tooltip("Толщина обводки секции. 0 — без обводки")]
        [SerializeField] private float outlineThickness = 1f;

        [Header("Заголовок")]
        [Tooltip("Высота полосы заголовка, точек")]
        [SerializeField] private float headerHeight = 22f;

        [Tooltip("Размер шрифта заголовка. 0 — как в стандартном скине")]
        [SerializeField] private int headerFontSize = 12;

        [Tooltip("Ширина цветной метки слева от заголовка, точек. 0 — без метки")]
        [SerializeField] private float accentWidth = 3f;

        [Header("Цвета (тёмная тема)")]
        [SerializeField] private Color darkFill = new Color(1f, 1f, 1f, 0.03f);
        [SerializeField] private Color darkOutline = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color darkHeader = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color darkTitle = new Color(0.88f, 0.88f, 0.9f);

        [Header("Цвета (светлая тема)")]
        [SerializeField] private Color lightFill = new Color(0f, 0f, 0f, 0.03f);
        [SerializeField] private Color lightOutline = new Color(0f, 0f, 0f, 0.10f);
        [SerializeField] private Color lightHeader = new Color(0f, 0f, 0f, 0.06f);
        [SerializeField] private Color lightTitle = new Color(0.15f, 0.15f, 0.17f);

        [Header("Акценты секций")]
        [Tooltip("Палитра цветных меток. Секции разбирают её по кругу")]
        [SerializeField] private Color[] accents =
        {
            new Color(0.45f, 0.72f, 1.00f),
            new Color(1.00f, 0.78f, 0.35f),
            new Color(0.60f, 0.85f, 0.55f),
            new Color(0.85f, 0.60f, 1.00f),
            new Color(1.00f, 0.55f, 0.50f),
            new Color(0.50f, 0.90f, 0.85f)
        };


        /// <summary>Оформление из Resources, а нет ассета — значения по умолчанию.</summary>
        public static InspectorStyle Current
        {
            get
            {
                if (current != null)
                    return current;

                current = Resources.Load<InspectorStyle>(Resource);

                return current != null ? current : current = CreateInstance<InspectorStyle>();
            }
        }


        public float CornerRadius => cornerRadius;
        public float Padding => padding;
        public float SectionGap => sectionGap;
        public float OutlineThickness => outlineThickness;
        public float HeaderHeight => headerHeight;
        public int HeaderFontSize => headerFontSize;
        public float AccentWidth => accentWidth;

        // Тему берём у самого редактора: одна палитра на обе выглядит грязно в одной из них.
        public Color Fill => EditorGUIUtility.isProSkin ? darkFill : lightFill;
        public Color Outline => EditorGUIUtility.isProSkin ? darkOutline : lightOutline;
        public Color Header => EditorGUIUtility.isProSkin ? darkHeader : lightHeader;
        public Color Title => EditorGUIUtility.isProSkin ? darkTitle : lightTitle;

        public Color Accent(int index)
        {
            if (accents == null || accents.Length == 0)
                return Color.gray;

            return accents[Mathf.Abs(index) % accents.Length];
        }
    }
}
