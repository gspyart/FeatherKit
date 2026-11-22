using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ярлыки точек контура выделенной стены или здания: C — замкнуть или разомкнуть стену, стрелки — выбранные точки на клетку.
    /// Что делают — <see cref="LiveBuilderContourKeys"/>; Delete у точек — команда Unity, ярлык ей не нужен.
    /// </summary>
    public static class LiveBuilderContourShortcuts
    {
        [Shortcut(LiveBuilderShortcutIds.ContourCloseOrOpen, typeof(LiveBuilderContourShortcutContext), KeyCode.C)]
        private static void CloseOrOpen()
        {
            LiveBuilderContourKeys.ToggleClosedOfShownContour();
            SceneView.RepaintAll();
        }


        [Shortcut(LiveBuilderShortcutIds.ContourNudgeLeft, typeof(LiveBuilderContourPointsShortcutContext), KeyCode.LeftArrow)]
        private static void NudgeLeft()
        {
            Nudge(KeyCode.LeftArrow);
        }


        [Shortcut(LiveBuilderShortcutIds.ContourNudgeRight, typeof(LiveBuilderContourPointsShortcutContext), KeyCode.RightArrow)]
        private static void NudgeRight()
        {
            Nudge(KeyCode.RightArrow);
        }


        [Shortcut(LiveBuilderShortcutIds.ContourNudgeForward, typeof(LiveBuilderContourPointsShortcutContext), KeyCode.UpArrow)]
        private static void NudgeForward()
        {
            Nudge(KeyCode.UpArrow);
        }


        [Shortcut(LiveBuilderShortcutIds.ContourNudgeBack, typeof(LiveBuilderContourPointsShortcutContext), KeyCode.DownArrow)]
        private static void NudgeBack()
        {
            Nudge(KeyCode.DownArrow);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void Nudge(KeyCode arrow)
        {
            LiveBuilderContourKeys.NudgeSelectedPoints(arrow);
            SceneView.RepaintAll();
        }
    }
}
