using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Покраска построек Live Builder из палитры уровня: клик по материалу кладёт его в данные выделенного (<see cref="LiveBuilderMaterialSlots"/>) —
    /// выделенные грани коробки, коробку, комнату, лестницу, вырез, стену, здание, — а не в меш, который пересоберётся.
    /// Записывается в общий список покрасок FeatherKit при загрузке редактора.
    /// </summary>
    [InitializeOnLoad]
    public sealed class LiveBuilderMaterialPainter : IObjectMaterialPainter
    {
        static LiveBuilderMaterialPainter()
        {
            ObjectMaterialPainters.Register(new LiveBuilderMaterialPainter());
        }


        // ── IObjectMaterialPainter ───────────────────────────────────────

        public string DescribeTarget(GameObject gameObject)
        {
            var slots = LiveBuilderMaterialSlots.ForSelected(gameObject);

            if (slots.Count == 0)
                return null;

            return slots.Count == 1 ? slots[0].Description : "граней коробки: " + slots.Count;
        }


        public bool TryPaint(GameObject gameObject, Material material)
        {
            var slots = LiveBuilderMaterialSlots.ForSelected(gameObject);

            if (slots.Count == 0)
                return false;

            foreach (var slot in slots)
                if (!slot.TryAssign(material) && slot.Problem != null && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.ShowNotification(new GUIContent(slot.Problem), 2f);

            return true;
        }
    }
}
