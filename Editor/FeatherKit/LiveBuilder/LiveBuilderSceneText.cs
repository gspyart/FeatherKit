using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Надписи инструмента «Live Builder» в окне сцены: размеры рядом с коробкой и строка-подсказка внизу — что делать сейчас.</summary>
    [InitializeOnLoad]
    public static class LiveBuilderSceneText
    {
        private const float HintBottomMargin = 12f;
        private const float HintSidePadding = 10f;

        // Насколько надпись отодвинута от своей точки вправо и вверх, пикс.: не под ручкой и не под курсором — стрелка мыши уходит вправо-вниз.
        private const float LabelOffsetX = 14f;
        private const float LabelOffsetY = 8f;

        // Отступ надписи от края окна сцены, пикс.
        private const float LabelEdgeMargin = 4f;

        private static readonly Color BackgroundColor = new(0.1f, 0.1f, 0.1f, 0.9f);

        private static Texture2D background;
        private static GUIStyle labelStyle;
        private static GUIStyle hintStyle;


        // Подложка не сохраняется и сама не удаляется: перед перезагрузкой скриптов — удалить, иначе после каждой копится новая.
        static LiveBuilderSceneText()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (background != null)
                    Object.DestroyImmediate(background);
            };
        }


        // Тёмная подложка: на светлой и пёстрой сцене белый текст читается одинаково.
        private static Texture2D Background
        {
            get
            {
                if (background != null)
                    return background;

                background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                background.SetPixel(0, 0, BackgroundColor);
                background.Apply();
                return background;
            }
        }

        // Стиль пересоздаётся, если подложку убрал движок (смена сцены, сборка мусора).
        private static GUIStyle LabelStyle => labelStyle != null && labelStyle.normal.background != null ? labelStyle : labelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 3, 3),
            normal = { textColor = Color.white, background = Background }
        };

        private static GUIStyle HintStyle => hintStyle != null && hintStyle.normal.background != null ? hintStyle : hintStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            padding = new RectOffset(10, 10, 6, 6),
            normal = { textColor = Color.white, background = Background }
        };


        // Надпись у точки мира — размеры коробки: правее и выше точки, в пределах окна сцены. Точка за камерой — надписи нет.
        public static void DrawLabel(Vector3 worldPoint, string text)
        {
            var guiPoint = HandleUtility.WorldToGUIPointWithDepth(worldPoint);

            if (guiPoint.z < 0f)
                return;

            var content = new GUIContent(text);
            var size = LabelStyle.CalcSize(content);
            var rect = new Rect(guiPoint.x + LabelOffsetX, guiPoint.y - LabelOffsetY - size.y, size.x, size.y);

            if (Camera.current != null)
            {
                var viewSize = Camera.current.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
                rect.x = Mathf.Clamp(rect.x, LabelEdgeMargin, Mathf.Max(LabelEdgeMargin, viewSize.x - size.x - LabelEdgeMargin));
                rect.y = Mathf.Clamp(rect.y, LabelEdgeMargin, Mathf.Max(LabelEdgeMargin, viewSize.y - size.y - LabelEdgeMargin));
            }

            Handles.BeginGUI();
            GUI.Label(rect, content, LabelStyle);
            Handles.EndGUI();
        }


        // Строка внизу по центру окна сцены.
        public static void DrawHint(SceneView sceneView, string text)
        {
            var content = new GUIContent(text);
            var viewSize = sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
            var width = Mathf.Min(HintStyle.CalcSize(content).x, viewSize.x - HintSidePadding * 2f);
            var height = HintStyle.CalcHeight(content, width);
            var rect = new Rect((viewSize.x - width) / 2f, viewSize.y - height - HintBottomMargin, width, height);

            Handles.BeginGUI();
            GUI.Label(rect, content, HintStyle);
            Handles.EndGUI();
        }


        // Метры для надписи: «1,5».
        public static string FormatMeters(float meters)
        {
            return Mathf.Abs(meters).ToString("0.##");
        }
    }
}
