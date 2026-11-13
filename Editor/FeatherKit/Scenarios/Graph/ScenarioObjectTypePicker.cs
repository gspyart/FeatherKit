using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Выбор типа для переменной Object с поиском: любой объект, GameObject, компоненты и ассеты по пространствам имён.
    /// Типы только игровые: редакторные в сборке игры не существуют.
    /// </summary>
    internal class ScenarioObjectTypePicker : AdvancedDropdown
    {
        private static readonly Vector2 WindowSize = new Vector2(280f, 360f);

        private readonly Action<Type> picked;


        public ScenarioObjectTypePicker(Action<Type> picked) : base(new AdvancedDropdownState())
        {
            this.picked = picked;
            minimumSize = WindowSize;
        }


        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Object Type");
            root.AddChild(new ObjectTypeItem("Any Object", typeof(Object)));
            root.AddChild(new ObjectTypeItem("GameObject", typeof(GameObject)));

            var components = new AdvancedDropdownItem("Components");
            var assets = new AdvancedDropdownItem("Assets");
            root.AddChild(components);
            root.AddChild(assets);

            var folders = new Dictionary<string, AdvancedDropdownItem>();

            foreach (var type in RuntimeObjectTypes())
            {
                var section = typeof(Component).IsAssignableFrom(type) ? components : assets;
                var folderName = type.Namespace?.Split('.')[0] ?? "Global";
                var folderKey = $"{section.name}/{folderName}";

                if (!folders.TryGetValue(folderKey, out var folder))
                {
                    folder = new AdvancedDropdownItem(folderName);
                    folders[folderKey] = folder;
                    section.AddChild(folder);
                }

                folder.AddChild(new ObjectTypeItem(type.Name, type));
            }

            return root;
        }

        private static IEnumerable<Type> RuntimeObjectTypes()
        {
            return TypeCache.GetTypesDerivedFrom<Object>()
                .Where(type => type.IsPublic && !type.IsGenericType && type != typeof(GameObject)
                               && !type.Assembly.GetName().Name.Contains("Editor"))
                .OrderBy(type => type.Namespace?.Split('.')[0] ?? "")
                .ThenBy(type => type.Name);
        }


        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is ObjectTypeItem typeItem)
                picked(typeItem.ObjectType);
        }


        /// <summary>Строка выбора, за которой стоит тип объекта.</summary>
        private class ObjectTypeItem : AdvancedDropdownItem
        {
            public ObjectTypeItem(string title, Type objectType) : base(title)
            {
                ObjectType = objectType;
            }


            public Type ObjectType { get; }
        }
    }
}
