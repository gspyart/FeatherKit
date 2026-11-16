using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Объекты сцены в копии графа для граф-файла — часть ScenarioGraphFileConverter: что куда перенести и где отказать.
    /// - объект, который поле читает, — в открытую переменную и значение привязки; один объект в нескольких полях — одна переменная
    /// - объект, который поле не читает (цель «This Object», выбрана переменная), — просто убирается
    /// - сначала план по всем полям, потом правки: правка посреди перебора SerializedProperty его ломает
    /// </summary>
    internal class ScenarioSceneObjectExtraction
    {
        private static readonly Regex ValuePath = new Regex(@"^(.+)\.value$");
        private static readonly Regex TargetPath = new Regex(@"^(.+)\.specificObject$");
        private static readonly Regex PointPath = new Regex(@"^(.+)\.targetTransform$");
        private static readonly Regex ListItemPath = new Regex(@"^(.+)\.items\.Array\.data\[\d+\]$");
        private static readonly Regex VariableValuePath = new Regex(@"^graph\.variables\.Array\.data\[(\d+)\]\.initialValue\.(objectValue|objectListValue)");
        private static readonly Regex NodePath = new Regex(@"^graph\.nodes\.Array\.data\[(\d+)\]");

        private readonly ScenarioGraphAsset asset;
        private readonly SerializedObject serializedAsset;
        private readonly List<ScenarioVariableDefinition> newVariables = new List<ScenarioVariableDefinition>();
        private readonly Dictionary<Object, ScenarioVariableDefinition> variablesByObject = new Dictionary<Object, ScenarioVariableDefinition>();
        private readonly HashSet<string> plannedOwners = new HashSet<string>();
        private readonly List<Action> edits = new List<Action>();


        public ScenarioSceneObjectExtraction(ScenarioGraphAsset asset)
        {
            this.asset = asset;
            serializedAsset = new SerializedObject(asset);
        }


        public List<string> Refusals { get; } = new List<string>();

        /// <summary>Значения привязок для этого объекта: id переменной → её значение здесь.</summary>
        public Dictionary<string, ScenarioVariableValue> BoundValues { get; } = new Dictionary<string, ScenarioVariableValue>();


        // ── План ─────────────────────────────────────────────────────────

        /// <summary>Пройти все поля и решить, что делать с каждым объектом сцены. Копия графа пока не меняется.</summary>
        public void Plan()
        {
            var property = serializedAsset.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null
                                                                                     && !EditorUtility.IsPersistent(property.objectReferenceValue))
                    PlanFor(property.propertyPath, property.objectReferenceValue);
            }
        }

        private void PlanFor(string path, Object sceneObject)
        {
            Match match;

            if ((match = ListItemPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                PlanList(match.Groups[1].Value);
            else if ((match = VariableValuePath.Match(path)).Success)
                PlanVariableValue(path, int.Parse(match.Groups[1].Value), match.Groups[2].Value == "objectListValue");
            else if ((match = ValuePath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                PlanField(path, match.Groups[1].Value, sceneObject, sceneObject.GetType(), null, !UsesVariable(match.Groups[1].Value));
            else if ((match = TargetPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                PlanField(path, match.Groups[1].Value, sceneObject, typeof(UnityEngine.GameObject), (int)ScenarioTargetKind.Variable,
                    KindOf(match.Groups[1].Value) == (int)ScenarioTargetKind.Specific);
            else if ((match = PointPath.Match(path)).Success && HasVariableId(match.Groups[1].Value))
                PlanField(path, match.Groups[1].Value, sceneObject, typeof(UnityEngine.Transform), (int)ScenarioPointKind.Variable,
                    KindOf(match.Groups[1].Value) == (int)ScenarioPointKind.Object);
            else
                Refusals.Add($"{NodeTitleOf(path)} · {serializedAsset.FindProperty(path).displayName}: {sceneObject.name}");
        }

        // Поле «значение», цель или точка. Читает объект — переменная Object с ним в привязке; не читает — объект просто убрать.
        private void PlanField(string path, string ownerPath, Object sceneObject, Type objectType, int? variableKindIndex, bool isRead)
        {
            edits.Add(() => ClearObject(path));

            if (!isRead)
                return;

            if (!variablesByObject.TryGetValue(sceneObject, out var variable))
            {
                variable = AddVariable(sceneObject.name, ScenarioVariableKind.Object, objectType);
                variablesByObject[sceneObject] = variable;
                BoundValues[variable.Id] = new ScenarioVariableValue { ObjectValue = sceneObject };
            }

            edits.Add(() =>
            {
                serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}").stringValue = variable.Id;

                if (variableKindIndex.HasValue)
                    serializedAsset.FindProperty($"{ownerPath}.kind").enumValueIndex = variableKindIndex.Value;
            });
        }

        // Свой список целиком — в одну переменную Object List; выбрана переменная — свой список не читается, его убрать.
        private void PlanList(string ownerPath)
        {
            if (!plannedOwners.Add(ownerPath))
                return;

            var items = serializedAsset.FindProperty($"{ownerPath}.items");
            var itemsPath = items.propertyPath;
            edits.Add(() => serializedAsset.FindProperty(itemsPath).arraySize = 0);

            if (UsesVariable(ownerPath))
                return;

            var objects = Enumerable.Range(0, items.arraySize).Select(index => items.GetArrayElementAtIndex(index).objectReferenceValue).ToList();
            var variable = AddVariable(serializedAsset.FindProperty(ownerPath).displayName, ScenarioVariableKind.ObjectList,
                objects.First(item => item != null).GetType());

            BoundValues[variable.Id] = new ScenarioVariableValue { ObjectListValue = objects };
            edits.Add(() => serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}").stringValue = variable.Id);
        }

        // Переменная графа держит объект сцены: её значение — в привязку, сама — открытая. Чужое виду значение — просто убрать.
        private void PlanVariableValue(string path, int variableIndex, bool isListItem)
        {
            var variable = asset.Graph.Variables[variableIndex];
            var isRead = variable.Kind == (isListItem ? ScenarioVariableKind.ObjectList : ScenarioVariableKind.Object);

            if (!isRead)
            {
                edits.Add(() => ClearObject(path));
                return;
            }

            var variablePath = $"{ScenarioGraphHost.GraphPropertyPath}.variables.Array.data[{variableIndex}]";

            if (!plannedOwners.Add(variablePath))
                return;

            BoundValues[variable.Id] = (ScenarioVariableValue)serializedAsset.FindProperty($"{variablePath}.initialValue").boxedValue;

            edits.Add(() =>
            {
                serializedAsset.FindProperty($"{variablePath}.isExposed").boolValue = true;
                serializedAsset.FindProperty($"{variablePath}.initialValue").boxedValue = new ScenarioVariableValue();
            });
        }


        // ── Правки ───────────────────────────────────────────────────────

        /// <summary>Записать план в копию графа: сначала поля, потом новые переменные — в обход SerializedObject.</summary>
        public void Apply()
        {
            foreach (var edit in edits)
                edit();

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();

            foreach (var variable in newVariables)
                asset.Graph.AddVariable(variable);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        // Поле могло уйти раньше: список очищен, значение переменной сброшено целиком.
        private void ClearObject(string path)
        {
            var property = serializedAsset.FindProperty(path);

            if (property != null)
                property.objectReferenceValue = null;
        }

        private bool HasVariableId(string ownerPath)
        {
            return serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}") != null;
        }

        private bool UsesVariable(string ownerPath)
        {
            return !string.IsNullOrEmpty(serializedAsset.FindProperty($"{ownerPath}.{ScenarioVariablePicking.VariableIdPropertyName}").stringValue);
        }

        private int KindOf(string ownerPath)
        {
            return serializedAsset.FindProperty($"{ownerPath}.kind")?.enumValueIndex ?? -1;
        }

        private ScenarioVariableDefinition AddVariable(string name, ScenarioVariableKind kind, Type objectType)
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
