using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Граф объекта ↔ граф-файл — кнопки «Make Graph File» и «Embed in Object» в шапке окна графа.
    /// - в файл: объекты сцены из полей «значение или переменная», целей, точек и списков уходят в открытые переменные
    ///   и привязки этого объекта; объект сцены в обычном поле блока — отказ со списком таких полей
    /// - в объект: копия графа файла, привязки этого объекта — в начальные значения, «Set on Object» снимается
    /// Всё одной записью undo у сценария; созданный файл undo не удаляет.
    /// </summary>
    internal static class ScenarioGraphFileConverter
    {
        private const string MakeUndoName = "Make Graph File";
        private const string EmbedUndoName = "Embed Graph in Object";

        private static readonly Regex ValuePath = new Regex(@"^(.+)\.value$");
        private static readonly Regex TargetPath = new Regex(@"^(.+)\.specificObject$");
        private static readonly Regex PointPath = new Regex(@"^(.+)\.targetTransform$");
        private static readonly Regex ListItemPath = new Regex(@"^(.+)\.items\.Array\.data\[\d+\]$");
        private static readonly Regex VariableValuePath = new Regex(@"^graph\.variables\.Array\.data\[(\d+)\]\.initialValue\.");
        private static readonly Regex NodePath = new Regex(@"^graph\.nodes\.Array\.data\[(\d+)\]");


        // ── Можно ли ─────────────────────────────────────────────────────

        public static bool CanMakeGraphFile(ScenarioGraphHost host)
        {
            return host is { IsAlive: true, IsGraphFile: false } && host.Scenario != null && !EditorUtility.IsPersistent(host.Scenario);
        }

        /// <summary>Встроить можно только граф-файл, открытый через объект: в него и встраиваем.</summary>
        public static bool CanEmbed(ScenarioGraphHost host)
        {
            return host is { IsAlive: true, IsGraphFile: true } && host.Scenario != null;
        }


        // ── В файл ───────────────────────────────────────────────────────

        /// <summary>Создать граф-файл из встроенного графа и переключить сценарий на него. Отказ — список полей с объектами сцены, файл не создаётся.</summary>
        public static List<string> MakeGraphFile(Scenario scenario, string assetPath)
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = DeepCopy(scenario.Graph);

            // Копия через текст теряет объекты сцены, а они-то и нужны: кладём на те же места до разбора.
            var sceneReferences = ScenarioSceneReferences.Collect(new SerializedObject(scenario).FindProperty(ScenarioGraphHost.GraphPropertyPath),
                ScenarioGraphHost.GraphPropertyPath);
            ScenarioSceneReferences.Restore(new SerializedObject(asset), sceneReferences);

            var extraction = new ScenarioSceneObjectExtraction(asset);
            extraction.Run();

            if (extraction.Refusals.Count > 0)
            {
                Object.DestroyImmediate(asset);
                return extraction.Refusals;
            }

            extraction.Apply();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssetIfDirty(asset);

            var serializedScenario = new SerializedObject(scenario);
            Undo.RecordObject(scenario, MakeUndoName);
            serializedScenario.FindProperty("graphSource").enumValueIndex = (int)ScenarioGraphSource.Asset;
            serializedScenario.FindProperty("graphAsset").objectReferenceValue = asset;
            serializedScenario.FindProperty(ScenarioGraphHost.GraphPropertyPath).boxedValue = new ScenarioGraph();
            serializedScenario.FindProperty(ScenarioBindingsEditing.BindingsPropertyName).arraySize = 0;

            var bindings = new ScenarioBindingsEditing(serializedScenario);

            foreach (var (variableId, value) in extraction.BoundValues)
                bindings.AddBinding(variableId).boxedValue = value;

            serializedScenario.ApplyModifiedProperties();

            return new List<string>();
        }


        // ── В объект ─────────────────────────────────────────────────────

        public static void EmbedInObject(Scenario scenario)
        {
            var graph = DeepCopy(scenario.GraphAsset.Graph);

            foreach (var variable in graph.Variables.Where(variable => variable is { IsExposed: true }))
            {
                var binding = scenario.VariableBindings.FirstOrDefault(candidate => candidate.VariableId == variable.Id);

                if (binding != null)
                    CopyValue(binding.Value, variable.InitialValue);

                variable.IsExposed = false;
            }

            var serializedScenario = new SerializedObject(scenario);
            Undo.RecordObject(scenario, EmbedUndoName);
            serializedScenario.FindProperty(ScenarioGraphHost.GraphPropertyPath).boxedValue = graph;
            serializedScenario.FindProperty("graphSource").enumValueIndex = (int)ScenarioGraphSource.Embedded;
            serializedScenario.FindProperty("graphAsset").objectReferenceValue = null;
            serializedScenario.FindProperty(ScenarioBindingsEditing.BindingsPropertyName).arraySize = 0;
            serializedScenario.ApplyModifiedProperties();
        }


        // ── Общие помощники ──────────────────────────────────────────────

        /// <summary>Глубокая копия графа: свои ноды и блоки, ни одной общей ссылки с исходным. Ассеты целы, объекты сцены — пусты.</summary>
        public static ScenarioGraph DeepCopy(ScenarioGraph graph)
        {
            var copy = new ScenarioGraph();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(graph), copy);

            return copy;
        }

        public static void CopyValue(ScenarioVariableValue from, ScenarioVariableValue to)
        {
            to.FlagValue = from.FlagValue;
            to.CounterValue = from.CounterValue;
            to.NumberValue = from.NumberValue;
            to.ObjectValue = from.ObjectValue;
            to.VectorValue = from.VectorValue;
            to.TextValue = from.TextValue;
            to.ColorValue = from.ColorValue;
            to.ObjectListValue = new List<Object>(from.ObjectListValue);
        }


        /// <summary>
        /// Поиск объектов сцены в копии графа для файла: что куда перенести и где отказать.
        /// Переносится в переменные и значения привязок; один объект в нескольких полях — одна переменная.
        /// </summary>
        private class ScenarioSceneObjectExtraction
        {
            private readonly ScenarioGraphAsset asset;
            private readonly SerializedObject serializedAsset;
            private readonly List<ScenarioVariableDefinition> newVariables = new List<ScenarioVariableDefinition>();
            private readonly Dictionary<Object, ScenarioVariableDefinition> variablesByObject = new Dictionary<Object, ScenarioVariableDefinition>();
            private readonly HashSet<string> handledListPaths = new HashSet<string>();


            public ScenarioSceneObjectExtraction(ScenarioGraphAsset asset)
            {
                this.asset = asset;
                serializedAsset = new SerializedObject(asset);
            }


            public List<string> Refusals { get; } = new List<string>();

            public Dictionary<string, ScenarioVariableValue> BoundValues { get; } = new Dictionary<string, ScenarioVariableValue>();


            public void Run()
            {
                var property = serializedAsset.GetIterator();

                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null
                                                                                         && !EditorUtility.IsPersistent(property.objectReferenceValue))
                        Extract(property.Copy());
                }
            }

            /// <summary>Записать переносы в копию графа: сначала поля, потом новые переменные — в обход SerializedObject.</summary>
            public void Apply()
            {
                serializedAsset.ApplyModifiedPropertiesWithoutUndo();

                foreach (var variable in newVariables)
                    asset.Graph.AddVariable(variable);
            }


            private void Extract(SerializedProperty property)
            {
                var path = property.propertyPath;
                Match match;

                if ((match = ListItemPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                    ExtractList(match.Groups[1].Value);
                else if ((match = VariableValuePath.Match(path)).Success)
                    ExposeVariable(int.Parse(match.Groups[1].Value));
                else if ((match = ValuePath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                    MoveToVariable(property, match.Groups[1].Value, property.objectReferenceValue.GetType(), null);
                else if ((match = TargetPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                    MoveToVariable(property, match.Groups[1].Value, typeof(GameObject), (int)ScenarioTargetKind.Variable);
                else if ((match = PointPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                    MoveToVariable(property, match.Groups[1].Value, typeof(Transform), (int)ScenarioPointKind.Variable);
                else
                    Refusals.Add($"{NodeTitleOf(path)} · {property.displayName}: {property.objectReferenceValue.name}");
            }

            private bool HasVariableId(string ownerPath)
            {
                return serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}") != null;
            }

            // Поле «значение», цель или точка: переменная Object с этим объектом в привязке; у цели и точки — вид «переменная».
            private void MoveToVariable(SerializedProperty property, string ownerPath, System.Type objectType, int? variableKindIndex)
            {
                var sceneObject = property.objectReferenceValue;

                if (!variablesByObject.TryGetValue(sceneObject, out var variable))
                {
                    variable = AddVariable(sceneObject.name, ScenarioVariableKind.Object, objectType);
                    variablesByObject[sceneObject] = variable;
                    BoundValues[variable.Id] = new ScenarioVariableValue { ObjectValue = sceneObject };
                }

                serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}").stringValue = variable.Id;
                property.objectReferenceValue = null;

                if (variableKindIndex.HasValue)
                    serializedAsset.FindProperty($"{ownerPath}.kind").enumValueIndex = variableKindIndex.Value;
            }

            // Список целиком — в одну переменную Object List: тип элемента — по первому объекту.
            private void ExtractList(string ownerPath)
            {
                if (!handledListPaths.Add(ownerPath))
                    return;

                var items = serializedAsset.FindProperty($"{ownerPath}.items");
                var objects = Enumerable.Range(0, items.arraySize).Select(index => items.GetArrayElementAtIndex(index).objectReferenceValue).ToList();
                var firstObject = objects.First(item => item != null);
                var variable = AddVariable(serializedAsset.FindProperty(ownerPath).displayName, ScenarioVariableKind.ObjectList, firstObject.GetType());

                BoundValues[variable.Id] = new ScenarioVariableValue { ObjectListValue = objects };
                serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}").stringValue = variable.Id;
                items.arraySize = 0;
            }

            // Переменная графа уже держала объект сцены: она становится открытой, объект — в привязку.
            private void ExposeVariable(int variableIndex)
            {
                var variableProperty = serializedAsset.FindProperty($"{ScenarioGraphHost.GraphPropertyPath}.variables.Array.data[{variableIndex}]");
                var variableId = variableProperty.FindPropertyRelative("id").stringValue;

                if (BoundValues.ContainsKey(variableId))
                    return;

                var initialValue = variableProperty.FindPropertyRelative("initialValue");
                var value = (ScenarioVariableValue)initialValue.boxedValue;

                BoundValues[variableId] = value;
                variableProperty.FindPropertyRelative("isExposed").boolValue = true;
                initialValue.boxedValue = new ScenarioVariableValue();
            }

            private ScenarioVariableDefinition AddVariable(string name, ScenarioVariableKind kind, System.Type objectType)
            {
                var takenNames = asset.Graph.Variables.Select(variable => variable.Name).Concat(newVariables.Select(variable => variable.Name)).ToList();
                var uniqueName = name;

                for (var number = 2; takenNames.Contains(uniqueName); number++)
                    uniqueName = $"{name} {number}";

                var variable = new ScenarioVariableDefinition(uniqueName, kind) { ObjectType = objectType, IsExposed = true };
                newVariables.Add(variable);

                return variable;
            }

            private string NodeTitleOf(string path)
            {
                var match = NodePath.Match(path);

                if (!match.Success)
                    return "Graph";

                var node = asset.Graph.Nodes[int.Parse(match.Groups[1].Value)];

                return node != null ? node.Title : "Missing node";
            }
        }
    }
}
