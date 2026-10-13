using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Страница фильтра карт света в Project Settings → FeatherKit → Карты света:
    /// галка «ставить после запекания», выбор фильтра и кнопка «Применить сейчас».
    /// Плюс пункт меню FeatherKit с той же галкой — включить и выключить, не открывая настроек.
    /// </summary>
    public static class LightmapFilterSettingsPage
    {
        private const string SettingsPath = "Project/FeatherKit/Карты света";
        private const string MenuPath = "FeatherKit/Фильтр карт света после запекания";

        private static readonly GUIContent EnabledLabel = new GUIContent("Enabled",
            "Включено — после каждого запекания картам света сам ставится фильтр ниже. Выключено — запекание оставляет плавный.");

        private static readonly GUIContent FilterModeLabel = new GUIContent("Filter Mode",
            "Как рисуются точки света. Point — каждая точка ровным квадратом, как в старых играх. Bilinear — соседние точки плавно смешиваются.");


        private static LightmapFilterSettings Settings => LightmapFilterSettings.instance;


        // ── Страница настроек ────────────────────────────────────────────

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Карты света",
                guiHandler = _ => DrawSettings(),
                keywords = new[] { "lightmap", "filter", "point", "карты света", "запекание" }
            };
        }

        private static void DrawSettings()
        {
            EditorGUILayout.HelpBox("Запекание сбрасывает фильтр карт света на плавный. Включи — и выбранный фильтр будет ставиться сам.",
                MessageType.None);

            Settings.IsEnabled = EditorGUILayout.Toggle(EnabledLabel, Settings.IsEnabled);
            Settings.FilterMode = (FilterMode)EditorGUILayout.EnumPopup(FilterModeLabel, Settings.FilterMode);

            EditorGUILayout.Space();

            if (GUILayout.Button("Применить сейчас"))
                ApplyNow();
        }

        // Ставит фильтр уже запечённым картам, без перепечки.
        private static void ApplyNow()
        {
            if (LightmapSettings.lightmaps.Length == 0)
            {
                Debug.Log("Карты света: в открытых сценах нет запечённых карт.");
                return;
            }

            int changedCount = LightmapFilterApplier.ApplyToLoadedScenes();
            Debug.Log($"Карты света: фильтр {Settings.FilterMode} поставлен, файлов изменено — {changedCount}.");
        }


        // ── Пункт меню ───────────────────────────────────────────────────

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            Settings.IsEnabled = !Settings.IsEnabled;

            if (Settings.IsEnabled)
                LightmapFilterApplier.ApplyToLoadedScenes();
        }

        // Галку ставим при открытии меню: её могли сменить и на странице настроек.
        [MenuItem(MenuPath, validate = true)]
        private static bool UpdateMenuChecked()
        {
            Menu.SetChecked(MenuPath, Settings.IsEnabled);
            return true;
        }
    }
}
