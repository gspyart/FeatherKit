using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вкладка «Инструмент» окна Live Builder — настройки выбранного инструмента, только те, что на него действуют:
    /// сверху то, что меняют каждый раз (<see cref="LiveBuilderTool.DrawSettings"/>), у инструментов, которые ставят своё в здание, — карточка «В здании»,
    /// ниже свёрнутое «Ещё» с редким (<see cref="LiveBuilderTool.DrawMoreSettings"/>). Уже созданное не меняет: его числа — на вкладке «Выделено» (<see cref="LiveBuilderSelectionTab"/>).
    /// Выбрал инструмент — открывается сама.
    /// </summary>
    public sealed class LiveBuilderToolTab : LiveBuilderWindowTab
    {
        // ── Корешок ──────────────────────────────────────────────────────

        public override string Title => "Инструмент";

        public override Texture2D Icon => LiveBuilderToolIcons.White(LiveBuilderToolPalette.Current);

        public override LiveBuilderTabColor TabColor => LiveBuilderTabColor.Tool;


        // ── Место ────────────────────────────────────────────────────────

        public override int Order => 0;

        public override string PreferenceKey => "Tool";


        // ── Открывается сама ─────────────────────────────────────────────

        public override bool ShouldOpenWhenToolChanges => true;


        // ── Содержимое ───────────────────────────────────────────────────

        public override void Draw()
        {
            var tool = LiveBuilderToolPalette.Current;
            tool.DrawSettings();

            if (tool.PlacesIntoBuilding)
                LiveBuilderToolSettingsFields.DrawBuildingStoreysCard();

            if (tool.MoreSettingsSummary != null && LiveBuilderWindowControls.MoreSection(tool.Id, tool.AreMoreSettingsChanged, tool.MoreSettingsSummary))
                tool.DrawMoreSettings();
        }
    }
}
