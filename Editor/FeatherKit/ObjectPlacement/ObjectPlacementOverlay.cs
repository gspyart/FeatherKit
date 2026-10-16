using UnityEditor;
using UnityEditor.Overlays;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Панель «Расстановка» в окне сцены: три кнопки для выделенных объектов —
    /// поставить на поверхность, перенести к камере, перенести к камере с её поворотом.
    /// Двигается, прячется и пристыковывается к краям так же, как штатные панели инструментов.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Object Placement", true)]
    public class ObjectPlacementOverlay : ToolbarOverlay
    {
        private const string OverlayId = "featherkit-object-placement";

        public ObjectPlacementOverlay() : base(
            PlaceOnSurfaceToggle.ElementId,
            MoveToCameraButton.ElementId,
            MoveToCameraWithRotationButton.ElementId)
        {
        }
    }
}
