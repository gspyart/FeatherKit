using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Значение переменной (ScenarioVariableValue) — поле того вида, какая переменная рядом.
    /// - соседи из [ScenarioValueKindFrom] на поле; без него — «variable» (блок), «variableId» (привязка), «id» (сама переменная)
    /// - выбрано «взять из другой переменной» или сравнение «пусто» — своё значение не нужно, поле прячется
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioVariableValue))]
    internal class ScenarioVariableValueDrawer : PropertyDrawer
    {
        private const string ChooseVariableFirstText = "Choose a variable first";
        private const string DefaultVariableFieldName = "variable";

        private static readonly string[] DefaultReplacingVariableNames = { "sourceVariable", "otherVariable" };


        private ScenarioValueKindFromAttribute KindFrom =>
            fieldInfo != null ? (ScenarioValueKindFromAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(ScenarioValueKindFromAttribute)) : null;


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsNotNeeded(property, KindFrom))
                return 0f;

            var variable = VariableOf(property, KindFrom);

            return variable != null ? ScenarioVariableValueGUI.HeightOf(property, variable.Kind) : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsNotNeeded(property, KindFrom))
                return;

            var variable = VariableOf(property, KindFrom);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                if (variable == null)
                    EditorGUI.LabelField(position, label, new GUIContent(ChooseVariableFirstText), EditorStyles.miniLabel);
                else
                    ScenarioVariableValueGUI.Draw(position, property, label, variable);
            }
        }


        // ── Соседи ───────────────────────────────────────────────────────

        private static bool IsNotNeeded(SerializedProperty property, ScenarioValueKindFromAttribute kindFrom)
        {
            var replacingNames = kindFrom != null ? (System.Collections.Generic.IEnumerable<string>)kindFrom.ReplacingVariableFieldNames : DefaultReplacingVariableNames;

            foreach (var name in replacingNames)
            {
                var variableId = SiblingOf(property, name)?.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);

                if (!string.IsNullOrEmpty(variableId?.stringValue))
                    return true;
            }

            var comparison = string.IsNullOrEmpty(kindFrom?.ComparisonFieldName) ? null : SiblingOf(property, kindFrom.ComparisonFieldName);

            return comparison != null && ((ScenarioVariableComparison)comparison.enumValueIndex).IsEmptinessCheck();
        }

        public static ScenarioVariableDefinition VariableOf(SerializedProperty property)
        {
            return VariableOf(property, null);
        }

        /// <summary>Переменная, чьего вида это поле. Null — переменная не выбрана или не найдена.</summary>
        public static ScenarioVariableDefinition VariableOf(SerializedProperty property, ScenarioValueKindFromAttribute kindFrom)
        {
            var graph = GraphOf(property.serializedObject.targetObject);

            if (graph == null)
                return null;

            var chosenId = SiblingOf(property, kindFrom?.VariableFieldName ?? DefaultVariableFieldName)
                ?.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);

            if (chosenId != null)
                return graph.FindVariable(chosenId.stringValue);

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
    /// Сравнение переменной (ScenarioVariableComparison) — только подходящие виду выбранной переменной:
    /// больше-меньше — у Counter и Number, «пусто» — у Object, Object List и Text. Переменная не выбрана — все.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioVariableComparison))]
    internal class ScenarioVariableComparisonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kindFrom = fieldInfo != null
                ? (ScenarioValueKindFromAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(ScenarioValueKindFromAttribute))
                : null;
            var variable = ScenarioVariableValueDrawer.VariableOf(property, kindFrom);
            var fitting = FittingComparisons(variable?.Kind);
            var current = (ScenarioVariableComparison)property.enumValueIndex;
            var problem = fitting.Contains(current) ? null : $"“{current.Symbol()}” doesn't fit a {variable?.Kind} variable";

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = ScenarioVariableField.PrefixLabel(position, label, problem != null);
                var options = fitting.Contains(current) ? fitting : fitting.Prepend(current).ToArray();
                var names = options.Select(option => new GUIContent(option.Symbol(), problem)).ToArray();

                EditorGUI.BeginChangeCheck();
                var chosen = EditorGUI.Popup(fieldRect, System.Array.IndexOf(options, current), names);

                if (EditorGUI.EndChangeCheck())
                    property.enumValueIndex = (int)options[chosen];
            }
        }

        public static ScenarioVariableComparison[] FittingComparisons(ScenarioVariableKind? kind)
        {
            var all = (ScenarioVariableComparison[])System.Enum.GetValues(typeof(ScenarioVariableComparison));

            return kind switch
            {
                null => all,
                ScenarioVariableKind.Counter or ScenarioVariableKind.Number => all.Where(comparison => !comparison.IsEmptinessCheck()).ToArray(),
                ScenarioVariableKind.Object or ScenarioVariableKind.ObjectList or ScenarioVariableKind.Text =>
                    all.Where(comparison => comparison is ScenarioVariableComparison.Equal or ScenarioVariableComparison.NotEqual
                                            || comparison.IsEmptinessCheck()).ToArray(),
                _ => new[] { ScenarioVariableComparison.Equal, ScenarioVariableComparison.NotEqual }
            };
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
