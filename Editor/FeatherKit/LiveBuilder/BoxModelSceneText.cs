using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Надписи инструмента «Live Builder» в окне сцены: размеры рядом с коробкой и строка-подсказка внизу — что делать сейчас.</summary>
    public static class BoxModelSceneText
    {
        private const float HintBottomMargin = 12f;
        private const float HintSidePadding = 10f;

        private static GUIStyle labelStyle;
        private static GUIStyle hintStyle;


        private static GUIStyle LabelStyle => labelStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        private static GUIStyle HintStyle => hintStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 6, 6),
            normal = { textColor = Color.white }
        };


        // Надпись у точки мира — размеры коробки.
        public static void DrawLabel(Vector3 worldPoint, string text)
        {
            Handles.Label(worldPoint, text, LabelStyle);
        }


        // Строка внизу по центру окна сцены.
        public static void DrawHint(SceneView sceneView, string text)
        {
            var content = new GUIContent(text);
            var size = HintStyle.CalcSize(content);
            var viewSize = sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
            var width = Mathf.Min(size.x, viewSize.x - HintSidePadding * 2f);
            var rect = new Rect((viewSize.x - width) / 2f, viewSize.y - size.y - HintBottomMargin, width, size.y);

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
