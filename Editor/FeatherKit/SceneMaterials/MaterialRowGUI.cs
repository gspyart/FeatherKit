using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Что сделали мышью со строкой материала.</summary>
    public enum MaterialRowClick
    {
        None,
        Single,
        Context
    }


    /// <summary>
    /// Строка материала, одинаковая во всём окне материалов: фон выбранной, превью, имя
    /// с подписью, метка «откуда он» и мышь — нажатие, правый клик, перетаскивание.
    /// Отдельно потому, что строки рисуют обе вкладки, и материал должен выглядеть
    /// и вести себя в них одинаково.
    ///
    /// Строка режется прямоугольниками слева и справа, а не раскладкой Unity: так строки
    /// одной высоты, и кнопки в них встают ровным столбцом.
    /// </summary>
    public static class MaterialRowGUI
    {
        public const float RowHeight = 38f;
        public const float Gap = 4f;

        private const float PreviewSize = 32f;
        private const float BadgeWidth = 48f;
        private const float BadgeHeight = 16f;
        private const float DragThreshold = 5f;

        /// <summary>Сценовую копию делят несколько слотов: правка уедет сразу всем. Обычно след дублирования объекта.</summary>
        public static readonly Color SharedColor = Pick(new Color(1f, 0.75f, 0.35f), new Color(0.72f, 0.42f, 0.02f));

        private static readonly Color InspectedColor = new Color(0.25f, 0.5f, 0.95f, 0.28f);
        private static readonly Color HoverColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
        private static readonly Color LineColor = new Color(0f, 0f, 0f, 0.15f);
        private static readonly Color SceneColor = Pick(new Color(1f, 0.7f, 0.3f), new Color(0.72f, 0.4f, 0f));
        private static readonly Color ProjectColor = Pick(new Color(0.45f, 0.7f, 1f), new Color(0.1f, 0.38f, 0.75f));
        private static readonly Color BuiltInColor = Pick(new Color(0.65f, 0.65f, 0.65f), new Color(0.4f, 0.4f, 0.4f));
        private static readonly Color EmptyColor = Pick(new Color(1f, 0.45f, 0.4f), new Color(0.75f, 0.15f, 0.1f));

        private static Vector2 dragStart;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle badgeStyle;


        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.label)
        {
            clipping = TextClipping.Clip
        };

        private static GUIStyle SubtitleStyle => subtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            clipping = TextClipping.Clip
        };

        private static GUIStyle BadgeStyle => badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            padding = new RectOffset()
        };


        // ── Строка ───────────────────────────────────────

        /// <summary>Место под строку в раскладке окна. Дальше её режут слева и справа.</summary>
        public static Rect ReserveRow() => GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));


        /// <summary>Фон: выбранная подсвечена, под мышью чуть светлее, снизу тонкая черта.</summary>
        public static void DrawBackground(Rect row, bool isInspected)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (isInspected)
                EditorGUI.DrawRect(row, InspectedColor);
            else if (row.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(row, HoverColor);

            EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), LineColor);
        }


        public static void DrawPreview(ref Rect content, Material material, MaterialPreviewCache previews)
        {
            var rect = CenterVertically(CutLeft(ref content, PreviewSize), PreviewSize);

            if (Event.current.type != EventType.Repaint)
                return;

            var preview = previews.GetPreview(material);

            if (preview != null)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            else
                FeatherEditorGUIBox.Draw(rect, Color.clear, EmptyColor, 1f, 3f);
        }


        /// <summary>Метка справа: откуда материал — сцена, проект, Unity или слот пуст.</summary>
        public static void DrawBadge(ref Rect content, Material material)
        {
            var rect = CenterVertically(CutRight(ref content, BadgeWidth), BadgeHeight);

            if (Event.current.type != EventType.Repaint)
                return;

            var origin = SceneMaterialTools.GetOrigin(material);
            var color = OriginColor(origin);

            FeatherEditorGUIBox.Draw(rect, new Color(color.r, color.g, color.b, 0.14f), new Color(color.r, color.g, color.b, 0.6f), 1f, 3f);

            var textColor = BadgeStyle.normal.textColor;
            BadgeStyle.normal.textColor = color;
            GUI.Label(rect, OriginTitle(origin), BadgeStyle);
            BadgeStyle.normal.textColor = textColor;
        }

        private static Color OriginColor(MaterialOrigin origin) => origin switch
        {
            MaterialOrigin.Scene => SceneColor,
            MaterialOrigin.Project => ProjectColor,
            MaterialOrigin.BuiltIn => BuiltInColor,
            _ => EmptyColor
        };

        private static string OriginTitle(MaterialOrigin origin) => origin switch
        {
            MaterialOrigin.Scene => "сцена",
            MaterialOrigin.Project => "проект",
            MaterialOrigin.BuiltIn => "unity",
            _ => "пусто"
        };


        /// <summary>Имя сверху, подпись под ним. Цвет подписи — когда в ней предупреждение.</summary>
        public static void DrawTitle(Rect content, string title, string subtitle, Color? subtitleColor = null)
        {
            var titleRect = new Rect(content.x, content.y + 3f, content.width, 18f);
            var subtitleRect = new Rect(content.x, content.y + 19f, content.width, 15f);

            GUI.Label(titleRect, title, TitleStyle);

            var color = GUI.contentColor;

            if (subtitleColor.HasValue)
                GUI.contentColor = subtitleColor.Value;

            GUI.Label(subtitleRect, subtitle, SubtitleStyle);
            GUI.contentColor = color;
        }


        /// <summary>
        /// Мышь над строкой: нажатие, правый клик и перетаскивание материала наружу — в поле,
        /// в инспектор, на объект в сцене. Звать ПОСЛЕ кнопок внутри строки, иначе строка
        /// заберёт их нажатия себе.
        /// </summary>
        public static MaterialRowClick HandleMouse(Rect row, Material material)
        {
            var id = GUIUtility.GetControlID(FocusType.Passive, row);
            var current = Event.current;

            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && row.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    dragStart = current.mousePosition;
                    current.Use();
                    return MaterialRowClick.Single;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    if ((current.mousePosition - dragStart).magnitude < DragThreshold)
                        break;

                    GUIUtility.hotControl = 0;
                    current.Use();

                    if (material != null)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new Object[] { material };
                        DragAndDrop.StartDrag(material.name);
                    }

                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;

                case EventType.ContextClick when row.Contains(current.mousePosition):
                    current.Use();
                    return MaterialRowClick.Context;
            }

            return MaterialRowClick.None;
        }


        // ── Разметка ─────────────────────────────────────

        /// <summary>Отрезает кусок слева; то, что осталось, сдвигается на ширину куска и зазор.</summary>
        public static Rect CutLeft(ref Rect rect, float width)
        {
            var cut = new Rect(rect.x, rect.y, width, rect.height);
            rect.xMin += width + Gap;

            return cut;
        }


        public static Rect CutRight(ref Rect rect, float width)
        {
            var cut = new Rect(rect.xMax - width, rect.y, width, rect.height);
            rect.xMax -= width + Gap;

            return cut;
        }


        public static Rect CenterVertically(Rect rect, float height) =>
            new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, rect.width, height);


        // ── Текст ────────────────────────────────────────

        /// <summary>Число с существительным в нужной форме: 1 слот, 2 слота, 5 слотов.</summary>
        public static string CountText(int count, string one, string few, string many)
        {
            var lastTwo = count % 100;
            var last = count % 10;

            var word = lastTwo >= 11 && lastTwo <= 14 ? many
                : last == 1 ? one
                : last >= 2 && last <= 4 ? few
                : many;

            return $"{count} {word}";
        }


        public static string CountSlots(int count) => CountText(count, "слот", "слота", "слотов");


        public static string CountObjects(int count) => CountText(count, "объект", "объекта", "объектов");


        // ── Общее ────────────────────────────────────────

        // Цвет подписи должен читаться и на тёмной теме редактора, и на светлой.
        private static Color Pick(Color darkSkin, Color lightSkin) => EditorGUIUtility.isProSkin ? darkSkin : lightSkin;
    }
}
