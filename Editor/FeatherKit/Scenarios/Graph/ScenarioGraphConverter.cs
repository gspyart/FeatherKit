using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Старый Scenario (списки When → If → Do) → граф: нода When (события, условия, Only Once) + нода Do (шаги) + провод Then → In.
    /// - старые поля очищаются, всё одной записью undo; экземпляр префаба получает свои overrides
    /// - в When всегда добавляется «Called»: старый сценарий запускали и кнопки, и Timeline, и другие сценарии
    /// - старые блоки потока (If, Parallel, Random, Repeat…) остаются блоками в Do — о каждом предупреждение в консоли
    /// </summary>
    internal static class ScenarioGraphConverter
    {
        private const string MenuPath = "FeatherKit/Scenarios/Convert Old Scenarios to Graphs";
        private const string UndoName = "Convert Scenario to Graph";

        private static readonly Vector2 WhenPosition = Vector2.zero;
        private static readonly Vector2 StepsPosition = new Vector2(400f, 0f);

        // Их заменяют ноды потока; разворачивать в ноды не берёмся — слишком разные случаи, проще переложить руками.
        private static readonly HashSet<string> OldFlowBlockNames = new HashSet<string>
        {
            "IfAction", "ParallelAction", "RandomBranchAction", "RepeatAction", "WaitForBackgroundAction", "WaitUntilAction"
        };


        // ── Что конвертировать ───────────────────────────────────────────

        /// <summary>Сценарий живёт старыми списками и в них что-то есть: встроенный граф пуст.</summary>
        public static bool CanConvert(Scenario scenario)
        {
            if (scenario == null || scenario.GraphSource != ScenarioGraphSource.Embedded || scenario.Graph is { Nodes: { Count: > 0 } })
                return false;

            var serializedScenario = new SerializedObject(scenario);

            return serializedScenario.FindProperty("events").arraySize > 0
                   || serializedScenario.FindProperty("conditions").arraySize > 0
                   || serializedScenario.FindProperty("actions").arraySize > 0;
        }


        // ── Конвертация ──────────────────────────────────────────────────

        /// <summary>Перевести один сценарий в граф. recordsUndo = false — для содержимого префаба, открытого кодом.</summary>
        public static ScenarioGraphConversion Convert(Scenario scenario, bool recordsUndo = true)
        {
            var serializedScenario = new SerializedObject(scenario);
            var events = BlocksOf<ScenarioEvent>(serializedScenario.FindProperty("events"));
            var conditions = BlocksOf<ScenarioCondition>(serializedScenario.FindProperty("conditions"));
            var actions = BlocksOf<ScenarioAction>(serializedScenario.FindProperty("actions"));
            var conditionsMode = (ScenarioConditionsMode)serializedScenario.FindProperty("conditionsMode").enumValueIndex;
            var runsOnce = serializedScenario.FindProperty("runsOnce").boolValue;

            if (recordsUndo)
                Undo.RecordObject(scenario, UndoName);

            var conversion = BuildGraph(scenario.Graph, events, conditions, conditionsMode, runsOnce, actions);

            if (PrefabUtility.IsPartOfPrefabInstance(scenario))
                PrefabUtility.RecordPrefabInstancePropertyModifications(scenario);

            EditorUtility.SetDirty(scenario);
            ClearOldLists(serializedScenario, recordsUndo);
            WarnAboutOldFlowBlocks(scenario, conversion);

            return conversion;
        }

        private static ScenarioGraphConversion BuildGraph(ScenarioGraph graph, List<ScenarioEvent> events, List<ScenarioCondition> conditions,
            ScenarioConditionsMode conditionsMode, bool runsOnce, List<ScenarioAction> actions)
        {
            var when = graph.AddNode(new ScenarioWhenNode { RunsOnlyOnce = runsOnce }, WhenPosition);

            foreach (var scenarioEvent in events)
                when.AddEvent(scenarioEvent);

            when.AddEvent(new CalledEvent());
            when.Conditions.Mode = conditionsMode;

            foreach (var condition in conditions)
                when.Conditions.Add(condition);

            var steps = graph.AddNode(new ScenarioStepsNode(), StepsPosition);

            foreach (var action in actions)
                steps.AddStep(action);

            graph.Connect(when, ScenarioWhenNode.Then, steps, ScenarioStepsNode.In);

            var oldFlowBlocks = actions.Where(action => OldFlowBlockNames.Contains(action.GetType().Name))
                .Select(action => ScenarioBlockCatalog.TitleOf(action.GetType()))
                .ToList();

            return new ScenarioGraphConversion(when.Id, steps.Id, oldFlowBlocks);
        }

        // Блоки переехали в ноды по ссылке: в старых полях их остаётся только убрать.
        private static void ClearOldLists(SerializedObject serializedScenario, bool recordsUndo)
        {
            serializedScenario.Update();
            serializedScenario.FindProperty("events").arraySize = 0;
            serializedScenario.FindProperty("conditions").arraySize = 0;
            serializedScenario.FindProperty("actions").arraySize = 0;
            serializedScenario.FindProperty("conditionsMode").enumValueIndex = 0;
            serializedScenario.FindProperty("runsOnce").boolValue = false;

            if (recordsUndo)
                serializedScenario.ApplyModifiedProperties();
            else
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WarnAboutOldFlowBlocks(Scenario scenario, ScenarioGraphConversion conversion)
        {
            if (conversion.OldFlowBlocks.Count > 0)
                Debug.LogWarning($"Scenario “{scenario.name}” converted, but its Do node still has old flow blocks: "
                                 + $"{string.Join(", ", conversion.OldFlowBlocks)}. They work, but are better rebuilt with flow nodes "
                                 + "(Branch, Parallel, Repeat, Wait For).", scenario);
        }

        private static List<T> BlocksOf<T>(SerializedProperty blocks) where T : class
        {
            var result = new List<T>();

            for (var index = 0; index < blocks.arraySize; index++)
            {
                if (blocks.GetArrayElementAtIndex(index).managedReferenceValue is T block)
                    result.Add(block);
            }

            return result;
        }


        // ── Все сценарии открытой сцены или префаба ──────────────────────

        [MenuItem(MenuPath)]
        private static void ConvertAllOpen()
        {
            var scenarios = OpenScenarios().Where(CanConvert).ToList();

            if (scenarios.Count == 0)
            {
                EditorUtility.DisplayDialog("Convert old scenarios", "No scenarios with old lists in the open scenes or prefab.", "OK");
                return;
            }

            var names = string.Join("\n", scenarios.Take(15).Select(scenario => "• " + scenario.name));
            var isConfirmed = EditorUtility.DisplayDialog("Convert old scenarios",
                $"Convert {scenarios.Count} scenarios to graphs?\n\n{names}\n\nOne undo step brings them all back. Save the scene or prefab yourself.",
                "Convert", "Cancel");

            if (!isConfirmed)
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var scenario in scenarios)
                Convert(scenario);

            Undo.SetCurrentGroupName("Convert Scenarios to Graphs");
            Undo.CollapseUndoOperations(undoGroup);
        }

        // Открыт префаб — только его содержимое; иначе все загруженные сцены.
        private static IEnumerable<Scenario> OpenScenarios()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage != null)
                return prefabStage.prefabContentsRoot.GetComponentsInChildren<Scenario>(true);

            return Object.FindObjectsByType<Scenario>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }


    /// <summary>Что получилось при конвертации: ноды When и Do и старые блоки потока, оставшиеся в Do.</summary>
    internal class ScenarioGraphConversion
    {
        public ScenarioGraphConversion(int whenNodeId, int stepsNodeId, List<string> oldFlowBlocks)
        {
            WhenNodeId = whenNodeId;
            StepsNodeId = stepsNodeId;
            OldFlowBlocks = oldFlowBlocks;
        }


        public int WhenNodeId { get; }

        public int StepsNodeId { get; }

        public IReadOnlyList<string> OldFlowBlocks { get; }
    }
}
