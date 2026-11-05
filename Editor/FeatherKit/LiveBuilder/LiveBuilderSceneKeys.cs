using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Шпаргалка клавиш в углу окна сцены — только то, что работает сейчас: клавиши выбранного инструмента (<see cref="LiveBuilderTool.KeyHints"/>),
    /// того, что выделено (точки контура, грани коробки), и общие. Клавиша «/» прячет и показывает её; спрятана — в углу одна строка, как вернуть.
    /// Полный список — значок клавиатуры в окне Live Builder.
    /// </summary>
    public static class LiveBuilderSceneKeys
    {
        private const string ShownPreferenceKey = "FeatherKit.LiveBuilder.SceneKeysShown";
        private const float Margin = 10f;
        private const float BottomOffset = 46f;
        private const float Width = 330f;

        // Рамка по пустому — только где пустой клик не начинает новую линию (не у «Стены» и «Здания»).
        private const string ContourBoxSelectKey = "точки: рамка по пустому — выбрать, Shift — добавить";

        private static readonly string[] ContourKeys =
        {
            "точки: клик — выбрать, стрелки выбранных — двигать, у стены и вверх",
            "Delete — убрать точки, двойной клик — угол / плавно / Безье"
        };

        private static readonly string[] BoxKeys =
        {
            "квадрат на грани — размер; Alt — обе грани, Alt+Shift — все",
            "Ctrl + тянуть квадрат — выдавить новую коробку",
            "стрелки-ручки — перенос, Shift — копия; точно — «Перенос и копии» в окне"
        };

        private static readonly string[] CommonKeys =
        {
            "1–8 — инструмент · [ ] — сетка · PgUp / PgDn — этаж",
            "голубое кольцо у прицела — прилипло к углу, середине или ребру; S — выключить",
            "Esc — бросить то, что ещё не поставлено; ничего нет — на «Выделение»",
            "/ — спрятать подсказки"
        };

        private static GUIStyle lineStyle;
        private static GUIStyle titleStyle;


        public static bool IsShown
        {
            get => EditorPrefs.GetBool(ShownPreferenceKey, true);
            set => EditorPrefs.SetBool(ShownPreferenceKey, value);
        }

        private static GUIStyle LineStyle => lineStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }, wordWrap = true };

        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };


        // ── Рисование ────────────────────────────────────────────────────

        // Левый нижний угол окна сцены, над строкой подсказки. isContourShown — видны точки выделенной стены или здания.
        public static void Draw(SceneView sceneView, LiveBuilderTool tool, bool isContourShown)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var viewSize = sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
            Handles.BeginGUI();

            if (!IsShown)
            {
                var chipRect = new Rect(Margin, viewSize.y - BottomOffset - 18f, 120f, 18f);
                EditorGUI.DrawRect(chipRect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
                GUI.Label(new Rect(chipRect.x + 6f, chipRect.y + 2f, chipRect.width, chipRect.height), "/ — клавиши", LineStyle);
                Handles.EndGUI();
                return;
            }

            var lines = LinesFor(tool, isContourShown);
            var height = 22f;

            foreach (var line in lines)
                height += LineStyle.CalcHeight(new GUIContent(line), Width - 12f);

            var rect = new Rect(Margin, viewSize.y - BottomOffset - height, Width, height);
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 0.75f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 16f), "Клавиши · " + tool.DisplayName, TitleStyle);

            var y = rect.y + 20f;

            foreach (var line in lines)
            {
                var lineHeight = LineStyle.CalcHeight(new GUIContent(line), Width - 12f);
                GUI.Label(new Rect(rect.x + 6f, y, Width - 12f, lineHeight), line, LineStyle);
                y += lineHeight;
            }

            Handles.EndGUI();
        }

        // Инструмент, потом выделенное, потом общее.
        private static List<string> LinesFor(LiveBuilderTool tool, bool isContourShown)
        {
            var lines = new List<string>(tool.KeyHints);

            if (isContourShown && tool.PathGesture == null)
                lines.Add(ContourBoxSelectKey);

            if (isContourShown)
                lines.AddRange(ContourKeys);
            else if (IsEditableBoxSelected(tool))
                lines.AddRange(BoxKeys);

            lines.AddRange(CommonKeys);
            return lines;
        }

        // Выделена коробка, комната или лестница, у которой инструмент показывает квадраты граней.
        private static bool IsEditableBoxSelected(LiveBuilderTool tool)
        {
            var selected = Selection.activeGameObject;
            return selected != null && BoxModelShapeCollector.IsPiece(selected) && tool.CanEdit(selected);
        }
    }
}
