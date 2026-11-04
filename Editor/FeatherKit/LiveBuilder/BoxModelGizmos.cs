using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Каркасы коробок у выделенного объекта из коробок, пока инструмент «Live Builder» выключен: видно, из чего собран объект.</summary>
    public static class BoxModelGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawPartsOfSelectedModel(BoxModel model, GizmoType gizmoType)
        {
            if (LiveBuilderActivation.IsToolActive)
                return;

            BoxModelPartHandles.DrawWireframes(model);
        }


        // Выделена одна коробка — каркасы всего её объекта.
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawPartsAroundSelectedPart(BoxModelPart part, GizmoType gizmoType)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model != null && !LiveBuilderActivation.IsToolActive)
                BoxModelPartHandles.DrawWireframes(model);
        }
    }
}
