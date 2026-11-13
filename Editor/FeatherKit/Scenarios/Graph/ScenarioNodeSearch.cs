using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поиск нод для холста — по «Create Node», пробелу и проводу, брошенному в пустоту.
    /// - заметка «Comment», When, Do, ноды потока, блоки-события и блоки-шаги по категориям ScenarioBlock
    /// - блок-событие даёт ноду When с ним, блок-шаг — Do с ним
    /// - тестовые блоки без пустого конструктора в поиск не попадают, как и в меню «+»
    /// </summary>
    internal class ScenarioNodeSearch : ScriptableObject, ISearchWindowProvider
    {
        private const string FlowFolder = "Flow";
        private const string EventsFolder = "Events";
        private const string StepsFolder = "Steps";

        private ScenarioNodeCreation nodeCreation;
        private Port draggedPort;


        // ── Что можно создать ────────────────────────────────────────────

        /// <summary>Все строки поиска по порядку: заметка, When и Do, потом ноды потока, события и шаги.</summary>
        public static List<ScenarioNodeSearchOption> CollectOptions()
        {
            var options = new List<ScenarioNodeSearchOption>
            {
                new ScenarioNodeSearchOption(new[] { "Comment" }, typeof(ScenarioNote)),
                new ScenarioNodeSearchOption(new[] { "When" }, typeof(ScenarioWhenNode)),
                new ScenarioNodeSearchOption(new[] { "Do" }, typeof(ScenarioStepsNode))
            };

            foreach (var nodeType in ScenarioBlockCatalog.BlockTypesOf(typeof(ScenarioNode)))
            {
                if (nodeType != typeof(ScenarioWhenNode) && nodeType != typeof(ScenarioStepsNode))
                    options.Add(new ScenarioNodeSearchOption(FlowPathOf(nodeType), nodeType));
            }

            options.AddRange(BlockOptions(EventsFolder, typeof(ScenarioEvent)));
            options.AddRange(BlockOptions(StepsFolder, typeof(ScenarioAction)));

            return options;
        }

        // Нода потока без своей папки ложится в «Flow»: «Flow/Wait For» и «Coin Flip» стоят рядом.
        private static string[] FlowPathOf(Type nodeType)
        {
            var path = ScenarioBlockCatalog.MenuPathOf(nodeType).Split('/');

            return path[0] == FlowFolder ? path : new[] { FlowFolder }.Concat(path).ToArray();
        }

        private static IEnumerable<ScenarioNodeSearchOption> BlockOptions(string folder, Type blockBaseType)
        {
            return ScenarioBlockCatalog.BlockTypesOf(blockBaseType)
                .Select(blockType => new ScenarioNodeSearchOption(
                    new[] { folder }.Concat(ScenarioBlockCatalog.MenuPathOf(blockType).Split('/')).ToArray(), blockType));
        }


        // ── Окно поиска ──────────────────────────────────────────────────

        /// <summary>Перед каждым открытием: кто создаёт ноду и порт, с которого бросили провод (или null).</summary>
        public void Prepare(ScenarioNodeCreation creation, Port portOfDroppedWire)
        {
            nodeCreation = creation;
            draggedPort = portOfDroppedWire;
        }


        // ISearchWindowProvider
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Create Node"), 0) };
            var openedFolders = new HashSet<string>();

            foreach (var option in CollectOptions())
            {
                for (var depth = 0; depth < option.Path.Length - 1; depth++)
                {
                    var folderPath = string.Join("/", option.Path, 0, depth + 1);

                    if (openedFolders.Add(folderPath))
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(option.Path[depth], FolderIconOf(option.Path[depth])), depth + 1));
                }

                tree.Add(new SearchTreeEntry(new GUIContent(option.Title, IconOf(option.CreatedType)))
                {
                    level = option.Path.Length,
                    userData = option.CreatedType
                });
            }

            return tree;
        }

        private static Texture FolderIconOf(string folder)
        {
            if (folder == EventsFolder)
                return ScenarioIcons.When;

            if (folder == FlowFolder || folder == StepsFolder)
                return ScenarioIcons.Flow;

            return ScenarioIcons.TypeIcon(ScenarioBlockStyle.IconTypeOfCategory(folder));
        }

        // Своей иконки у заметки нет — значок «i», чтобы строки поиска стояли ровно.
        private static Texture IconOf(Type createdType)
        {
            return createdType == typeof(ScenarioNote) ? ScenarioIcons.Info : ScenarioIcons.IconOf(createdType);
        }


        // ISearchWindowProvider
        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (nodeCreation == null || entry.userData is not Type createdType)
                return false;

            nodeCreation.CreateFromSearch(createdType, context.screenMousePosition, draggedPort);

            return true;
        }
    }


    /// <summary>Строка поиска нод: путь по папкам и тип — нода или блок, из которого нода соберётся.</summary>
    internal class ScenarioNodeSearchOption
    {
        public ScenarioNodeSearchOption(string[] path, Type createdType)
        {
            Path = path;
            CreatedType = createdType;
        }


        public string[] Path { get; }

        public Type CreatedType { get; }

        public string Title => Path[Path.Length - 1];
    }
}
