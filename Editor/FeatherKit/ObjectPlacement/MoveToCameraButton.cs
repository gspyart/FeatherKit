using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кнопка панели «Расстановка»: переносит выделенное туда, где стоит камера сцены.
    /// Поворот объектов не меняется.
    /// </summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public class MoveToCameraButton : EditorToolbarButton, IAccessContainerWindow
    {
        public const string ElementId = "FeatherKit/ObjectPlacement/MoveToCamera";

        public MoveToCameraButton()
        {
            icon = (Texture2D)EditorGUIUtility.IconContent("SceneViewCamera").image;
            tooltip = "Перенести к камере: объект встанет туда, где сейчас камера сцены. Поворот не меняется";

            clicked += () => SelectionPlacer.MoveToCamera(OwnSceneView, takeCameraRotation: false);
            this.EnableOnlyWithSelection();
        }

        // ── IAccessContainerWindow ───────────────────────

        public EditorWindow containerWindow { get; set; }

        // Окно сцены, в котором лежит панель. Unity его может не сообщить — тогда последнее активное.
        private SceneView OwnSceneView => containerWindow as SceneView ?? SceneView.lastActiveSceneView;
    }
}
