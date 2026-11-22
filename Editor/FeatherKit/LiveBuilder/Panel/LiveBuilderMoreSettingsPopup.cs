using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Всплывающее окошко «⋯» строки инструмента в панели: редкие настройки нового (<see cref="LiveBuilderTool.DrawMoreSettings"/>).
    /// Закрывается кликом мимо, как меню; длинное прокручивается.
    /// </summary>
    public sealed class LiveBuilderMoreSettingsPopup : PopupWindowContent
    {
        private const float Width = 300f;
        private const float MaxHeight = 460f;

        private static GUIStyle paddingStyle;

        private readonly LiveBuilderTool tool;
        private Vector2 scrollPosition;
        private float contentHeight = 200f;


        private LiveBuilderMoreSettingsPopup(LiveBuilderTool tool)
        {
            this.tool = tool;
        }


        private static GUIStyle PaddingStyle => paddingStyle ??= new GUIStyle { padding = new RectOffset(6, 6, 6, 6) };


        public static void Show(Rect buttonRect, LiveBuilderTool tool)
        {
            PopupWindow.Show(buttonRect, new LiveBuilderMoreSettingsPopup(tool));
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(Width, Mathf.Min(contentHeight + 12f, MaxHeight));
        }


        public override void OnGUI(Rect rect)
        {
            EditorGUIUtility.labelWidth = 120f;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            var content = EditorGUILayout.BeginVertical(PaddingStyle);
            EditorGUILayout.LabelField(tool.DisplayName + " — more", EditorStyles.miniBoldLabel);
            tool.DrawMoreSettings();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            // Высоту окошко узнаёт после первой разметки — подстраивается под содержимое.
            if (Event.current.type == EventType.Repaint && content.height > 1f && !Mathf.Approximately(content.height, contentHeight))
            {
                contentHeight = content.height;
                editorWindow.Repaint();
            }
        }
    }
}
