using UnityEditor;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кнопки панели «Расстановка» работают с выделенным, поэтому без выделения они серые.
    /// </summary>
    public static class SelectionDependentButtonExtensions
    {
        public static void EnableOnlyWithSelection(this VisualElement button)
        {
            void ShowAvailability() => button.SetEnabled(SelectionPlacer.HasTargets);

            button.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Selection.selectionChanged += ShowAvailability;
                ShowAvailability();
            });
            button.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= ShowAvailability);
        }
    }
}
