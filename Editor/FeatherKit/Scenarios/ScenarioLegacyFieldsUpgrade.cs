using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// «Upgrade Fields» — старые поля блоков прежних версий в новые (IScenarioUpgradableBlock), только по команде человека.
    /// - найти блоки со старыми данными: в Scenario (и в старых списках), в граф-файле
    /// - перенести одной записью undo с пометкой «изменено»; для открытых сцен и префаба — пунктом меню
    /// Просмотр графа или инспектора ничего не переносит: открыть сцену и не поменять её — правило.
    /// </summary>
    internal static class ScenarioLegacyFieldsUpgrade
    {
        public const string ButtonText = "Upgrade Fields";

        private const string MenuPath = "FeatherKit/Scenarios/Upgrade Old Fields in Open Scenes";
        private const string UndoName = "Upgrade Scenario Fields";

        private static readonly Dictionary<System.Type, List<FieldInfo>> LegacyFieldsByType = new Dictionary<System.Type, List<FieldInfo>>();


        // ── Найти ────────────────────────────────────────────────────────

        /// <summary>Сколько блоков объекта (Scenario или граф-файла) хранят старые поля.</summary>
        public static int CountOutdatedBlocks(Object owner)
        {
            return owner != null ? OutdatedBlocksOf(new SerializedObject(owner)).Count : 0;
        }

        private static List<IScenarioUpgradableBlock> OutdatedBlocksOf(SerializedObject serializedOwner)
        {
            var blocks = new List<IScenarioUpgradableBlock>();
            var property = serializedOwner.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ManagedReference
                    && property.managedReferenceValue is IScenarioUpgradableBlock block && HasLegacyData(block) && !blocks.Contains(block))
                    blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// Старые поля — скрытые [SerializeField, HideInInspector] у такого блока; заданы — ссылка, непустой список
        /// или значение с IsDefault == false. Читаем, не вызывая перенос: он меняет данные.
        /// </summary>
        public static bool HasLegacyData(object block)
        {
            foreach (var field in LegacyFieldsOf(block.GetType()))
            {
                var value = field.GetValue(block);

                var isSet = value switch
                {
                    Object reference => reference != null,
                    IList list => list.Count > 0,
                    null => false,
                    _ => value.GetType().GetProperty("IsDefault", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetValue(value) is false
                };

                if (isSet)
                    return true;
            }

            return false;
        }

        private static List<FieldInfo> LegacyFieldsOf(System.Type blockType)
        {
            if (LegacyFieldsByType.TryGetValue(blockType, out var fields))
                return fields;

            fields = new List<FieldInfo>();

            for (var type = blockType; type != null && type != typeof(object); type = type.BaseType)
            {
                fields.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(field => field.GetCustomAttribute<SerializeField>() != null && field.GetCustomAttribute<HideInInspector>() != null));
            }

            LegacyFieldsByType[blockType] = fields;

            return fields;
        }


        // ── Перенести ────────────────────────────────────────────────────

        /// <summary>Перенести старые поля всех блоков объекта одной записью undo. Отдаёт, сколько блоков поменялось.</summary>
        public static int Upgrade(Object owner)
        {
            var blocks = OutdatedBlocksOf(new SerializedObject(owner));

            if (blocks.Count == 0)
                return 0;

            Undo.RecordObject(owner, UndoName);
            var upgradedCount = blocks.Count(block => block.TryUpgradeLegacyFields());

            if (PrefabUtility.IsPartOfPrefabInstance(owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);

            EditorUtility.SetDirty(owner);

            return upgradedCount;
        }


        [MenuItem(MenuPath)]
        private static void UpgradeOpen()
        {
            var owners = OpenOwners().Where(owner => CountOutdatedBlocks(owner) > 0).ToList();

            if (owners.Count == 0)
            {
                EditorUtility.DisplayDialog(ButtonText, "No blocks with old fields in the open scenes or prefab.", "OK");
                return;
            }

            var names = string.Join("\n", owners.Take(15).Select(owner => $"• {owner.name} — {PlaceOf(owner)}"));
            var sceneNames = owners.OfType<Component>().Select(component => component.gameObject.scene.name).Distinct().ToList();
            var scenesLine = sceneNames.Count > 1 ? $"\n\nScenes: {string.Join(", ", sceneNames)}" : "";
            var isConfirmed = EditorUtility.DisplayDialog(ButtonText,
                $"Move old fields to new ones in {owners.Count} scenarios and graph files?\n\n{names}{scenesLine}\n\nOne undo step brings them all back. "
                + "Save the scenes, prefab and files yourself.", ButtonText, "Cancel");

            if (!isConfirmed)
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var owner in owners)
                Upgrade(owner);

            Undo.SetCurrentGroupName(UndoName);
            Undo.CollapseUndoOperations(undoGroup);
        }

        // Где лежит: сцена у объекта, путь у граф-файла — два одноимённых объекта в разных сценах иначе не различить.
        private static string PlaceOf(Object owner)
        {
            return owner is Component component ? component.gameObject.scene.name : AssetDatabase.GetAssetPath(owner);
        }

        // Открыт префаб — только его содержимое; иначе все загруженные сцены. Плюс граф-файлы, которые они используют.
        private static IEnumerable<Object> OpenOwners()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var scenarios = prefabStage != null
                ? prefabStage.prefabContentsRoot.GetComponentsInChildren<Scenario>(true)
                : Object.FindObjectsByType<Scenario>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var graphFiles = scenarios.Where(scenario => scenario.GraphSource == ScenarioGraphSource.Asset && scenario.GraphAsset != null)
                .Select(scenario => (Object)scenario.GraphAsset);

            return scenarios.Cast<Object>().Concat(graphFiles.Distinct());
        }
    }
}
