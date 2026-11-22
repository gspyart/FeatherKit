using System;
using System.Collections.Generic;
using UnityEditor.ShortcutManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Имена ярлыков Live Builder в окне Edit → Shortcuts (категория «Live Builder») — одно место на все.
    /// - имена ярлыков инструмента — по его Id: выбор (ещё раз — следующий режим) и с Shift (предыдущий режим)
    /// - подпись клавиши — из текущей привязки: переназначил в Shortcuts — подписи следом
    /// </summary>
    public static class LiveBuilderShortcutIds
    {
        public const string Category = "Live Builder/";
        public const string ToggleLiveBuilder = Category + "Toggle Live Builder";
        public const string ToolsPrefix = Category + "Tools/";
        public const string PreviousModeSuffix = " (Previous Mode)";
        public const string GridStepDown = Category + "Grid Step Down";
        public const string GridStepUp = Category + "Grid Step Up";
        public const string StoreyDown = Category + "Storey Down";
        public const string StoreyUp = Category + "Storey Up";
        public const string Rotate = Category + "Rotate";
        public const string TogglePointSnap = Category + "Toggle Point Snap";
        public const string ToggleKeyHints = Category + "Toggle Key Hints";
        public const string DigitPrefix = Category + "Number Input/Digit ";
        public const string Times = Category + "Number Input/Times";
        public const string ContourCloseOrOpen = Category + "Contour/Close Or Open";
        public const string ContourNudgeLeft = Category + "Contour/Nudge Left";
        public const string ContourNudgeRight = Category + "Contour/Nudge Right";
        public const string ContourNudgeForward = Category + "Contour/Nudge Forward";
        public const string ContourNudgeBack = Category + "Contour/Nudge Back";

        private static readonly string[] CommonIds =
        {
            ToggleLiveBuilder, GridStepDown, GridStepUp, StoreyDown, StoreyUp, Rotate, TogglePointSnap, ToggleKeyHints, Times,
            ContourCloseOrOpen, ContourNudgeLeft, ContourNudgeRight, ContourNudgeForward, ContourNudgeBack
        };


        // ── Имена ────────────────────────────────────────────────────────

        // Все ярлыки Live Builder: общие, цифры набора и по два на инструмент палитры.
        public static List<string> AllIds()
        {
            var ids = new List<string>(CommonIds);

            for (var digit = 0; digit <= 9; digit++)
                ids.Add(DigitOf(digit));

            foreach (var tool in LiveBuilderToolPalette.Tools)
            {
                ids.Add(ToolOf(tool));
                ids.Add(PreviousModeOf(tool));
            }

            return ids;
        }


        public static string ToolOf(LiveBuilderTool tool)
        {
            return ToolsPrefix + tool.Id;
        }


        // Shift + клавиша инструмента: выбрать его, уже выбран — предыдущий режим.
        public static string PreviousModeOf(LiveBuilderTool tool)
        {
            return ToolOf(tool) + PreviousModeSuffix;
        }


        public static string DigitOf(int digit)
        {
            return DigitPrefix + digit;
        }


        // ── Подпись клавиши ──────────────────────────────────────────────

        // Как в окне Shortcuts: «2», «⇧2». Нет привязки — пусто. Ярлык не найден (скрипты ещё компилируются) — тоже пусто, окно не ломаем.
        public static string KeyLabelOf(string shortcutId)
        {
            try
            {
                return ShortcutManager.instance.GetShortcutBinding(shortcutId).ToString();
            }
            catch (ArgumentException)
            {
                return "";
            }
        }
    }
}
