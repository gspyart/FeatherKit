using System;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно выбора блока сценария с поиском — как Add Component: разделы, поле поиска, стрелки и Enter.
    /// Разделы — из пути блока в ScenarioBlockAttribute; рядом с названием — имя класса, искать можно и по нему.
    /// </summary>
    internal class ScenarioBlockPicker : AdvancedDropdown
    {
        private static readonly Vector2 WindowSize = new Vector2(280f, 340f);

        private readonly Type blockBaseType;
        private readonly Action<Type> picked;


        public ScenarioBlockPicker(Type blockBaseType, Action<Type> picked) : base(new AdvancedDropdownState())
        {
            this.blockBaseType = blockBaseType;
            this.picked = picked;
            minimumSize = WindowSize;
        }


        public static string KindTitleOf(Type blockBaseType)
        {
            if (blockBaseType == typeof(ScenarioEvent))
                return "Когда";

            return blockBaseType == typeof(ScenarioCondition) ? "Если" : "Что сделать";
        }


        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(KindTitleOf(blockBaseType));
            var folders = new Dictionary<string, AdvancedDropdownItem>();

            foreach (var blockType in ScenarioBlockCatalog.BlockTypesOf(blockBaseType))
            {
                var path = ScenarioBlockCatalog.MenuPathOf(blockType).Split('/');
                var parent = root;

                for (var depth = 0; depth < path.Length - 1; depth++)
                    parent = FolderOf(folders, parent, string.Join("/", path, 0, depth + 1), path[depth]);

                parent.AddChild(new BlockItem($"{path[path.Length - 1]} ({blockType.Name})", blockType));
            }

            return root;
        }

        private static AdvancedDropdownItem FolderOf(Dictionary<string, AdvancedDropdownItem> folders,
            AdvancedDropdownItem parent, string folderPath, string folderName)
        {
            if (folders.TryGetValue(folderPath, out var folder))
                return folder;

            folder = new AdvancedDropdownItem(folderName);
            folders[folderPath] = folder;
            parent.AddChild(folder);

            return folder;
        }


        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is BlockItem blockItem)
                picked(blockItem.BlockType);
        }


        /// <summary>Строка окна выбора, за которой стоит класс блока.</summary>
        private class BlockItem : AdvancedDropdownItem
        {
            public BlockItem(string title, Type blockType) : base(title)
            {
                BlockType = blockType;
            }


            public Type BlockType { get; }
        }
    }
}
