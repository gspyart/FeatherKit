using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderShortcutContext;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общие ярлыки Live Builder в окне Edit → Shortcuts: вкл/выкл, сетка, этаж, поворот, прилипание, шпаргалка.
    /// Здесь только клавиша и контекст; что она делает — <see cref="LiveBuilderSceneKeyboard"/>.
    /// </summary>
    public static class LiveBuilderShortcuts
    {
        // Окно сцены Unity, а не свой контекст: свои контексты живут, только пока Live Builder включён.
        [Shortcut(LiveBuilderShortcutIds.ToggleLiveBuilder, typeof(SceneView), KeyCode.B)]
        private static void ToggleLiveBuilder()
        {
            LiveBuilderActivation.ToggleTool();
        }


        [Shortcut(LiveBuilderShortcutIds.GridStepDown, typeof(LiveBuilderSceneShortcutContext), KeyCode.LeftBracket)]
        private static void GridStepDown(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).ChangeGridStep(false);
        }


        [Shortcut(LiveBuilderShortcutIds.GridStepUp, typeof(LiveBuilderSceneShortcutContext), KeyCode.RightBracket)]
        private static void GridStepUp(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).ChangeGridStep(true);
        }


        [Shortcut(LiveBuilderShortcutIds.StoreyDown, typeof(LiveBuilderSceneShortcutContext), KeyCode.PageDown)]
        private static void StoreyDown(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).ChangeStorey(-1);
        }


        [Shortcut(LiveBuilderShortcutIds.StoreyUp, typeof(LiveBuilderSceneShortcutContext), KeyCode.PageUp)]
        private static void StoreyUp(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).ChangeStorey(1);
        }


        [Shortcut(LiveBuilderShortcutIds.Rotate, typeof(LiveBuilderSceneShortcutContext), KeyCode.Tab)]
        private static void Rotate(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).Rotate();
        }


        [Shortcut(LiveBuilderShortcutIds.TogglePointSnap, typeof(LiveBuilderSceneShortcutContext), KeyCode.S)]
        private static void TogglePointSnap(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).TogglePointSnap();
        }


        [Shortcut(LiveBuilderShortcutIds.ToggleKeyHints, typeof(LiveBuilderSceneShortcutContext), KeyCode.Slash)]
        private static void ToggleKeyHints(ShortcutArguments arguments)
        {
            KeyboardOf(arguments).ToggleKeyHints();
        }
    }
}
