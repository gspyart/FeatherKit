using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Раздел инспектора Scenario «Set on Object»: значения открытых переменных граф-файла на этом объекте.
    /// - только для граф-файла: во встроенном графе объекты сцены кладутся прямо в переменные
    /// - пока значение не меняли, видно начальное из файла; правка заводит привязку, «↺» её убирает
    /// - открытая Object без значения — предупреждение; привязки к пропавшим переменным чистятся кнопкой
    /// </summary>
    internal class ScenarioBindingsSection
    {
        private const float ResetButtonWidth = 22f;

        private readonly ScenarioBindingsEditing bindings;


        public ScenarioBindingsSection(SerializedObject serializedObject)
        {
            bindings = new ScenarioBindingsEditing(serializedObject);
        }


        public void Draw()
        {
            var scenario = bindings.Scenario;

            if (!ScenarioBindingsEditing.AreUsedBy(scenario) || scenario.Graph == null)
                return;

            var graph = scenario.Graph;
            var exposedVariables = graph.Variables.Where(variable => variable is { IsExposed: true }).ToList();

            if (exposedVariables.Count > 0)
            {
                EditorGUILayout.LabelField("Set on Object", EditorStyles.boldLabel);

                foreach (var variable in exposedVariables)
                    DrawVariableValue(variable);
            }

            DrawVariableProblems(graph);
            DrawUnusedBindingsButton(graph);
        }


        // ── Значение переменной ──────────────────────────────────────────

        private void DrawVariableValue(ScenarioVariableDefinition variable)
        {
            var boundValue = bindings.ValueOf(variable.Id);
            var label = new GUIContent(variable.Name, ScenarioIcons.IconOf(variable),
                boundValue != null ? "Set on this object" : "The graph file's default — change it to set your own");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (boundValue != null)
                    DrawBoundValue(label, variable, boundValue);
                else
                    DrawDefaultValue(label, variable);

                using (new EditorGUI.DisabledScope(boundValue == null))
                {
                    if (GUILayout.Button(new GUIContent("↺", "Back to the graph file's default"), EditorStyles.miniButton,
                            GUILayout.Width(ResetButtonWidth)))
                        bindings.RemoveBinding(variable.Id);
                }
            }
        }

        private static void DrawBoundValue(GUIContent label, ScenarioVariableDefinition variable, SerializedProperty value)
        {
            switch (variable.Kind)
            {
                case ScenarioVariableKind.Flag:
                    EditorGUILayout.PropertyField(value.FindPropertyRelative("flagValue"), label);
                    break;

                case ScenarioVariableKind.Counter:
                    EditorGUILayout.PropertyField(value.FindPropertyRelative("counterValue"), label);
                    break;

                case ScenarioVariableKind.Number:
                    EditorGUILayout.PropertyField(value.FindPropertyRelative("numberValue"), label);
                    break;

                default:
                    var objectValue = value.FindPropertyRelative("objectValue");
                    objectValue.objectReferenceValue = EditorGUILayout.ObjectField(label, objectValue.objectReferenceValue,
                        variable.ObjectType, allowSceneObjects: true);
                    break;
            }
        }

        // Своего значения ещё нет: показываем начальное из файла, правка заводит привязку с новым значением.
        private void DrawDefaultValue(GUIContent label, ScenarioVariableDefinition variable)
        {
            var initial = variable.InitialValue;

            EditorGUI.BeginChangeCheck();

            switch (variable.Kind)
            {
                case ScenarioVariableKind.Flag:
                    var flag = EditorGUILayout.Toggle(label, initial.FlagValue);
                    if (EditorGUI.EndChangeCheck())
                        bindings.AddBinding(variable.Id).FindPropertyRelative("flagValue").boolValue = flag;
                    break;

                case ScenarioVariableKind.Counter:
                    var counter = EditorGUILayout.IntField(label, initial.CounterValue);
                    if (EditorGUI.EndChangeCheck())
                        bindings.AddBinding(variable.Id).FindPropertyRelative("counterValue").intValue = counter;
                    break;

                case ScenarioVariableKind.Number:
                    var number = EditorGUILayout.FloatField(label, initial.NumberValue);
                    if (EditorGUI.EndChangeCheck())
                        bindings.AddBinding(variable.Id).FindPropertyRelative("numberValue").floatValue = number;
                    break;

                default:
                    var objectValue = EditorGUILayout.ObjectField(label, initial.ObjectValue, variable.ObjectType, allowSceneObjects: true);
                    if (EditorGUI.EndChangeCheck())
                        bindings.AddBinding(variable.Id).FindPropertyRelative("objectValue").objectReferenceValue = objectValue;
                    break;
            }
        }


        // ── Проверки ─────────────────────────────────────────────────────

        // Тот же список, что у окна графа: строка состояния, панель переменных и инспектор говорят одно и то же.
        private void DrawVariableProblems(ScenarioGraph graph)
        {
            foreach (var problem in ScenarioGraphValidation.CheckVariables(graph, isGraphFile: true, bindings))
                EditorGUILayout.HelpBox(problem.Message, problem.IsError ? MessageType.Error : MessageType.Warning);
        }

        private void DrawUnusedBindingsButton(ScenarioGraph graph)
        {
            var unusedCount = bindings.CountUnused(graph);

            if (unusedCount > 0 && GUILayout.Button($"Remove Unused Bindings ({unusedCount})", EditorStyles.miniButton))
                bindings.RemoveUnused(graph);
        }
    }
}
