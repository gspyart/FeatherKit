using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона инструмента окна Live Builder: значок и имя выбранного инструмента в заголовке, под ним — его настройки,
    /// только те, что на него действуют (<see cref="LiveBuilderTool.DrawSettings"/>). У инструментов, которые ставят своё в здание, —
    /// ещё карточка «В здании». Уже созданное не меняет: его числа — в зоне выделенного (<see cref="LiveBuilderSelectionZone"/>).
    /// </summary>
    public sealed class LiveBuilderToolZone : LiveBuilderWindowZone
    {
        // ── Зона ─────────────────────────────────────────────────────────

        public override string Title => LiveBuilderToolPalette.Current.DisplayName;

        public override string Detail => "инструмент";

        public override Texture2D Icon => LiveBuilderToolIcons.White(LiveBuilderToolPalette.Current);

        public override LiveBuilderZoneColor ZoneColor => LiveBuilderZoneColor.Tool;

        public override int Order => 1;

        public override string PreferenceKey => "Tool";


        public override void Draw()
        {
            var tool = LiveBuilderToolPalette.Current;
            tool.DrawSettings();

            if (tool.PlacesIntoBuilding)
                LiveBuilderToolSettingsFields.DrawBuildingStoreysCard();
        }
    }
}
