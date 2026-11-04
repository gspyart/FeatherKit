using UnityEditor.EditorTools;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Включён ли инструмент «Live Builder» в окне сцены (<see cref="LiveBuilderSceneTool"/>), и включить или выключить его.
    /// Одно место на все кнопки и проверки: окно, плашка в окне сцены, выделение кликом, каркасы.
    /// </summary>
    public static class LiveBuilderActivation
    {
        public static bool IsToolActive => ToolManager.activeToolType == typeof(LiveBuilderSceneTool);


        // Выключить — вернуть инструмент, который был до него.
        public static void SetToolActive(bool isActive)
        {
            if (isActive)
                ToolManager.SetActiveTool<LiveBuilderSceneTool>();
            else if (IsToolActive)
                ToolManager.RestorePreviousPersistentTool();
        }
    }
}
