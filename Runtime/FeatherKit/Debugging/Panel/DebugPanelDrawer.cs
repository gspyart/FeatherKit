using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Единственный, кто рисует панели отладки. Заводится сам при первой записи в
    /// <see cref="DebugPanel"/> и живёт весь запуск — в сцену класть не надо.
    ///
    /// Один OnGUI на все окна: у каждого свой прямоугольник, и раскладывать их
    /// в столбик можно только зная про всех сразу.
    ///
    /// Текст не обрезается никогда. Не влезло значение в строку — оно переезжает под
    /// название и переносится по словам. Обрезанное значение хуже бесполезного: «12.3…»
    /// читается как настоящее число, и на него ведутся.
    ///
    /// Как выглядит — целиком в <see cref="DebugPanelStyle"/>. Высоты считаются по шрифту
    /// и по самому тексту, в настройках их нет.
    /// </summary>
    public class DebugPanelDrawer : MonoBehaviour
    {
        private static DebugPanelDrawer instance;

        // Один на все замеры: CalcSize зовётся десятки раз за кадр, и плодить GUIContent
        // на каждый вызов — это мусор в каждом кадре отладки.
        private static readonly GUIContent measured = new GUIContent();

        // Сколько уже занято в каждом углу — чтобы следующее окно встало под предыдущим.
        private readonly float[] takenByAnchor = new float[4];

        // Высота каждой строки текущего окна: у строк с переносом она своя.
        private readonly List<float> rowHeights = new List<float>();

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle valueWrapStyle;
        private GUIStyle boxStyle;

        private Texture2D backgroundTexture;
        private Texture2D separatorTexture;

        private float titleHeight;
        private float lineHeight;

        // Стили собраны под конкретный набор настроек. Подменили настройки — пересобираем,
        // иначе панель осталась бы со старым шрифтом до перезапуска.
        private DebugPanelStyle builtFor;


        // internal: рисовалка должна быть одна, и заводит её DebugPanel. Вторая рисовала бы
        // те же окна поверх первых.
        internal static void EnsureExists()
        {
            // В редакторе вне плей-мода объект создавать нельзя — он попал бы в сцену
            // и сохранился в ней.
            if (instance != null || !Application.isPlaying)
                return;

            var host = new GameObject("DebugPanel");
            DontDestroyOnLoad(host);

            instance = host.AddComponent<DebugPanelDrawer>();
        }


        private void Draw(DebugPanelWindow window, Vector2 screen, DebugPanelStyle style)
        {
            // Не шире и не выше заданной доли экрана: в портретной ориентации фиксированная
            // ширина не оставляет места второму столбику, а длинное окно закрывает игру.
            var width = Mathf.Min(style.Width, screen.x * style.MaxScreenFraction - style.Margin * 2f);
            var innerWidth = width - style.Padding * 2f;

            if (innerWidth <= 0f)
                return;

            MeasureRows(window, innerWidth, style);

            var header = titleHeight + style.TitleSpacing + style.SeparatorHeight + style.SeparatorSpacing;
            var maxHeight = screen.y * style.MaxScreenFraction;

            var visibleRows = VisibleRows(maxHeight - header - style.Padding * 2f, style);
            var height = header + style.Padding * 2f + ContentHeight(visibleRows, style);

            var anchor = (int)window.Anchor;
            var taken = takenByAnchor[anchor];

            var isLeft = window.Anchor == DebugAnchor.TopLeft || window.Anchor == DebugAnchor.BottomLeft;
            var isTop = window.Anchor == DebugAnchor.TopLeft || window.Anchor == DebugAnchor.TopRight;

            var x = isLeft ? style.Margin : screen.x - width - style.Margin;
            // Снизу столбик растёт вверх, поэтому высоту окна вычитаем, а не прибавляем.
            var y = isTop ? taken : screen.y - taken - height;

            takenByAnchor[anchor] = taken + height + style.WindowGap;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, boxStyle);

            var innerX = x + style.Padding;
            var cursor = y + style.Padding;

            GUI.Label(new Rect(innerX, cursor, innerWidth, titleHeight), window.Title, titleStyle);
            cursor += titleHeight + style.TitleSpacing;

            if (style.SeparatorHeight > 0f && separatorTexture != null)
                GUI.DrawTexture(new Rect(innerX, cursor, innerWidth, style.SeparatorHeight), separatorTexture);

            // Отступ считаем даже без черты: это просвет до первой строки, а не поле вокруг неё.
            cursor += style.SeparatorHeight + style.SeparatorSpacing;

            var hidden = window.RowCount - visibleRows;

            for (var i = 0; i < visibleRows; i++)
            {
                // Последняя строка уходит под счётчик остатка — молча обрезать список нельзя,
                // иначе кажется, что свойство просто не зарегистрировалось.
                if (hidden > 0 && i == visibleRows - 1)
                {
                    GUI.Label(new Rect(innerX, cursor, innerWidth, lineHeight), $"…ещё {hidden + 1}", labelStyle);
                    break;
                }

                DrawRow(new Rect(innerX, cursor, innerWidth, rowHeights[i]),
                    window.LabelAt(i), window.ValueAt(i), style);

                cursor += rowHeights[i] + style.RowSpacing;
            }
        }

        // Влезает в одну строку — рисуем в две колонки. Не влезает — название сверху,
        // значение под ним целиком, с переносом по словам.
        private void DrawRow(Rect row, string label, string value, DebugPanelStyle style)
        {
            if (row.height <= lineHeight + 0.5f)
            {
                var valueWidth = Measure(valueStyle, value);

                GUI.Label(new Rect(row.x, row.y, row.width - valueWidth - style.ColumnGap, lineHeight),
                    label, labelStyle);

                GUI.Label(new Rect(row.xMax - valueWidth, row.y, valueWidth, lineHeight),
                    value, valueStyle);

                return;
            }

            GUI.Label(new Rect(row.x, row.y, row.width, lineHeight), label, labelStyle);

            var indent = Mathf.Min(style.WrapIndent, row.width);

            GUI.Label(new Rect(row.x + indent, row.y + lineHeight, row.width - indent, row.height - lineHeight),
                value, valueWrapStyle);
        }

        private void MeasureRows(DebugPanelWindow window, float innerWidth, DebugPanelStyle style)
        {
            rowHeights.Clear();

            for (var i = 0; i < window.RowCount; i++)
            {
                var label = window.LabelAt(i);
                var value = window.ValueAt(i);

                var oneLine = Measure(labelStyle, label) + style.ColumnGap + Measure(valueStyle, value);

                if (oneLine <= innerWidth)
                {
                    rowHeights.Add(lineHeight);
                    continue;
                }

                // Переехало вниз: высоту спрашиваем у самого текста — длинное значение
                // займёт столько строк, сколько ему нужно.
                var indent = Mathf.Min(style.WrapIndent, innerWidth);

                measured.text = value;
                var valueHeight = valueWrapStyle.CalcHeight(measured, innerWidth - indent);

                rowHeights.Add(lineHeight + valueHeight);
            }
        }

        private float ContentHeight(int rowCount, DebugPanelStyle style)
        {
            var total = 0f;

            for (var i = 0; i < rowCount; i++)
                total += rowHeights[i];

            return total + Mathf.Max(0, rowCount - 1) * style.RowSpacing;
        }

        // Сколько строк влезает в отведённую высоту. Хотя бы одна — окно без строк бессмысленно.
        private int VisibleRows(float available, DebugPanelStyle style)
        {
            var used = 0f;

            for (var i = 0; i < rowHeights.Count; i++)
            {
                var next = used + rowHeights[i] + (i > 0 ? style.RowSpacing : 0f);

                if (next > available)
                    return Mathf.Max(1, i);

                used = next;
            }

            return rowHeights.Count;
        }

        // Стили нельзя собрать в Awake: GUI.skin существует только внутри OnGUI.
        private void EnsureStyles(DebugPanelStyle style)
        {
            if (builtFor == style && titleStyle != null)
                return;

            builtFor = style;

            titleStyle = Build(style.Title, TextAnchor.MiddleLeft, false);
            labelStyle = Build(style.Label, TextAnchor.MiddleLeft, false);
            valueStyle = Build(style.Value, TextAnchor.MiddleRight, false);

            // Переехавшее вниз значение прижимаем влево: многострочный текст враспор
            // по правому краю читается рвано.
            valueWrapStyle = Build(style.Value, TextAnchor.UpperLeft, true);

            // Высоты берём у самого шрифта: так текст не обрезается ни при каком размере,
            // и в настройках остаётся только сам размер.
            titleHeight = Height(titleStyle);
            lineHeight = Mathf.Max(Height(labelStyle), Height(valueStyle));

            backgroundTexture = Solid(backgroundTexture, style.Background);
            separatorTexture = Solid(separatorTexture, style.SeparatorColor);

            // Подложка своей текстурой: у стандартного GUI.Box своя рамка и свой цвет,
            // прозрачность у него не настроить.
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = backgroundTexture;
            boxStyle.border = new RectOffset();
        }

        private static GUIStyle Build(DebugTextStyle source, TextAnchor alignment, bool wrap)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = alignment,
                fontSize = source.FontSize,
                fontStyle = source.FontStyle,
                // Свой отступ у скина сдвигает текст и врёт про высоту — считаем сами.
                padding = new RectOffset(),
                margin = new RectOffset(),
                wordWrap = wrap,
                clipping = TextClipping.Overflow
            };

            style.normal.textColor = source.Color;

            return style;
        }

        // Строка с выносными элементами: у «Ag» есть и верхний, и нижний край, поэтому
        // высота получается честная, а не по конкретному тексту окна.
        private static float Height(GUIStyle style)
        {
            measured.text = "Ag";

            return Mathf.Ceil(style.CalcSize(measured).y);
        }

        private static float Measure(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            measured.text = text;

            return style.CalcSize(measured).x;
        }

        private static Texture2D Solid(Texture2D texture, Color color)
        {
            if (texture == null)
                texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };

            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        private void ResetColumns(DebugPanelStyle style)
        {
            for (var i = 0; i < takenByAnchor.Length; i++)
                takenByAnchor[i] = style.Margin;
        }


        /// <summary>
        /// Сброс при входе в игру: ссылка статическая и с выключенной перезагрузкой домена
        /// пережила бы выход из плей-мода вместе с уничтоженным объектом.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
        }


        private void OnDestroy()
        {
            if (backgroundTexture != null)
                Destroy(backgroundTexture);

            if (separatorTexture != null)
                Destroy(separatorTexture);
        }

        private void OnGUI()
        {
            var windows = DebugPanel.Windows;

            if (!DebugPanel.IsVisible || windows.Count == 0)
                return;

            var style = DebugPanel.Style;

            EnsureStyles(style);

            // На телефоне пиксели мельче, и текст в честных пикселях читать невозможно.
            // Масштабируем весь слой разом, а координаты считаем уже в масштабированных.
            var scale = DebugPanel.Scale > 0f
                ? DebugPanel.Scale
                : Mathf.Clamp(Screen.height / style.ReferenceHeight, style.ScaleRange.x, style.ScaleRange.y);

            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var screen = new Vector2(Screen.width / scale, Screen.height / scale);

            ResetColumns(style);

            for (var i = 0; i < windows.Count; i++)
                Draw(windows[i], screen, style);

            GUI.matrix = previous;
        }
    }
}
