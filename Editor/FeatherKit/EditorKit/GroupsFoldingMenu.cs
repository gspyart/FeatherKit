using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Пункты «Свернуть все группы» и «Развернуть все группы» в меню ⋮ компонента и ассета
    /// с разметкой FeatherKit (<see cref="GroupedFieldsLayout"/>).
    ///
    /// Свои, потому что «Collapse All Components» у Unity сворачивает только заголовки компонентов,
    /// а поля внутри не трогает — у ассета настроек он не делает ничего.
    ///
    /// Сворачивает всё, что сворачивается: рамки [FGroupBox] на всех уровнях, блоки [FBlockTitle]
    /// и раскрывающиеся поля-классы. Секции и плоские рамки внутри блоков не сворачиваются вовсе.
    /// Списки не трогает: развернуть их разом — значит утонуть в элементах.
    /// </summary>
    internal static class GroupsFoldingMenu
    {
        private const string CollapseTitle = "Свернуть все группы";
        private const string ExpandTitle = "Развернуть все группы";


        // ── Пункты меню ──────────────────────────────────────────────────

        [MenuItem("CONTEXT/MonoBehaviour/" + CollapseTitle)]
        [MenuItem("CONTEXT/ScriptableObject/" + CollapseTitle)]
        private static void CollapseAll(MenuCommand command)
        {
            SetAllExpanded(command.context, false);
        }


        [MenuItem("CONTEXT/MonoBehaviour/" + ExpandTitle)]
        [MenuItem("CONTEXT/ScriptableObject/" + ExpandTitle)]
        private static void ExpandAll(MenuCommand command)
        {
            SetAllExpanded(command.context, true);
        }


        // Пункты видны только у объектов с разметкой FeatherKit: у остальных им нечего сворачивать.
        [MenuItem("CONTEXT/MonoBehaviour/" + CollapseTitle, true)]
        [MenuItem("CONTEXT/ScriptableObject/" + CollapseTitle, true)]
        [MenuItem("CONTEXT/MonoBehaviour/" + ExpandTitle, true)]
        [MenuItem("CONTEXT/ScriptableObject/" + ExpandTitle, true)]
        private static bool HasGroups(MenuCommand command)
        {
            return command.context != null && GroupedFieldsLayout.IsUsedBy(command.context.GetType());
        }


        // ── Сворачивание ─────────────────────────────────────────────────

        private static void SetAllExpanded(Object target, bool expanded)
        {
            if (target == null)
                return;

            var serializedObject = SerializedObjectOf(target);

            SetFieldsExpanded(serializedObject, expanded);
            GroupedFieldsLayout.SetGroupBoxesExpanded(serializedObject, expanded);
            InternalEditorUtility.RepaintAllViews();
        }

        // Поля-классы — внутрь заходим, списки — нет: их элементы остаются как были.
        private static void SetFieldsExpanded(SerializedObject serializedObject, bool expanded)
        {
            var iterator = serializedObject.GetIterator();

            for (var enterChildren = true; iterator.NextVisible(enterChildren);)
            {
                var canFold = iterator.propertyType == SerializedPropertyType.Generic && !iterator.isArray && iterator.hasVisibleChildren;

                if (canFold)
                    iterator.isExpanded = expanded;

                enterChildren = canFold;
            }
        }

        // Тот же SerializedObject, что у открытого инспектора: так свёрнутое видно сразу.
        private static SerializedObject SerializedObjectOf(Object target)
        {
            foreach (var editor in ActiveEditorTracker.sharedTracker.activeEditors)
            {
                if (editor != null && editor.target == target)
                    return editor.serializedObject;
            }

            return new SerializedObject(target);
        }
    }
}
