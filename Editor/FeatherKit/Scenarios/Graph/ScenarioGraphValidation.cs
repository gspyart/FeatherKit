using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Что в графе не так — без UI: для значков на нодах, счётчика в строке состояния и инспектора.
    /// - на нодах: блок пропал или пуст, переменная не найдена или не того вида, не выбран объект, флаг или счётчик,
    ///   некуда записать результат, старые поля блока, повтор значения в Switch; провод к порту, которого у ноды нет
    /// - на переменных граф-файла: объект сцены без «Set on Object», открытая Object без значения на объекте
    /// </summary>
    internal static class ScenarioGraphValidation
    {
        private const int MaxDepth = 8;

        private static readonly Dictionary<Type, List<FieldInfo>> SerializedFieldsByType = new Dictionary<Type, List<FieldInfo>>();


        /// <summary>Все проблемы графа. Граф-файл — свои правила переменных; привязки — объекта, через который файл открыт.</summary>
        public static List<ScenarioGraphProblem> Check(ScenarioGraph graph, bool isGraphFile = false, ScenarioBindingsEditing bindings = null)
        {
            var problems = new List<ScenarioGraphProblem>();

            if (graph == null)
                return problems;

            foreach (var node in graph.Nodes)
            {
                if (node == null)
                    continue;

                CheckFields(node, graph, node.Id, problems, 0);
                CheckSwitchCases(node, problems);
            }

            CheckWires(graph, problems);
            problems.AddRange(CheckVariables(graph, isGraphFile, bindings));

            return problems;
        }


        // ── Переменные граф-файла ────────────────────────────────────────

        /// <summary>Только проблемы переменных — для инспектора Scenario: ноды ему проверять незачем.</summary>
        public static List<ScenarioGraphProblem> CheckVariables(ScenarioGraph graph, bool isGraphFile, ScenarioBindingsEditing bindings)
        {
            var problems = new List<ScenarioGraphProblem>();

            if (graph == null || !isGraphFile)
                return problems;

            // Переменную, в которую граф пишет сам (For Each, Random Item…), заполнять снаружи не нужно.
            var writtenIds = ScenarioVariableWriters.WrittenVariableIds(graph);

            foreach (var variable in graph.Variables)
            {
                if (variable is { IsExposed: false, Kind: ScenarioVariableKind.Object or ScenarioVariableKind.ObjectList }
                    && ScenarioBindingsEditing.IsSceneType(variable.ObjectType) && !writtenIds.Contains(variable.Id))
                    problems.Add(new ScenarioGraphProblem(variable.Id,
                        $"“{variable.Name}” holds scene objects, but a graph file can't keep them — turn on Set on Object", true));
            }

            if (bindings == null)
                return problems;

            foreach (var variable in bindings.ObjectVariablesWithoutValue(graph))
                problems.Add(new ScenarioGraphProblem(variable.Id, $"“{variable.Name}” has no object on {bindings.Scenario.name}", false));

            return problems;
        }


        // ── Поля нод и блоков ────────────────────────────────────────────

        private static void CheckFields(object owner, ScenarioGraph graph, int nodeId, List<ScenarioGraphProblem> problems, int depth)
        {
            if (depth > MaxDepth)
                return;

            if (owner is IScenarioUpgradableBlock && ScenarioLegacyFieldsUpgrade.HasLegacyData(owner))
                problems.Add(new ScenarioGraphProblem(nodeId,
                    $"“{ScenarioBlockCatalog.TitleOf(owner.GetType())}” keeps fields of an old version — press {ScenarioLegacyFieldsUpgrade.ButtonText}",
                    false) { IsOldFields = true });

            foreach (var field in SerializedFieldsOf(owner.GetType()))
            {
                var value = field.GetValue(owner);
                var label = LabelOf(field);

                switch (value)
                {
                    case ScenarioFlagReference flagReference:
                        CheckReference(graph, nodeId, label, flagReference.VariableId, flagReference.Flag, typeof(bool), problems);
                        break;

                    case ScenarioCounterReference counterReference:
                        CheckReference(graph, nodeId, label, counterReference.VariableId, counterReference.Counter, typeof(int), problems);
                        break;

                    case ScenarioTarget target:
                        CheckTarget(target, graph, nodeId, label, problems);
                        break;

                    case ScenarioPoint point:
                        CheckPoint(point, graph, nodeId, label, problems);
                        break;

                    case not null when IsGeneric(value.GetType(), typeof(ScenarioObjectList<>)):
                        CheckObjectList(value, graph, nodeId, label, problems);
                        break;

                    case not null when IsGeneric(value.GetType(), typeof(ScenarioVariableSlot<>)):
                        CheckSlot(value, graph, nodeId, label, problems);
                        break;

                    case IList list:
                        CheckList(list, field, label, graph, nodeId, problems, depth);
                        break;

                    case not null when IsValueField(value.GetType()):
                        CheckValueField(value, graph, nodeId, label, AllowsEmpty(field), problems);
                        break;

                    case not null when IsNestedData(value.GetType()):
                        CheckFields(value, graph, nodeId, problems, depth + 1);
                        break;
                }
            }
        }

        private static void CheckList(IList list, FieldInfo field, string label, ScenarioGraph graph, int nodeId,
            List<ScenarioGraphProblem> problems, int depth)
        {
            var holdsBlocks = field.GetCustomAttribute<SerializeReference>() != null;

            foreach (var item in list)
            {
                if (item == null && holdsBlocks)
                    problems.Add(new ScenarioGraphProblem(nodeId, $"A block in “{label}” is empty or its class was renamed or removed", true));
                else if (item is ScenarioWaitCase { AwaitedEvent: null, Condition: null } waitCase)
                    problems.Add(new ScenarioGraphProblem(nodeId, $"Case “{waitCase.Label}” has no block: its class was renamed or removed", true));
                else if (item != null && (holdsBlocks || IsNestedData(item.GetType())))
                    CheckFields(item, graph, nodeId, problems, depth + 1);
            }
        }


        // ── Значение или переменная ──────────────────────────────────────

        private static bool IsValueField(Type type)
        {
            return IsGeneric(type, typeof(ScenarioValue<>));
        }

        private static void CheckValueField(object valueField, ScenarioGraph graph, int nodeId, string label, bool allowsEmpty,
            List<ScenarioGraphProblem> problems)
        {
            var type = valueField.GetType();
            var valueType = type.GetGenericArguments()[0];
            var variableId = (string)type.GetProperty(nameof(ScenarioValue<int>.VariableId))?.GetValue(valueField);

            if (!string.IsNullOrEmpty(variableId))
            {
                CheckVariable(graph, nodeId, label, variableId, valueType, problems);
                return;
            }

            if (!allowsEmpty && typeof(Object).IsAssignableFrom(valueType)
                             && type.GetProperty(nameof(ScenarioValue<int>.Value))?.GetValue(valueField) as Object == null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}” is empty", false));
        }

        // Договорённость FeatherKit: подсказка «Empty — …» значит, что у пустого поля есть смысл (камера, этот сценарий).
        private static bool AllowsEmpty(FieldInfo field)
        {
            return field.GetCustomAttribute<TooltipAttribute>()?.tooltip?.Contains("Empty —") == true;
        }

        private static void CheckReference(ScenarioGraph graph, int nodeId, string label, string variableId, Object asset, Type valueType,
            List<ScenarioGraphProblem> problems)
        {
            if (!string.IsNullOrEmpty(variableId))
                CheckVariable(graph, nodeId, label, variableId, valueType, problems);
            else if (asset == null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}” is not selected", false));
        }

        private static void CheckVariable(ScenarioGraph graph, int nodeId, string label, string variableId, Type valueType,
            List<ScenarioGraphProblem> problems)
        {
            var message = VariableProblemOf(graph.FindVariable(variableId), valueType);

            if (message != null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: {message}", true));
        }

        /// <summary>Что не так с переменной в поле этого типа. Null — подходит. Один текст и для значка, и для поля.</summary>
        public static string VariableProblemOf(ScenarioVariableDefinition variable, Type valueType)
        {
            if (variable == null)
                return "variable not found";

            if (ScenarioVariableKinds.Fits(variable, valueType))
                return null;

            if (!ScenarioVariableKinds.TryGetKind(valueType, out var neededKind))
                return $"this field can't use variables, but “{variable.Name}” is selected";

            return neededKind == variable.Kind
                ? $"“{variable.Name}” holds {variable.ObjectType.Name}, this field needs {valueType.Name}"
                : $"“{variable.Name}” is a {variable.Kind}, this field needs a {neededKind}";
        }


        private static void CheckSwitchCases(ScenarioNode node, List<ScenarioGraphProblem> problems)
        {
            if (node is ScenarioSwitchOnCounterNode switchNode && switchNode.TryFindRepeatedValue(out var repeatedValue))
                problems.Add(new ScenarioGraphProblem(node.Id, $"Value {repeatedValue} is in two cases — the lower one never fires", false));
        }


        // ── Списки и «куда записать» ─────────────────────────────────────

        private static void CheckObjectList(object listField, ScenarioGraph graph, int nodeId, string label, List<ScenarioGraphProblem> problems)
        {
            var type = listField.GetType();
            var variableId = (string)type.GetProperty(nameof(ScenarioObjectList<Object>.VariableId))?.GetValue(listField);

            if (!string.IsNullOrEmpty(variableId))
                CheckVariable(graph, nodeId, label, variableId, typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]), problems);
            else if (type.GetProperty(nameof(ScenarioObjectList<Object>.Items))?.GetValue(listField) is IList { Count: 0 })
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}” is empty", false));
        }

        private static void CheckSlot(object slot, ScenarioGraph graph, int nodeId, string label, List<ScenarioGraphProblem> problems)
        {
            var type = slot.GetType();
            var variableId = (string)type.GetProperty(nameof(ScenarioVariableSlot<int>.VariableId))?.GetValue(slot);

            if (string.IsNullOrEmpty(variableId))
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: nowhere to write — choose a variable", true));
            else
                CheckVariable(graph, nodeId, label, variableId, type.GetGenericArguments()[0], problems);
        }


        // ── Цель, точка и провода ────────────────────────────────────────

        private static void CheckTarget(ScenarioTarget target, ScenarioGraph graph, int nodeId, string label, List<ScenarioGraphProblem> problems)
        {
            var kind = (ScenarioTargetKind)ScenarioBlockStyle.FindField(typeof(ScenarioTarget), "kind").GetValue(target);
            var specificObject = ScenarioBlockStyle.FindField(typeof(ScenarioTarget), "specificObject").GetValue(target) as Object;

            if (kind == ScenarioTargetKind.Specific && specificObject == null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: no object selected", false));
            else if (kind == ScenarioTargetKind.Variable)
                CheckVariable(graph, nodeId, label, target.VariableId, typeof(GameObject), problems);
        }

        // Точке подходит и переменная-позиция, и объект с Transform: проверяем обе.
        private static void CheckPoint(ScenarioPoint point, ScenarioGraph graph, int nodeId, string label, List<ScenarioGraphProblem> problems)
        {
            if (point.Kind == ScenarioPointKind.Object && point.TargetTransform == null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: no object selected", false));

            if (point.Kind != ScenarioPointKind.Variable)
                return;

            var variable = graph.FindVariable(point.VariableId);

            if (variable == null)
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: variable not found", true));
            else if (!ScenarioVariableKinds.Fits(variable, typeof(Vector3)) && !ScenarioVariableKinds.Fits(variable, typeof(Transform)))
                problems.Add(new ScenarioGraphProblem(nodeId, $"“{label}”: “{variable.Name}” is a {variable.Kind}, a point needs an Object or a Vector3", true));
        }

        private static void CheckWires(ScenarioGraph graph, List<ScenarioGraphProblem> problems)
        {
            foreach (var wire in graph.Wires)
            {
                var fromNode = graph.FindNode(wire.FromNodeId);
                var toNode = graph.FindNode(wire.ToNodeId);

                if (fromNode != null && fromNode.CollectPorts().FindOutput(wire.FromPortId) == null)
                    problems.Add(new ScenarioGraphProblem(fromNode.Id, $"A wire starts at output “{wire.FromPortId}”, which the node no longer has", true));

                if (toNode != null && toNode.CollectPorts().FindInput(wire.ToPortId) == null)
                    problems.Add(new ScenarioGraphProblem(toNode.Id, $"A wire ends at input “{wire.ToPortId}”, which the node no longer has", true));
            }
        }


        // ── Общие помощники ──────────────────────────────────────────────

        public static List<FieldInfo> SerializedFieldsOf(Type type)
        {
            if (SerializedFieldsByType.TryGetValue(type, out var fields))
                return fields;

            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            fields = new List<FieldInfo>();

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(declared))
                {
                    var isSerialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null
                                                      || field.GetCustomAttribute<SerializeReference>() != null;

                    if (isSerialized && !field.IsNotSerialized)
                        fields.Add(field);
                }
            }

            SerializedFieldsByType[type] = fields;

            return fields;
        }

        public static bool IsGeneric(Type type, Type genericDefinition)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition;
        }

        public static bool IsNestedData(Type type)
        {
            return type.IsSerializable && !type.IsPrimitive && !type.IsEnum && type != typeof(string)
                   && !typeof(Object).IsAssignableFrom(type) && type.Namespace?.StartsWith("UnityEngine") != true;
        }

        private static string LabelOf(FieldInfo field)
        {
            return field.GetCustomAttribute<ScenarioLabelAttribute>()?.Text ?? ObjectNames.NicifyVariableName(field.Name);
        }
    }


    /// <summary>Одна проблема графа: на какой ноде или переменной, что сказать человеку и мешает ли она работе.</summary>
    internal class ScenarioGraphProblem
    {
        public ScenarioGraphProblem(int nodeId, string message, bool isError)
        {
            NodeId = nodeId;
            Message = message;
            IsError = isError;
        }

        public ScenarioGraphProblem(string variableId, string message, bool isError)
        {
            VariableId = variableId;
            Message = message;
            IsError = isError;
        }


        /// <summary>Нода с проблемой; 0 — проблема переменной.</summary>
        public int NodeId { get; }

        /// <summary>Переменная с проблемой; null — проблема ноды.</summary>
        public string VariableId { get; }

        public string Message { get; }

        /// <summary>Ошибка — граф так не сработает; иначе предупреждение.</summary>
        public bool IsError { get; }

        /// <summary>Блок хранит поля прежней версии — лечится кнопкой Upgrade Fields.</summary>
        public bool IsOldFields { get; set; }
    }
}
