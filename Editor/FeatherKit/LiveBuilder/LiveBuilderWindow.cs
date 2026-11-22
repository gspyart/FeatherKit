using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — настройки уровня, одна страница (<see cref="LiveBuilderLevelPage"/>): проверка, этажи, типы стен, материалы, файлы мешей.
    /// Строят без него: инструменты, выделенное и клавиши — в панели окна сцены (<see cref="LiveBuilderPanelOverlay"/>).
    /// </summary>
    public class LiveBuilderWindow : EditorWindow
    {
        private static GUIStyle pagePadding;

        private readonly LiveBuilderLevelPage levelPage = new();
        private Vector2 scrollPosition;


        private static GUIStyle PagePadding => pagePadding ??= new GUIStyle { padding = new RectOffset(6, 6, 6, 6) };


        [MenuItem("FeatherKit/Live Builder", false, 0)]
        public static void Open()
        {
            var window = GetWindow<LiveBuilderWindow>();
            window.titleContent = new GUIContent("Live Builder", LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Level));
            window.minSize = new Vector2(280f, 240f);
            window.Show();
        }


        // ── Окно ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Подсветка заголовков разделов под мышью.
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            // Подписи полей — не шире половины окна: в узком окне поля не уезжают за край.
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 110f, 160f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            using (new EditorGUILayout.VerticalScope(PagePadding))
                levelPage.Draw();

            EditorGUILayout.EndScrollView();
        }


        // ── Unity-события ────────────────────────────────────────────────

        private void OnEnable()
        {
            wantsMouseMove = true;
            Selection.selectionChanged += Repaint;
            LiveBuilderStoreys.Changed += Repaint;
            LiveBuilderToolPalette.Changed += Repaint;
            LiveBuilderProjectSettings.Changed += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            LiveBuilderStoreys.Changed -= Repaint;
            LiveBuilderToolPalette.Changed -= Repaint;
            LiveBuilderProjectSettings.Changed -= Repaint;
        }
    }
}
