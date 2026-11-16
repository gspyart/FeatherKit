using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Где лежит открытый в редакторе граф: в компоненте Scenario или в граф-файле.
    /// - отдаёт граф, объект Unity для undo, его SerializedObject и пути к нодам и переменным
    /// - граф-файл, открытый через объект, — ещё и привязки этого объекта
    /// - знает, есть ли несохранённое, и сохраняет туда, где граф живёт: сцена, префаб или файл
    /// Остальной редактор графа про разницу не знает.
    /// </summary>
    internal class ScenarioGraphHost
    {
        // У Scenario и у ScenarioGraphAsset поле графа называется одинаково.
        public const string GraphPropertyPath = "graph";

        private const string NodesPropertyName = "nodes";
        private const string VariablesPropertyName = "variables";


        private ScenarioGraphHost(Object owner, Scenario scenario)
        {
            Owner = owner;
            Scenario = scenario;
            SerializedGraph = new SerializedObject(owner);

            if (owner is ScenarioGraphAsset && scenario != null)
                Bindings = new ScenarioBindingsEditing(new SerializedObject(scenario));
        }


        /// <summary>Объект Unity, в котором лежит граф: компонент Scenario или ScenarioGraphAsset.</summary>
        public Object Owner { get; }

        /// <summary>Сценарий, из которого граф открыт. Null — открыт сам граф-файл.</summary>
        public Scenario Scenario { get; }

        public SerializedObject SerializedGraph { get; }

        /// <summary>Привязки объекта, через который открыт граф-файл. Null — открыт встроенный граф или сам файл.</summary>
        public ScenarioBindingsEditing Bindings { get; }

        public bool IsGraphFile => Owner is ScenarioGraphAsset;

        /// <summary>Граф ещё на месте: объект не удалён, сцену не закрыли.</summary>
        public bool IsAlive => Owner != null;

        public ScenarioGraph Graph => Owner switch
        {
            ScenarioGraphAsset asset => asset.Graph,
            Scenario scenario => scenario.Graph,
            _ => null
        };

        /// <summary>Объекты сцены можно класть прямо в поля нод только во встроенном графе.</summary>
        public bool CanReferenceSceneObjects => !IsGraphFile && !EditorUtility.IsPersistent(Owner);

        public string DisplayName
        {
            get
            {
                if (!IsAlive)
                    return "Graph is gone";

                if (IsGraphFile)
                    return Scenario != null ? $"{Owner.name} (Graph File) · on {Scenario.name}" : $"{Owner.name} (Graph File)";

                var scene = Scenario.gameObject.scene;

                return scene.IsValid() ? $"{Scenario.name} (In This Object) · {scene.name}" : $"{Scenario.name} (In This Object)";
            }
        }

        /// <summary>Есть несохранённые правки там, где граф лежит: в сцене, в открытом префабе или в файле.</summary>
        public bool IsDirty
        {
            get
            {
                if (!IsAlive)
                    return false;

                // Граф-файл, собранный кодом и ещё не сохранённый, — не компонент, но и не на диске.
                if (EditorUtility.IsPersistent(Owner) || Owner is not Component component)
                    return EditorUtility.IsDirty(Owner);

                var gameObject = component.gameObject;
                var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);

                return prefabStage != null ? prefabStage.scene.isDirty : gameObject.scene.isDirty;
            }
        }

        public SerializedProperty NodesProperty => SerializedGraph.FindProperty($"{GraphPropertyPath}.{NodesPropertyName}");

        public SerializedProperty VariablesProperty => SerializedGraph.FindProperty($"{GraphPropertyPath}.{VariablesPropertyName}");


        // ── Открыть ──────────────────────────────────────────────────────

        /// <summary>Граф объекта: Scenario (или его GameObject) либо граф-файл. Null — графа нет: файл у Scenario не выбран.</summary>
        public static ScenarioGraphHost For(Object target)
        {
            var owner = GraphOwnerOf(target);

            return owner != null ? new ScenarioGraphHost(owner, ScenarioOf(target)) : null;
        }

        /// <summary>Где сейчас лежит граф объекта — без SerializedObject, для частой проверки «не сменил ли сценарий источник».</summary>
        public static Object GraphOwnerOf(Object target)
        {
            // Сравнение Unity: GetComponent в редакторе отдаёт не C#-null, а «пустышку».
            if (target == null)
                return null;

            return target switch
            {
                ScenarioGraphAsset asset => asset,
                GameObject gameObject => GraphOwnerOf(gameObject.GetComponent<Scenario>()),
                Scenario { GraphSource: ScenarioGraphSource.Asset } scenario => scenario.GraphAsset,
                Scenario scenario => scenario,
                _ => null
            };
        }

        private static Scenario ScenarioOf(Object target)
        {
            return target is GameObject gameObject ? gameObject.GetComponent<Scenario>() : target as Scenario;
        }


        // ── Пути в SerializedObject ──────────────────────────────────────

        /// <summary>Нода в SerializedObject по её месту в списке нод графа.</summary>
        public SerializedProperty NodePropertyAt(int index)
        {
            var nodes = NodesProperty;

            return index >= 0 && index < nodes.arraySize ? nodes.GetArrayElementAtIndex(index) : null;
        }

        public SerializedProperty NodeProperty(int nodeId)
        {
            var nodes = Graph.Nodes;

            for (var index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] != null && nodes[index].Id == nodeId)
                    return NodePropertyAt(index);
            }

            return null;
        }

        public SerializedProperty VariableProperty(string variableId)
        {
            var variables = Graph.Variables;

            for (var index = 0; index < variables.Count; index++)
            {
                if (variables[index] != null && variables[index].Id == variableId)
                    return VariablesProperty.GetArrayElementAtIndex(index);
            }

            return null;
        }


        // ── Сохранить ────────────────────────────────────────────────────

        /// <summary>Сохранить туда, где граф лежит: файл или префаб — на диск, встроенный — его сцену или открытый префаб.</summary>
        public void Save()
        {
            if (!IsAlive)
                return;

            if (EditorUtility.IsPersistent(Owner))
            {
                AssetDatabase.SaveAssetIfDirty(Owner);
                return;
            }

            // Граф-файл не на диске сохранять некуда — его создатель решает, куда.
            if (Owner is not Component component)
                return;

            var gameObject = component.gameObject;
            var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);

            if (prefabStage != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
                prefabStage.ClearDirtiness();
                return;
            }

            EditorSceneManager.SaveScene(gameObject.scene);
        }
    }
}
