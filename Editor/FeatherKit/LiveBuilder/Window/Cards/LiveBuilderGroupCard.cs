using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Группа» вкладки «Выделено» — у любого объекта сцены: «Новая группа внутри» и «В группу» для выделенного.
    /// Выделена сама группа (объект из коробок) — строка о том, что вырезы снаружи её не трогают. Сами команды — <see cref="LiveBuilderGroups"/>.
    /// </summary>
    public sealed class LiveBuilderGroupCard : LiveBuilderSelectionCard
    {
        private const string IsolationTooltip = "Группа — граница: вырезы снаружи её не режут, её вырезы режут только внутри.";


        public override int Order => 85;


        public override bool Draw(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid())
                return false;

            using (LiveBuilderWindowControls.Card("Группа"))
            {
                if (selected.GetComponent<BoxModel>() != null)
                    LiveBuilderWindowControls.Note("Группа: вырезы снаружи её не трогают.");

                var canGroup = LiveBuilderGroups.CanGroup(Selection.gameObjects, out var reason);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Новая группа внутри", "Пустая группа внутри выделенного; в здании — на этаже окна. " + IsolationTooltip)))
                        LiveBuilderGroups.CreateGroupInside(selected);

                    using (new EditorGUI.DisabledScope(!canGroup))
                        if (GUILayout.Button(new GUIContent("В группу", canGroup ? "Выделенное — в новую группу на его месте. " + IsolationTooltip : "Нельзя: " + reason)))
                            LiveBuilderGroups.GroupObjects(Selection.gameObjects);
                }
            }

            return true;
        }
    }
}
