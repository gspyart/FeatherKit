using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кнопка-переключатель панели «Расстановка»: включает и выключает режим
    /// «поставить на поверхность». Нажата, пока режим включён.
    /// </summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public class PlaceOnSurfaceToggle : EditorToolbarToggle
    {
        public const string ElementId = "FeatherKit/ObjectPlacement/PlaceOnSurface";

        public PlaceOnSurfaceToggle()
        {
            icon = (Texture2D)EditorGUIUtility.IconContent("ToolHandlePivot").image;
            tooltip = "Поставить на поверхность: нажми и кликни в сцене, куда поставить. " +
                      "Объект встанет низом на поверхность и наклонится вместе с ней. Esc — отмена";

            this.EnableOnlyWithSelection();
            this.RegisterValueChangedCallback(SwitchMode);
            RegisterCallback<AttachToPanelEvent>(_ => StartFollowingMode());
            RegisterCallback<DetachFromPanelEvent>(_ => SurfacePlacementMode.ActiveChanged -= ShowModeState);
        }


        private static void SwitchMode(ChangeEvent<bool> change)
        {
            if (change.newValue)
                SurfacePlacementMode.Start();
            else
                SurfacePlacementMode.Stop();
        }


        // Режим выключается и сам — кликом в сцене или Esc, — поэтому кнопка следит за ним.
        private void StartFollowingMode()
        {
            SurfacePlacementMode.ActiveChanged += ShowModeState;
            ShowModeState();
        }

        private void ShowModeState() => SetValueWithoutNotify(SurfacePlacementMode.IsActive);
    }
}
