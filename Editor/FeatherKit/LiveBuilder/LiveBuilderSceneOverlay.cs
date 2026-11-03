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
            if (!BoxModelActions.IsToolActive)
            {
                if (GUILayout.Button(new GUIContent("Live Builder (B)", "Включить инструмент сборки уровня")))
                    BoxModelActions.SetToolActive(true);

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
            var tools = LiveBuilderToolSetting.PaletteOrder;
            var current = System.Array.IndexOf(tools, LiveBuilderToolSetting.Current);
            var contents = new GUIContent[tools.Length];

            for (var index = 0; index < tools.Length; index++)
                contents[index] = new GUIContent(LiveBuilderToolIcons.Themed(tools[index]),
                    LiveBuilderToolSetting.DisplayName(tools[index]) + " (" + LiveBuilderToolSetting.HotkeyOf(tools[index]) + ") — " + LiveBuilderToolSetting.Tooltip(tools[index]));

            var chosen = GUILayout.Toolbar(current, contents, EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.Width(tools.Length * 28f));

            if (chosen != current)
                LiveBuilderToolSetting.Current = tools[chosen];
        }


        private static void DrawStorey()
        {
            if (GUILayout.Button(new GUIContent("◀", "Этаж ниже (PageDown)"), EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                LiveBuilderStoreys.CurrentStorey--;

            GUILayout.Label(new GUIContent("Этаж " + LiveBuilderStoreys.CurrentStorey, "Пол " + BoxModelSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м"),
                EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            if (GUILayout.Button(new GUIContent("▶", "Этаж выше (PageUp)"), EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                LiveBuilderStoreys.CurrentStorey++;
        }


        // Всё, комнаты (разрез на 3 м над полом этажа), стены вниз (на 1 м). Только в окне сцены.
        private static void DrawVisibility()
        {
            var isSectioned = LiveBuilderStoreys.IsSectionEnabled;
            var height = LiveBuilderStoreys.SectionHeightAboveFloor;
            var isRooms = isSectioned && Mathf.Approximately(height, 3f);
            var isLowWalls = isSectioned && Mathf.Approximately(height, 1f);

            if (GUILayout.Toggle(!isSectioned, new GUIContent("Всё", "Всё как есть"), EditorStyles.miniButtonLeft) && isSectioned)
                LiveBuilderStoreys.IsSectionEnabled = false;

            if (GUILayout.Toggle(isRooms, new GUIContent("Комнаты", "Выше 3 м над полом этажа спрятано"), EditorStyles.miniButtonMid) && !isRooms)
                SetSection(3f);

            if (GUILayout.Toggle(isLowWalls, new GUIContent("Стены вниз", "Выше 1 м над полом этажа спрятано"), EditorStyles.miniButtonRight) && !isLowWalls)
                SetSection(1f);
        }

        private static void SetSection(float heightAboveFloor)
        {
            LiveBuilderStoreys.SectionHeightAboveFloor = heightAboveFloor;
            LiveBuilderStoreys.IsSectionEnabled = true;
        }
    }
}
