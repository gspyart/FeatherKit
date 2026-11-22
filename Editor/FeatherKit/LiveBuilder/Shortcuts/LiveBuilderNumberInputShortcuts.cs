using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderShortcutContext;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ярлыки набора числа, пока инструмент рисует: цифры и X — в «4 × 3 × 1,5». Работают только в контексте «рисую»,
    /// поэтому те же цифры без рисования — выбор инструмента, а X — ярлык Unity.
    /// Запятая, цифровой блок и Backspace ярлыков не имеют — их отдаёт окно сцены (<see cref="LiveBuilderSceneKeyboard.HandleUnboundKey"/>).
    /// </summary>
    public static class LiveBuilderNumberInputShortcuts
    {
        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "0", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha0)]
        private static void TypeDigit0(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha0);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "1", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha1)]
        private static void TypeDigit1(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha1);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "2", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha2)]
        private static void TypeDigit2(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha2);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "3", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha3)]
        private static void TypeDigit3(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha3);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "4", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha4)]
        private static void TypeDigit4(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha4);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "5", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha5)]
        private static void TypeDigit5(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha5);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "6", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha6)]
        private static void TypeDigit6(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha6);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "7", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha7)]
        private static void TypeDigit7(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha7);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "8", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha8)]
        private static void TypeDigit8(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha8);
        }


        [Shortcut(LiveBuilderShortcutIds.DigitPrefix + "9", typeof(LiveBuilderDrawingShortcutContext), KeyCode.Alpha9)]
        private static void TypeDigit9(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.Alpha9);
        }


        [Shortcut(LiveBuilderShortcutIds.Times, typeof(LiveBuilderDrawingShortcutContext), KeyCode.X)]
        private static void TypeTimes(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).SendKeyToTool(KeyCode.X);
        }
    }
}
