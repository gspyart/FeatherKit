using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Панель Live Builder полосой — когда её пристыковали к краю или к полосе окна сцены (<see cref="LiveBuilderPanelOverlay"/>), на UI Toolkit.
    /// - вкл/выкл и 8 инструментов: клик — выбрать, клавиша — в подсказке
    /// - этаж ◀ N ▶ (в вертикальной ▲ N ▼) и, в горизонтальной, шаг сетки списком
    /// Карточки нет — ей нужна высота; этаж и сетку тогда повторяет подсказка внизу сцены (<see cref="LiveBuilderSceneText.DrawHint"/>).
    /// </summary>
    public sealed class LiveBuilderPanelToolbarElements
    {
        private readonly List<(LiveBuilderTool Tool, EditorToolbarToggle Toggle)> toolToggles = new();
        private readonly EditorToolbarToggle powerToggle;
        private readonly Label storeyLabel;
        private readonly EditorToolbarDropdown gridDropdown;


        // isVertical — колонка у края: этаж стрелками вверх-вниз, без сетки.
        public LiveBuilderPanelToolbarElements(bool isVertical)
        {
            Root = new OverlayToolbar();

            powerToggle = new EditorToolbarToggle { name = "LiveBuilderPower", icon = LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Power) };
            powerToggle.RegisterValueChangedCallback(change => LiveBuilderActivation.SetToolActive(change.newValue));
            Root.Add(powerToggle);

            var toolStrip = new VisualElement();
            toolStrip.style.flexDirection = isVertical ? FlexDirection.Column : FlexDirection.Row;

            foreach (var tool in LiveBuilderToolPalette.Tools)
                toolStrip.Add(CreateToolToggle(tool));

            EditorToolbarUtility.SetupChildrenAsButtonStrip(toolStrip);
            Root.Add(toolStrip);

            storeyLabel = new Label { name = "LiveBuilderStorey", style = { unityTextAlign = TextAnchor.MiddleCenter, minWidth = 18f } };
            Root.Add(CreateStoreyStrip(isVertical));

            if (!isVertical)
            {
                gridDropdown = new EditorToolbarDropdown { tooltip = "Шаг сетки, м: всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены" };
                gridDropdown.clicked += ShowGridMenu;
                Root.Add(gridDropdown);
            }

            Refresh();
        }


        public OverlayToolbar Root { get; }

        public int ToolToggleCount => toolToggles.Count;


        // ── Инструменты ──────────────────────────────────────────────────

        private EditorToolbarToggle CreateToolToggle(LiveBuilderTool tool)
        {
            var toggle = new EditorToolbarToggle { name = "LiveBuilderTool" + tool.Id, icon = LiveBuilderToolIcons.Themed(tool) };
            toggle.RegisterValueChangedCallback(change => SelectTool(tool, change.newValue));
            toolToggles.Add((tool, toggle));
            return toggle;
        }

        // Клик по выбранному не снимает его: какой-то инструмент выбран всегда. Выключенный Live Builder клик по инструменту включает.
        private void SelectTool(LiveBuilderTool tool, bool isOn)
        {
            if (isOn)
            {
                LiveBuilderToolPalette.Current = tool;

                if (!LiveBuilderActivation.IsToolActive)
                    LiveBuilderActivation.SetToolActive(true);
            }

            Refresh();
        }


        // ── Этаж и сетка ─────────────────────────────────────────────────

        private VisualElement CreateStoreyStrip(bool isVertical)
        {
            var strip = new VisualElement();
            strip.style.flexDirection = isVertical ? FlexDirection.Column : FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            strip.tooltip = "На каком этаже строишь — и в пустоте, и в зданиях. PageUp и PageDown в окне сцены";

            var down = new EditorToolbarButton(isVertical ? "▼" : "◀", () => LiveBuilderStoreys.CurrentStorey--) { tooltip = "Этаж ниже (PageDown)" };
            var up = new EditorToolbarButton(isVertical ? "▲" : "▶", () => LiveBuilderStoreys.CurrentStorey++) { tooltip = "Этаж выше (PageUp)" };

            // В колонке выше — сверху, как в здании.
            strip.Add(isVertical ? up : down);
            strip.Add(storeyLabel);
            strip.Add(isVertical ? down : up);
            return strip;
        }


        private void ShowGridMenu()
        {
            var menu = new GenericMenu();

            foreach (var step in LiveBuilderGrid.Steps)
            {
                var chosenStep = step;
                var isCurrent = Mathf.Abs(step - LiveBuilderGrid.CurrentStep) < 0.0001f;
                menu.AddItem(new GUIContent(LiveBuilderSceneText.FormatMeters(step) + " m"), isCurrent, () =>
                {
                    LiveBuilderGrid.SetStep(chosenStep);
                    Refresh();
                    SceneView.RepaintAll();
                });
            }

            menu.DropDown(gridDropdown.worldBound);
        }


        // ── Обновление ───────────────────────────────────────────────────

        // Сменились инструмент, этаж, сетка или вкл/выкл — полоса показывает как есть, не зовя свои же отклики.
        public void Refresh()
        {
            var isActive = LiveBuilderActivation.IsToolActive;
            var key = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.ToggleLiveBuilder);
            powerToggle.SetValueWithoutNotify(isActive);
            powerToggle.tooltip = (isActive ? "Выключить Live Builder (" : "Включить Live Builder (") + key + ")";

            foreach (var (tool, toggle) in toolToggles)
            {
                toggle.SetValueWithoutNotify(isActive && LiveBuilderToolPalette.Current == tool);
                toggle.tooltip = tool.DisplayName + " (" + LiveBuilderToolPalette.HotkeyOf(tool) + ") — " + tool.Tooltip;
            }

            storeyLabel.text = LiveBuilderStoreys.CurrentStoreyNumber.ToString();

            if (gridDropdown != null)
                gridDropdown.text = LiveBuilderSceneText.FormatMeters(LiveBuilderGrid.CurrentStep);
        }
    }
}
