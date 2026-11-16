using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Значение переменной (ScenarioVariableValue) — поле того вида, какая переменная рядом.
    /// Переменную ищет у соседей, не зная блоков: поле «variable» (выбор переменной в блоке), «variableId» (привязка)
    /// или «id» (сама переменная — её начальное значение). Соседний выбор «sourceVariable» / «otherVariable» прячет поле.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioVariableValue))]
    internal class ScenarioVariableValueDrawer : PropertyDrawer
    {
        private const string ChooseVariableFirstText = "Choose a variable first";

        private static readonly string[] ChosenVariableNames = { "variable" };
        private static readonly string[] ReplacingVariableNames = { "sourceVariable", "otherVariable" };


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsReplacedByVariable(property))
                return 0f;

            var variable = VariableOf(property);

            return variable != null ? ScenarioVariableValueGUI.HeightOf(property, variable.Kind) : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsReplacedByVariable(property))
                return;

            var variable = VariableOf(property);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                if (variable == null)
                    EditorGUI.LabelField(position, label, new GUIContent(ChooseVariableFirstText), EditorStyles.miniLabel);
                else
                    ScenarioVariableValueGUI.Draw(position, property, label, variable);
            }
        }


        // ── Соседи ───────────────────────────────────────────────────────

        private static bool IsReplacedByVariable(SerializedProperty property)
        {
            foreach (var name in ReplacingVariableNames)
            {
                var variableId = SiblingOf(property, name)?.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);

                if (!string.IsNullOrEmpty(variableId?.stringValue))
                    return true;
            }

            return false;
        }

        /// <summary>Переменная, чьего вида это значение. Null — переменная не выбрана или не найдена.</summary>
        public static ScenarioVariableDefinition VariableOf(SerializedProperty property)
        {
            var graph = GraphOf(property.serializedObject.targetObject);

            if (graph == null)
                return null;

            foreach (var name in ChosenVariableNames)
            {
                var chosenId = SiblingOf(property, name)?.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);

                if (chosenId != null)
                    return graph.FindVariable(chosenId.stringValue);
            }

            var bindingId = SiblingOf(property, ScenarioVariablePicking.VariableIdPropertyName);

            if (bindingId is { propertyType: SerializedPropertyType.String })
                return graph.FindVariable(bindingId.stringValue);

            var ownId = SiblingOf(property, "id");

            return ownId is { propertyType: SerializedPropertyType.String } ? graph.FindVariable(ownId.stringValue) : null;
        }

        // Привязки лежат на Scenario, а переменные — в его граф-файле.
        private static ScenarioGraph GraphOf(Object owner)
        {
            return owner switch
            {
                ScenarioGraphAsset asset => asset.Graph,
                Scenario { GraphSource: ScenarioGraphSource.Asset } scenario => scenario.GraphAsset != null ? scenario.GraphAsset.Graph : null,
                Scenario scenario => scenario.Graph,
                _ => null
            };
        }

        private static SerializedProperty SiblingOf(SerializedProperty property, string name)
        {
            var path = property.propertyPath;
            var cut = path.LastIndexOf('.');

            return property.serializedObject.FindProperty(cut > 0 ? $"{path.Substring(0, cut)}.{name}" : name);
        }
    }


    /// <summary>
    /// Поле значения переменной по её виду — одно на блоки, панель переменных и привязки в инспекторе Scenario.
    /// Ещё не заведённая привязка рисуется значением файла: правка заводит привязку, файл не меняется.
    /// </summary>
    internal static class ScenarioVariableValueGUI
    {
        public static float HeightOf(SerializedProperty value, ScenarioVariableKind kind)
        {
            return kind switch
            {
                ScenarioVariableKind.ObjectList => ScenarioObjectListGUI.HeightOf(value.FindPropertyRelative(FieldNameOf(kind))),
                _ => EditorGUIUtility.singleLineHeight
            };
        }

        public static void Draw(Rect rect, SerializedProperty value, GUIContent label, ScenarioVariableDefinition variable)
        {
            var field = value.FindPropertyRelative(FieldNameOf(variable.Kind));

            switch (variable.Kind)
            {
                case ScenarioVariableKind.Object:
                    ScenarioObjectFieldGUI.Draw(EditorGUI.PrefixLabel(rect, label), field, variable.ObjectType);
                    break;

                case ScenarioVariableKind.ObjectList:
                    var listRect = EditorGUI.PrefixLabel(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), label);
                    ScenarioObjectListGUI.Draw(new Rect(listRect.x, rect.y, listRect.width, rect.height), field, variable.ObjectType);
                    break;

                // В одну строку и на узкой панели: иначе Unity переносит X Y Z под подпись.
                case ScenarioVariableKind.Vector3:
                    EditorGUI.PropertyField(EditorGUI.PrefixLabel(rect, label), field, GUIContent.none);
                    break;

                default:
                    EditorGUI.PropertyField(rect, field, label);
                    break;
            }
        }

        /// <summary>Поле ScenarioVariableValue, которое читается у переменной этого вида.</summary>
        public static string FieldNameOf(ScenarioVariableKind kind)
        {
            return kind switch
            {
                ScenarioVariableKind.Flag => "flagValue",
                ScenarioVariableKind.Counter => "counterValue",
                ScenarioVariableKind.Number => "numberValue",
                ScenarioVariableKind.Vector3 => "vectorValue",
                ScenarioVariableKind.Text => "textValue",
                ScenarioVariableKind.Color => "colorValue",
                ScenarioVariableKind.ObjectList => "objectListValue",
                _ => "objectValue"
            };
        }


        /// <summary>
        /// Своей привязки нет: рисуем значение файла, а правку переносим в новую привязку и откатываем в файле.
        /// fileValue — начальное значение в SerializedObject граф-файла; true — привязка заведена, применить её — дело зовущего.
        /// </summary>
        public static bool DrawUnboundValue(Rect rect, GUIContent label, ScenarioVariableDefinition variable, SerializedProperty fileValue,
            ScenarioBindingsEditing bindings)
        {
            fileValue.serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            Draw(rect, fileValue, label, variable);

            if (!EditorGUI.EndChangeCheck())
                return false;

            var editedValue = fileValue.boxedValue;
            fileValue.serializedObject.Update();
            bindings.AddBinding(variable.Id).boxedValue = editedValue;

            return true;
        }
    }
}
