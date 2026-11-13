using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поля ноды как элементы UI — одни и те же для ноды на холсте и инспектора ноды.
    /// - обычное поле — PropertyField с подписью из ScenarioLabel; [ScenarioInspectorOnly] — только в инспекторе
    /// - стопки событий, условий и шагов — ScenarioBlockStackView, исходы Wait For — ScenarioWaitCaseListView
    /// </summary>
    internal static class ScenarioNodeFields
    {
        // Поля базы ноды: id и место правит холст, подпись — инспектор ноды отдельной строкой.
        private static readonly HashSet<string> BaseNodeFieldNames = new HashSet<string> { "id", "position", "customTitle" };


        public static void AddTo(VisualElement container, ScenarioGraphView graphView, ScenarioNode node,
            SerializedProperty nodeProperty, bool isInspector)
        {
            var nodeType = node.GetType();

            foreach (var field in VisibleChildrenOf(nodeProperty))
            {
                if (BaseNodeFieldNames.Contains(field.name))
                    continue;

                var fieldInfo = ScenarioBlockStyle.FindField(nodeType, field.name);

                if (!isInspector && fieldInfo?.GetCustomAttribute<ScenarioInspectorOnlyAttribute>() != null)
                    continue;

                container.Add(CreateFieldView(graphView, node, field, fieldInfo));
            }
        }


        private static VisualElement CreateFieldView(ScenarioGraphView graphView, ScenarioNode node,
            SerializedProperty field, FieldInfo fieldInfo)
        {
            var label = ScenarioBlockStyle.LabelOf(node.GetType(), field);
            var fieldType = fieldInfo?.FieldType;

            if (fieldType == typeof(ScenarioActionList))
                return new ScenarioBlockStackView(field.FindPropertyRelative("actions"), typeof(ScenarioAction), label.text, "No steps");

            if (fieldType == typeof(ScenarioConditionList))
                return new ScenarioBlockStackView(field.FindPropertyRelative("conditions"), typeof(ScenarioCondition), label.text,
                    "No conditions — always met", field.FindPropertyRelative("mode"));

            if (fieldType == typeof(List<ScenarioEvent>))
                return new ScenarioBlockStackView(field, typeof(ScenarioEvent), label.text, "No events — runs only when called");

            if (fieldType == typeof(List<ScenarioWaitCase>) && node is ScenarioWaitForNode waitForNode)
                return new ScenarioWaitCaseListView(graphView, waitForNode, field);

            var propertyField = new PropertyField(field, label.text) { tooltip = label.tooltip };
            propertyField.BindProperty(field);

            return propertyField;
        }


        private static IEnumerable<SerializedProperty> VisibleChildrenOf(SerializedProperty parent)
        {
            var child = parent.Copy();
            var end = parent.GetEndProperty();

            for (var hasChild = child.NextVisible(true);
                 hasChild && !SerializedProperty.EqualContents(child, end);
                 hasChild = child.NextVisible(false))
            {
                yield return child.Copy();
            }
        }
    }
}
