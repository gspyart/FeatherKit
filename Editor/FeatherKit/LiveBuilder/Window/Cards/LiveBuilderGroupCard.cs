using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Group — у объекта из коробок и у любого объекта сцены не из Live Builder: «New Group Inside» и «Group Selected».
    /// Группа — граница выреза: вырезы снаружи её не трогают. Сами команды — <see cref="LiveBuilderGroups"/>.
    /// </summary>
    public sealed class LiveBuilderGroupCard : LiveBuilderSelectionCard
    {
        private const string IsolationTooltip = "Группа — граница: вырезы снаружи её не режут, а её — режут только внутри.";


        public override int Order => GroupOrder;

        public override string Title => "Group";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return selected[0].scene.IsValid()
                   && AreAllOfKind(selected, LiveBuilderSelectionKind.Other, LiveBuilderSelectionKind.BoxModel, LiveBuilderSelectionKind.Group);
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            if (selected[0].GetComponent<BoxModel>() != null)
                LiveBuilderWindowControls.Note("A group: cuts outside it do not touch it.");

            var canGroup = LiveBuilderGroups.CanGroup(Selection.gameObjects, out var reason);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("New Group Inside", "Пустая группа внутри выделенного; в здании — на этаже. " + IsolationTooltip)))
                    LiveBuilderGroups.CreateGroupInside(selected[0]);

                using (new EditorGUI.DisabledScope(!canGroup))
                    if (GUILayout.Button(new GUIContent("Group Selected", canGroup ? "Выделенное — в новую группу на его месте. " + IsolationTooltip : "Нельзя: " + reason)))
                        LiveBuilderGroups.GroupObjects(Selection.gameObjects);
            }
        }
    }
}
