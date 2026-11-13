using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Постоянные порты типа ноды — её статические поля ScenarioInput и ScenarioOutput.
    /// - ищет их отражением один раз на тип и помнит до перезагрузки домена
    /// - порядок: сначала порты базовых классов, внутри класса — как объявлены в коде
    /// </summary>
    public static class ScenarioNodePorts
    {
        private static readonly Dictionary<Type, ScenarioInput[]> InputsByNodeType = new Dictionary<Type, ScenarioInput[]>();
        private static readonly Dictionary<Type, ScenarioOutput[]> OutputsByNodeType = new Dictionary<Type, ScenarioOutput[]>();


        public static IReadOnlyList<ScenarioInput> InputsOf(Type nodeType)
        {
            CacheIfMissing(nodeType);

            return InputsByNodeType[nodeType];
        }

        public static IReadOnlyList<ScenarioOutput> OutputsOf(Type nodeType)
        {
            CacheIfMissing(nodeType);

            return OutputsByNodeType[nodeType];
        }


        /// <summary>Добавить в список все постоянные порты типа — это и делает ScenarioNode.DescribePorts по умолчанию.</summary>
        public static void AddStaticPorts(Type nodeType, ScenarioPortList ports)
        {
            foreach (var input in InputsOf(nodeType))
                ports.AddInput(input);

            foreach (var output in OutputsOf(nodeType))
                ports.AddOutput(output);
        }


        // ── Кеш по типу ──────────────────────────────────────────────────

        private static void CacheIfMissing(Type nodeType)
        {
            if (nodeType == null)
                throw new ArgumentNullException(nameof(nodeType));

            if (InputsByNodeType.ContainsKey(nodeType))
                return;

            ReportInstancePortFields(nodeType);

            var portFields = PortFieldsOf(nodeType);

            InputsByNodeType[nodeType] = portFields
                .Where(field => field.FieldType == typeof(ScenarioInput))
                .Select(field => (ScenarioInput)field.GetValue(null))
                .Where(input => input != null)
                .ToArray();

            OutputsByNodeType[nodeType] = portFields
                .Where(field => field.FieldType == typeof(ScenarioOutput))
                .Select(field => (ScenarioOutput)field.GetValue(null))
                .Where(output => output != null)
                .ToArray();
        }

        // Порт полем экземпляра — забытый static: порт молча пропал бы с ноды.
        private static void ReportInstancePortFields(Type nodeType)
        {
            const BindingFlags declaredInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                                  | BindingFlags.DeclaredOnly;

            foreach (var field in TypesFromBase(nodeType).SelectMany(type => type.GetFields(declaredInstance)))
            {
                if (IsPortType(field.FieldType))
                    Debug.LogError($"Scenario node {nodeType.Name}: port field “{field.Name}” in {field.DeclaringType?.Name} "
                                   + "must be static readonly, otherwise the node does not show it");
            }
        }

        // Приватные статические поля базового класса через наследника не видны — обходим каждый класс сам.
        private static List<FieldInfo> PortFieldsOf(Type nodeType)
        {
            const BindingFlags declaredStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                                                | BindingFlags.DeclaredOnly;

            return TypesFromBase(nodeType)
                .SelectMany(type => type.GetFields(declaredStatic).OrderBy(field => field.MetadataToken))
                .ToList();
        }

        private static List<Type> TypesFromBase(Type nodeType)
        {
            var typesFromBase = new List<Type>();

            for (var type = nodeType; type != null && type != typeof(object); type = type.BaseType)
                typesFromBase.Insert(0, type);

            return typesFromBase;
        }

        private static bool IsPortType(Type type)
        {
            return type == typeof(ScenarioInput) || type == typeof(ScenarioOutput);
        }
    }
}
