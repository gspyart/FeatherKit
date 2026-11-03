using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Материал, перетащенный из Project или палитры уровня на постройку Live Builder в окне сцены, ложится в её данные
    /// (<see cref="LiveBuilderMaterialSlots"/>), а не в меш: меш собирается из них заново, и покраска меша слетела бы.
    /// На грань коробки — в эту её сторону, с Alt — во всю коробку; на стену — в стену; на здание — в то, что под мышью:
    /// наружные стены, стены изнутри, пол, потолок или крышу. Работает и без включённого Live Builder; одним шагом Ctrl+Z.
    /// Над чем-то, что не Live Builder, — как обычно в Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderMaterialDrop
    {
        static LiveBuilderMaterialDrop()
        {
            DragAndDrop.AddDropHandlerV2(OnSceneDrop);
        }


        // ── Перетаскивание ───────────────────────────────────────────────

        // Пока тянешь — курсор «сюда можно» и подпись, что покрасится; отпустил — материал в данные постройки.
        private static DragAndDropVisualMode OnSceneDrop(Object dropUpon, Vector3 worldPosition, Vector2 viewportPosition, Transform parentForDraggedObjects,
            bool perform)
        {
            var material = DraggedMaterial();

            if (material == null || Event.current == null)
                return DragAndDropVisualMode.None;

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            if (!LiveBuilderMaterialSlots.TryFindUnder(ray, Event.current.alt, out var slot))
                return DragAndDropVisualMode.None;

            if (!perform)
            {
                ShowNotification(slot.Problem ?? slot.Description + (Event.current.alt ? "" : " · Alt — вся коробка"));
                return slot.Problem == null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            }

            if (!slot.TryAssign(material))
                ShowNotification(slot.Problem);

            return DragAndDropVisualMode.Link;
        }

        private static Material DraggedMaterial()
        {
            var dragged = DragAndDrop.objectReferences;
            return dragged.Length == 1 ? dragged[0] as Material : null;
        }

        private static void ShowNotification(string text)
        {
            if (!string.IsNullOrEmpty(text) && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent(text), 1f);
        }
    }
}
