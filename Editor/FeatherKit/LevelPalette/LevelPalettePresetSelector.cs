using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Выпадающий список пресетов в начале строки вкладок — часть <see cref="LevelPaletteTabsBar"/>.
    /// - переключить пресет
    /// - новый пустой или копия открытого, переименовать, удалить
    /// </summary>
    public static class LevelPalettePresetSelector
    {
        private const float MaxWidth = 140f;
        private const string NewPresetName = "Пресет";


        public static float Width => Mathf.Min(MaxWidth, EditorStyles.toolbarDropDown.CalcSize(Label).x);

        private static LevelPaletteStorage Storage => LevelPaletteStorage.instance;

        private static GUIContent Label => new GUIContent(Storage.ActivePreset.Name, "Пресет палитры — свой набор вкладок");


        // ── Отрисовка ────────────────────────────────────────────────────

        /// <param name="presetSelected">Пользователь сам открыл другой пресет.</param>
        public static void Draw(Action presetSelected)
        {
            var rect = GUILayoutUtility.GetRect(Label, EditorStyles.toolbarDropDown, GUILayout.Width(Width));

            if (EditorGUI.DropdownButton(rect, Label, FocusType.Passive, EditorStyles.toolbarDropDown))
                ShowMenu(rect, presetSelected);
        }


        // ── Меню ─────────────────────────────────────────────────────────

        private static void ShowMenu(Rect buttonRect, Action presetSelected)
        {
            var menu = new GenericMenu();
            var active = Storage.ActivePreset;

            foreach (var preset in Storage.Presets)
            {
                menu.AddItem(new GUIContent(preset.Name), preset == active, () =>
                {
                    Storage.SelectPreset(preset);
                    presetSelected();
                });
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Новый пресет/Пустой"), false, () =>
                AddAndRename(new LevelPalettePreset(FreeName(NewPresetName), Array.Empty<LevelPaletteTab>()), buttonRect, presetSelected));
            menu.AddItem(new GUIContent($"Новый пресет/Копия «{active.Name}»"), false, () =>
                AddAndRename(active.Duplicate(FreeName(active.Name + " копия")), buttonRect, presetSelected));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent($"Переименовать «{active.Name}»"), false, () => ShowRenamePopup(active, buttonRect));

            if (Storage.Presets.Count > 1)
                menu.AddItem(new GUIContent($"Удалить «{active.Name}»"), false, () => RemoveAfterConfirm(active, presetSelected));
            else
                menu.AddDisabledItem(new GUIContent("Удалить — это единственный пресет"));

            menu.DropDown(buttonRect);
        }

        // Имя новому пресету всё равно придумывать — поле открывается сразу.
        private static void AddAndRename(LevelPalettePreset preset, Rect buttonRect, Action presetSelected)
        {
            Storage.AddPreset(preset);
            presetSelected();
            ShowRenamePopup(preset, buttonRect);
        }

        private static void ShowRenamePopup(LevelPalettePreset preset, Rect buttonRect)
        {
            PopupWindow.Show(buttonRect, new PaletteNameEditPopup(preset.Name, name => Storage.RenamePreset(preset, name)));
        }

        private static void RemoveAfterConfirm(LevelPalettePreset preset, Action presetSelected)
        {
            if (!EditorUtility.DisplayDialog("Палитра", $"Удалить пресет «{preset.Name}» со всеми его вкладками? Ассеты в проекте не пострадают.",
                    "Удалить", "Отмена"))
                return;

            Storage.RemovePreset(preset);
            presetSelected();
        }

        // «Пресет», занято — «Пресет 2», «Пресет 3»…
        private static string FreeName(string baseName)
        {
            var name = baseName;

            for (var number = 2; Storage.Presets.Any(preset => preset.Name == name); number++)
                name = $"{baseName} {number}";

            return name;
        }
    }
}
