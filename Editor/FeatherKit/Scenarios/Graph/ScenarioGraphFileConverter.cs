using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Граф объекта ↔ граф-файл — кнопки «Make Graph File» и «Embed in Object» в шапке окна графа.
    /// - в файл: объекты сцены, которые поля читают, — в открытые переменные и привязки этого объекта (ScenarioSceneObjectExtraction);
    ///   объект сцены в обычном поле блока — отказ со списком полей
    /// - в объект: копия графа файла, привязки этого объекта — в начальные значения, «Set on Object» снимается
    /// - граф с нодами пропавших классов не переносится: их данные не копирует ни один способ Unity — отказ, а не тихая потеря
    /// Копия — сериализацией Unity, а не текстом: объекты сцены целы. Одна запись undo у сценария.
    /// </summary>
    internal static class ScenarioGraphFileConverter
    {
        private const string MakeUndoName = "Make Graph File";
        private const string EmbedUndoName = "Embed Graph in Object";
        private const string GraphSourcePropertyName = "graphSource";
        private const string GraphAssetPropertyName = "graphAsset";


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

        /// <summary>Создать граф-файл из встроенного графа и переключить сценарий на него. Отказ — список причин, файл не создаётся.</summary>
        public static List<string> MakeGraphFile(Scenario scenario, string assetPath)
        {
            var missingClasses = MissingClassesOf(scenario);

            if (missingClasses.Count > 0)
                return missingClasses;

            // Поле графа у компонента и у файла называется одинаково — Unity копирует его целиком, со ссылками на сцену.
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            EditorUtility.CopySerializedManagedFieldsOnly(scenario, asset);

            var extraction = new ScenarioSceneObjectExtraction(asset);
            extraction.Plan();

            if (extraction.Refusals.Count > 0)
            {
                Object.DestroyImmediate(asset);
                return extraction.Refusals;
            }

            extraction.Apply();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssetIfDirty(asset);

            Undo.RecordObject(scenario, MakeUndoName);
            var serializedScenario = new SerializedObject(scenario);
            serializedScenario.FindProperty(GraphSourcePropertyName).enumValueIndex = (int)ScenarioGraphSource.Asset;
            serializedScenario.FindProperty(GraphAssetPropertyName).objectReferenceValue = asset;
            serializedScenario.FindProperty(ScenarioGraphHost.GraphPropertyPath).boxedValue = new ScenarioGraph();
            serializedScenario.FindProperty(ScenarioBindingsEditing.BindingsPropertyName).arraySize = 0;

            var bindings = new ScenarioBindingsEditing(serializedScenario);

            foreach (var (variableId, value) in extraction.BoundValues)
                bindings.AddBinding(variableId).boxedValue = value;

            serializedScenario.ApplyModifiedProperties();

            return new List<string>();
        }


        // ── В объект ─────────────────────────────────────────────────────

        /// <summary>Скопировать граф-файл в объект. Отказ — список причин, объект не меняется.</summary>
        public static List<string> EmbedInObject(Scenario scenario)
        {
            var missingClasses = MissingClassesOf(scenario.GraphAsset);

            if (missingClasses.Count > 0)
                return missingClasses;

            var boundValues = scenario.VariableBindings.Where(binding => binding != null)
                .ToDictionary(binding => binding.VariableId, binding => CopyOf(binding.Value));

            Undo.RecordObject(scenario, EmbedUndoName);
            EditorUtility.CopySerializedManagedFieldsOnly(scenario.GraphAsset, scenario);

            var serializedScenario = new SerializedObject(scenario);
            var variables = serializedScenario.FindProperty($"{ScenarioGraphHost.GraphPropertyPath}.variables");

            for (var index = 0; index < variables.arraySize; index++)
            {
                var variable = variables.GetArrayElementAtIndex(index);
                var isExposed = variable.FindPropertyRelative("isExposed");

                if (!isExposed.boolValue)
                    continue;

                if (boundValues.TryGetValue(variable.FindPropertyRelative("id").stringValue, out var boundValue))
                    variable.FindPropertyRelative("initialValue").boxedValue = boundValue;

                isExposed.boolValue = false;
            }

            serializedScenario.FindProperty(GraphSourcePropertyName).enumValueIndex = (int)ScenarioGraphSource.Embedded;
            serializedScenario.FindProperty(GraphAssetPropertyName).objectReferenceValue = null;
            serializedScenario.FindProperty(ScenarioBindingsEditing.BindingsPropertyName).arraySize = 0;
            serializedScenario.ApplyModifiedProperties();

            return new List<string>();
        }


        // ── Общие помощники ──────────────────────────────────────────────

        // Проверено: Instantiate, CopySerializedManagedFieldsOnly, CopyFromSerializedProperty и boxedValue такие ноды теряют.
        private static List<string> MissingClassesOf(Object graphOwner)
        {
            if (!SerializationUtility.HasManagedReferencesWithMissingTypes(graphOwner))
                return new List<string>();

            var classNames = SerializationUtility.GetManagedReferencesWithMissingTypes(graphOwner)
                .Select(missing => missing.className).Distinct();

            return new List<string>
            {
                $"The graph has nodes or blocks of missing classes: {string.Join(", ", classNames)}. "
                + "They can't be copied — remove them or bring their classes back first."
            };
        }

        private static ScenarioVariableValue CopyOf(ScenarioVariableValue value)
        {
            return new ScenarioVariableValue
            {
                FlagValue = value.FlagValue,
                CounterValue = value.CounterValue,
                NumberValue = value.NumberValue,
                ObjectValue = value.ObjectValue,
                VectorValue = value.VectorValue,
                TextValue = value.TextValue,
                ColorValue = value.ColorValue,
                ObjectListValue = new List<Object>(value.ObjectListValue)
            };
        }
    }
}
