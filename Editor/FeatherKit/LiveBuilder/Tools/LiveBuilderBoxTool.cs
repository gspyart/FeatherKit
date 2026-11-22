using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Коробка»: рисует протягиванием по поверхности — в четырёх режимах (<see cref="LiveBuilderBoxMode"/>), у каждого своя часть с жестом:
    /// Solid — коробка, Room — пустая комната на высоту этажа, Ramp — пандус, Column — колонна.
    /// Правит коробки, их грани, вырез-коробки, комнаты и колонны — в любом режиме.
    /// Здесь же — «новый объект»: следующая коробка отдельным объектом, даже поверх другого; на полу здания — вложенным объектом в нём.
    /// </summary>
    public sealed class LiveBuilderBoxTool : LiveBuilderTool
    {
        // По части на режим, в порядке LiveBuilderBoxMode.
        private readonly LiveBuilderBoxToolMode[] modeParts =
        {
            new LiveBuilderSolidBoxMode(),
            new LiveBuilderRoomBoxMode(),
            new LiveBuilderRampBoxMode(),
            new LiveBuilderColumnBoxMode()
        };

        private LiveBuilderToolModes modes;


        // Кнопка «Новый»: следующая нарисованная коробка — отдельный новый объект, даже если рисуешь на другом объекте из коробок.
        public static bool IsNewModelRequested { get; set; }


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Box";

        public override string DisplayName => "Box";

        public override string Tooltip => "Протяни по поверхности, мышью глубина, клик — готово. Режимы: Solid — коробка, Room — пустая комната, " +
                                          "Ramp — пандус, Column — колонна; клавиша ещё раз — следующий режим. Клик по грани — выделить коробку";

        public override int PaletteOrder => 1;

        // Кубик в изометрии.
        public override List<Vector2[]> IconLines => new()
        {
            Points(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f, 12, 3),
            Points(4, 7.5f, 12, 12, 20, 7.5f),
            Points(12, 12, 12, 21)
        };

        public override LiveBuilderToolModes Modes => modes ??= new LiveBuilderToolModes(() => (int)LiveBuilderBoxModeSettings.Mode, SwitchMode,
            new LiveBuilderToolModeLabel("Solid", LiveBuilderOptionIcon.Boxes, "Коробка: наружу добавляет, внутрь вырезает (Auto · Add · Cut)"),
            new LiveBuilderToolModeLabel("Room", LiveBuilderOptionIcon.Room, "Пустая комната на высоту этажа: стены, пол и потолок вокруг. По контуру — «Wall» или «Building»"),
            new LiveBuilderToolModeLabel("Ramp", LiveBuilderOptionIcon.Ramp, "Пандус: скат во всю длину, поднимается к концу протягивания, Tab — повернуть"),
            new LiveBuilderToolModeLabel("Column", LiveBuilderOptionIcon.Column, "Колонна-цилиндр в протянутом прямоугольнике, отдельный объект: вырезы её не режут"));

        private LiveBuilderBoxToolMode CurrentPart => modeParts[(int)LiveBuilderBoxModeSettings.Mode];


        // ── Что правит ───────────────────────────────────────────────────

        public override bool CanEditFaces => true;

        public override bool CanEditRepeats => true;

        public override bool PlacesIntoBuilding => true;


        // ── Куда встанет новое ───────────────────────────────────────────

        public override string IntoTarget => LiveBuilderIntoTarget.OfBox(IsNewModelRequested);


        // ── Окно сцены ───────────────────────────────────────────────────

        // «Новый» — не начатое рисование: цифры по-прежнему меняют инструмент, а смена инструмента «Новый» снимает.
        public override bool IsBusy => CurrentPart.IsBusy;

        public override LiveBuilderDrawingReport DrawingReport => CurrentPart.DrawingReport;

        public override bool DrawsSurfaceHover => CurrentPart.DrawsSurfaceHover;

        public override bool DrawsSnapCursor => true;

        public override string Hint => CurrentPart.Hint;

        public override bool ShowsDepthClickHint => CurrentPart.ShowsDepthClickHint;

        public override string[] KeyHints => CurrentPart.KeyHints;


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => CurrentPart.MoreSettingsSummary;

        public override bool AreMoreSettingsChanged => CurrentPart.AreMoreSettingsChanged;


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Box", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha2)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderBoxTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Box" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha2, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderBoxTool>();
        }


        // ── Режим ────────────────────────────────────────────────────────

        // Начатое в прежнем режиме бросаем: оно рисовалось другим жестом.
        private void SwitchMode(int index)
        {
            CurrentPart.Cancel();
            LiveBuilderBoxModeSettings.Mode = (LiveBuilderBoxMode)index;
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Коробки и вырез-коробки своего объекта, комнаты из коробок, колонны.
        protected override bool CanEditTarget(Component target)
        {
            return target is BoxModelPart || target is BoxModelRoom || target is LiveBuilderColumn
                || target is GeometryCutter cutter && GeometryCutterSolverBoxes.IsSolverPiece(cutter);
        }


        // ── Окно ─────────────────────────────────────────────────────────

        public override void DrawSettings()
        {
            CurrentPart.DrawSettings();
        }


        public override void DrawMoreSettings()
        {
            CurrentPart.DrawMoreSettings();
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            return CurrentPart.OnMouseMove(worldRay, current);
        }


        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            return CurrentPart.OnMouseDown(worldRay, current);
        }


        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            CurrentPart.OnMouseDrag(downRay, worldRay, canBeginGesture);
        }


        public override bool OnMouseUp(Event current)
        {
            return CurrentPart.OnMouseUp(current);
        }


        // ── Окно сцены: клавиши, отмена и вид ────────────────────────────

        // Клавиши — режиму. Esc при нажатом «Новый», пока ничего не рисуешь, — снять «Новый» и остаться в «Коробке».
        public override bool OnKeyDown(Event current)
        {
            if (CurrentPart.OnKeyDown(current))
                return true;

            if (current.keyCode != KeyCode.Escape || !IsNewModelRequested || IsBusy)
                return false;

            IsNewModelRequested = false;
            SceneView.RepaintAll();
            return true;
        }


        // Бросить начатое во всех режимах и «Новый».
        public override void Cancel()
        {
            CancelAfterUndo();
            IsNewModelRequested = false;
        }


        // Ctrl+Z: начатое бросаем, «Новый» остаётся.
        public override void CancelAfterUndo()
        {
            foreach (var part in modeParts)
                part.Cancel();
        }


        public override void DrawPreview()
        {
            CurrentPart.DrawPreview();
        }
    }
}
