using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Панель Live Builder в окне сцены — самое частое под рукой, не отводя глаз от сцены: инструмент, этаж ◀ ▶ и что видно.
    /// Настройки и свойства — в окне Live Builder, кнопка «Окно» его открывает. Live Builder выключен — одна кнопка «включить».
    /// Спрятать и вернуть панель — меню панелей окна сцены, клавиша `.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Live Builder", defaultDisplay = true, defaultDockZone = DockZone.TopToolbar)]
    internal sealed class LiveBuilderSceneOverlay : IMGUIOverlay
    {
        private const string OverlayId = "featherkit-live-builder";


        public override void OnGUI()
        {
            if (!LiveBuilderActivation.IsToolActive)
            {
                if (GUILayout.Button(new GUIContent("Live Builder (B)", "Включить инструмент сборки уровня")))
                    LiveBuilderActivation.SetToolActive(true);

                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawTools();
                GUILayout.Space(8f);
                DrawStorey();
                GUILayout.Space(8f);
                DrawVisibility();
                GUILayout.Space(8f);

                if (GUILayout.Button(new GUIContent("Окно", "Окно Live Builder: настройки нового и свойства выделенного"), EditorStyles.miniButton))
                    LiveBuilderWindow.Open();
            }
        }


        // Значки инструментов, как в окне; название, клавиша и что делает — во всплывающей подсказке.
        private static void DrawTools()
        {
            var tools = LiveBuilderToolPalette.Tools;
            var current = LiveBuilderToolPalette.IndexOf(LiveBuilderToolPalette.Current);
            var contents = new GUIContent[tools.Count];

            for (var index = 0; index < tools.Count; index++)
                contents[index] = new GUIContent(LiveBuilderToolIcons.Themed(tools[index]),
                    tools[index].DisplayName + " (" + LiveBuilderToolPalette.HotkeyOf(tools[index]) + ") — " + tools[index].Tooltip);

            var chosen = GUILayout.Toolbar(current, contents, EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.Width(tools.Count * 28f));

            if (chosen != current)
                LiveBuilderToolPalette.Current = tools[chosen];
        }


        private static void DrawStorey()
        {
            if (GUILayout.Button(new GUIContent("◀", "Этаж ниже (PageDown)"), EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                LiveBuilderStoreys.CurrentStorey--;

            GUILayout.Label(new GUIContent("Этаж " + LiveBuilderStoreys.CurrentStorey, "Пол " + LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м"),
                EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            if (GUILayout.Button(new GUIContent("▶", "Этаж выше (PageUp)"), EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                LiveBuilderStoreys.CurrentStorey++;
        }


        // Всё, комнаты (разрез на 3 м над полом этажа), стены вниз (на 1 м). Только в окне сцены.
        // Виды — общие с окном (<see cref="LiveBuilderViewPresets"/>); своя высота среза — не горит ни одна кнопка.
        private static void DrawVisibility()
        {
            DrawViewButton(LiveBuilderViewPresets.ViewAll, new GUIContent("Всё", "Всё как есть"), EditorStyles.miniButtonLeft);
            DrawViewButton(LiveBuilderViewPresets.ViewRooms, new GUIContent("Комнаты", "Выше 3 м над полом этажа спрятано"), EditorStyles.miniButtonMid);
            DrawViewButton(LiveBuilderViewPresets.ViewWallsDown, new GUIContent("Стены вниз", "Выше 1 м над полом этажа спрятано"), EditorStyles.miniButtonRight);
        }

        private static void DrawViewButton(int preset, GUIContent content, GUIStyle style)
        {
            var isCurrent = LiveBuilderViewPresets.Current() == preset;

            if (GUILayout.Toggle(isCurrent, content, style) && !isCurrent)
                LiveBuilderViewPresets.Apply(preset);
        }
    }
}
