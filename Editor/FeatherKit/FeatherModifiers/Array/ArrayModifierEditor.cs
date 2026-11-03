using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инспектор модификатора Array (<see cref="ArrayModifier"/>): те же настройки, что в окне Feather Modifiers, и «Apply», «Remove».
    /// В окне сцены — ручки массива (<see cref="ArrayModifierHandles"/>); объект сдвинули — копии сразу за ним, не дожидаясь конца перетаскивания.
    /// </summary>
    [CustomEditor(typeof(ArrayModifier))]
    public class ArrayModifierEditor : Editor
    {
        // ── Инспектор ────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var array = (ArrayModifier)target;
            var description = FDescriptionHeader.DescriptionOf(target);

            if (!string.IsNullOrEmpty(description))
                EditorGUILayout.HelpBox(description, MessageType.None);

            ArrayModifierSettingsGUI.Draw(array);
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Open Feather Modifiers", "The window with every modifier of the selected object")))
                    FeatherModifiersWindow.Open();

                if (GUILayout.Button(new GUIContent("Apply", "Keep the copies as ordinary objects and remove the modifier. Ctrl+Z brings it back")))
                {
                    ArrayModifierBuilder.Apply(array);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Remove", "Remove the copies and the modifier: the object is back as it was")))
                {
                    ArrayModifierBuilder.Remove(array);
                    GUIUtility.ExitGUI();
                }
            }
        }


        // ── Окно сцены ───────────────────────────────────────────────────

        private void OnSceneGUI()
        {
            var array = (ArrayModifier)target;

            if (array == null)
                return;

            if (ArrayModifierHandles.Draw(array) || Event.current.type == EventType.Repaint && !ArrayModifierBuilder.IsUpToDate(array))
                ArrayModifierBuilder.Rebuild(array);
        }


        // ── Unity-события ────────────────────────────────────────────────

        // Открыли объект, а массив отстал (поменялся префаб, удалили копию) — сразу на место.
        private void OnEnable()
        {
            if (target is ArrayModifier array && !ArrayModifierBuilder.IsUpToDate(array))
                ArrayModifierBuilder.Rebuild(array);
        }
    }
}
