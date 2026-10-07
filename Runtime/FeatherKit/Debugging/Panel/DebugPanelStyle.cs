using System;
using UnityEngine;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Оформление текста: размер, начертание, цвет. Одинаково настраивается у заголовка,
    /// у названия параметра и у его значения — поэтому три отдельных блока, а не один
    /// «шрифт панели».
    /// </summary>
    [Serializable]
    public class DebugTextStyle
    {
        [Tooltip("Размер шрифта, точек. 0 — как в стандартном скине Unity")]
        public int FontSize;

        [Tooltip("Начертание")]
        public FontStyle FontStyle = FontStyle.Normal;

        public Color Color = Color.white;
    }


    /// <summary>
    /// Как выглядит панель отладки.
    ///
    /// Высоты строк и заголовка тут не задаются намеренно — они считаются по самому шрифту.
    /// Заданные числом, они разъезжаются с размером текста, и заголовок начинает обрезаться,
    /// стоит поставить шрифт крупнее.
    ///
    /// Ассет необязателен: нет — панель берёт эти же значения по умолчанию.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Configs/Debug Panel Style", fileName = "DebugPanelStyle")]
    public class DebugPanelStyle : ScriptableObject
    {
        [Header("Текст")]
        [SerializeField] private DebugTextStyle title = new DebugTextStyle
        {
            FontSize = 14,
            FontStyle = FontStyle.Bold,
            Color = new Color(1f, 0.85f, 0.4f)
        };

        [Tooltip("Название параметра — слева в строке")]
        [SerializeField] private DebugTextStyle label = new DebugTextStyle
        {
            FontSize = 12,
            Color = new Color(0.75f, 0.75f, 0.75f)
        };

        [Tooltip("Значение — справа в строке. Держи ярче названия: глазами ищут именно его")]
        [SerializeField] private DebugTextStyle value = new DebugTextStyle
        {
            FontSize = 12,
            FontStyle = FontStyle.Bold,
            Color = Color.white
        };

        [Header("Размеры")]
        [Tooltip("Ширина окна, точек. Больше — влезают длинные значения, но панель лезет в кадр")]
        [SerializeField] private float width = 260f;

        [Tooltip("Отступ окна от края экрана, точек")]
        [SerializeField] private float margin = 8f;

        [Tooltip("Отступ содержимого от края окна, точек")]
        [SerializeField] private float padding = 8f;

        [Tooltip("Просвет между строками, точек. Сама высота строки берётся от шрифта")]
        [SerializeField] private float rowSpacing = 2f;

        [Tooltip("Просвет между заголовком и чертой под ним, точек")]
        [SerializeField] private float titleSpacing = 6f;

        [Tooltip("Просвет между чертой и первой строкой, точек")]
        [SerializeField] private float separatorSpacing = 6f;

        [Tooltip("Просвет между названием и значением, точек. Не даёт им слипнуться, " +
                 "когда оба длинные")]
        [SerializeField] private float columnGap = 10f;

        [Tooltip("Отступ слева у значения, когда оно не влезло в строку и переехало вниз, " +
                 "точек. Небольшая лесенка показывает, к какому названию оно относится")]
        [SerializeField] private float wrapIndent = 12f;

        [Tooltip("Просвет между окнами в одном углу, точек")]
        [SerializeField] private float windowGap = 6f;

        [Tooltip("Какую долю экрана окну разрешено занять. Панель не должна закрывать то, " +
                 "что ей же и отлаживают")]
        [Range(0.1f, 1f)]
        [SerializeField] private float maxScreenFraction = 0.5f;

        [Header("Окно")]
        [Tooltip("Подложка. Прозрачность важнее цвета: сквозь панель должно быть видно игру")]
        [SerializeField] private Color background = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("Черта под заголовком. Высота 0 — черты нет")]
        [SerializeField] private float separatorHeight = 1f;

        [SerializeField] private Color separatorColor = new Color(1f, 1f, 1f, 0.15f);

        [Header("Масштаб")]
        [Tooltip("Опорная высота экрана, пикселей. На ней масштаб единичный, на экране выше — " +
                 "панель пропорционально крупнее")]
        [SerializeField] private float referenceHeight = 720f;

        [Tooltip("Насколько сильно панели разрешено вырасти на большом экране")]
        [SerializeField] private Vector2 scaleRange = new Vector2(1f, 3f);


        public DebugTextStyle Title => title;
        public DebugTextStyle Label => label;
        public DebugTextStyle Value => value;

        public float Width => width;
        public float Margin => margin;
        public float Padding => padding;
        public float RowSpacing => rowSpacing;
        public float TitleSpacing => titleSpacing;
        public float SeparatorSpacing => separatorSpacing;
        public float ColumnGap => columnGap;
        public float WrapIndent => wrapIndent;
        public float WindowGap => windowGap;
        public float MaxScreenFraction => maxScreenFraction;

        public Color Background => background;
        public float SeparatorHeight => separatorHeight;
        public Color SeparatorColor => separatorColor;

        public float ReferenceHeight => referenceHeight;
        public Vector2 ScaleRange => scaleRange;


        /// <summary>
        /// Значения по умолчанию. Живут экземпляром, а не константами, чтобы у рисовалки
        /// была ровно одна дорога к настройкам.
        /// </summary>
        public static DebugPanelStyle CreateDefault() => CreateInstance<DebugPanelStyle>();
    }
}
