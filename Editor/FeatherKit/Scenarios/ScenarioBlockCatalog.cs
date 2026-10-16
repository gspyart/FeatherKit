using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Какие блоки сценария есть в проекте — и в FeatherKit, и в игре.
    /// Отдаёт их путь в меню «+» и название; ищет один раз за перезагрузку домена.
    /// </summary>
    internal static class ScenarioBlockCatalog
    {
        private static readonly Dictionary<Type, List<Type>> BlockTypesByBaseType = new Dictionary<Type, List<Type>>();


        public static IReadOnlyList<Type> BlockTypesOf(Type baseType)
        {
            if (BlockTypesByBaseType.TryGetValue(baseType, out var blockTypes))
                return blockTypes;

            // [Serializable] обязателен: без него SerializeReference блок не сохранит.
            blockTypes = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.IsSerializable
                               && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(MenuPathOf)
                .ToList();

            BlockTypesByBaseType[baseType] = blockTypes;

            return blockTypes;
        }

        public static string MenuPathOf(Type blockType)
        {
            return blockType.GetCustomAttribute<ScenarioBlockAttribute>()?.MenuPath
                   ?? ObjectNames.NicifyVariableName(blockType.Name);
        }

        public static string TitleOf(Type blockType)
        {
            return blockType.GetCustomAttribute<ScenarioBlockAttribute>()?.Title
                   ?? ObjectNames.NicifyVariableName(blockType.Name);
        }
    }
}
