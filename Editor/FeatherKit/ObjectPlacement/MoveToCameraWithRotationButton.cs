using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кнопка панели «Расстановка»: переносит выделенное туда, где стоит камера сцены,
    /// и поворачивает так же, как камера.
    /// </summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public class MoveToCameraWithRotationButton : EditorToolbarButton, IAccessContainerWindow
    {
        public const string ElementId = "FeatherKit/ObjectPlacement/MoveToCameraWithRotation";

        public MoveToCameraWithRotationButton()
        {
            icon = (Texture2D)EditorGUIUtility.IconContent("RotateTool").image;
            tooltip = "Перенести к камере и повернуть как камеру: объект встанет туда, где сейчас камера сцены, " +
                      "и будет смотреть туда же";

            clicked += () => SelectionPlacer.MoveToCamera(OwnSceneView, takeCameraRotation: true);
            this.EnableOnlyWithSelection();
        }

        // ── IAccessContainerWindow ───────────────────────

        public EditorWindow containerWindow { get; set; }

        // Окно сцены, в котором лежит панель. Unity его может не сообщить — тогда последнее активное.
        private SceneView OwnSceneView => containerWindow as SceneView ?? SceneView.lastActiveSceneView;
    }
}
