using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно Feather Modifiers — модификаторы выделенного объекта, как стек модификаторов в Blender:
    /// - сверху — какой объект выделен и кнопки модификаторов (<see cref="FeatherModifierKind"/>): нажал — модификатор встал на этот объект
    /// - ниже — карточка каждого модификатора, что уже стоит: настройки, «Apply» — оставить результат, «Remove» — убрать всё
    /// Модификаторы — компоненты на объекте: тот же результат и через инспектор.
    /// </summary>
    public class FeatherModifiersWindow : EditorWindow
    {
        private const float CardGap = 6f;

        private Vector2 scroll;


        [MenuItem("FeatherKit/Feather Modifiers")]
        public static void Open()
        {
            var window = GetWindow<FeatherModifiersWindow>();
            window.titleContent = new GUIContent("Feather Modifiers");
            window.Show();
        }


        // ── Окно ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            var target = Selection.activeGameObject;

            if (target == null || EditorUtility.IsPersistent(target))
            {
                EditorGUILayout.HelpBox("Select an object in the scene, then press a modifier button below to add it.", MessageType.Info);
                DrawAddButtons(null);
                return;
            }

            EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
            DrawAddButtons(target);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var kind in FeatherModifierKind.All)
            {
                var modifier = kind.FindOn(target);

                if (modifier != null)
                    DrawCard(kind, modifier);
            }

            EditorGUILayout.EndScrollView();
        }

        // Кнопка на каждый вид: нажал — модификатор на выделенном объекте. Уже стоит или ничего не выделено — серая.
        private static void DrawAddButtons(GameObject target)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var kind in FeatherModifierKind.All)
                {
                    using (new EditorGUI.DisabledScope(target == null || !kind.CanAddTo(target)))
                        if (GUILayout.Button(new GUIContent(kind.Title, kind.Tooltip), GUILayout.Height(24f)))
                            kind.AddTo(target);
                }

                GUILayout.FlexibleSpace();
            }
        }

        // Карточка модификатора: название, «Apply» и «Remove», под ними — настройки.
        private static void DrawCard(FeatherModifierKind kind, Component modifier)
        {
            GUILayout.Space(CardGap);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(kind.Title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("Apply", "Keep the result as ordinary objects and remove the modifier. Ctrl+Z brings it back"),
                            GUILayout.Width(60f)))
                    {
                        kind.Apply(modifier);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(new GUIContent("Remove", "Remove the modifier and everything it made: the object is back as it was"),
                            GUILayout.Width(64f)))
                    {
                        kind.Remove(modifier);
                        GUIUtility.ExitGUI();
                    }
                }

                kind.DrawSettings(modifier);
            }
        }


        // ── Unity-события ────────────────────────────────────────────────

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnHierarchyChange()
        {
            Repaint();
        }
    }
}
