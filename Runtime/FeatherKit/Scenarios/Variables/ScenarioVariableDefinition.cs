using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Переменная графа: Flag, Counter, Number или Object — у каждого экземпляра графа своя.
    /// - постоянный id: по нему её находят поля нод и привязки, имя можно менять
    /// - открытая — значение задаётся на каждом объекте в привязках Scenario
    /// </summary>
    [Serializable]
    public class ScenarioVariableDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string variableName;
        [SerializeField] private ScenarioVariableKind kind;

        [Tooltip("For Object variables: which component or asset type fits. Empty — any object")]
        [SerializeField] private string objectTypeName;

        [Tooltip("Set on Object: each Scenario using this graph gives its own value in its bindings")]
        [SerializeField] private bool isExposed;

        [Tooltip("Kept in checkpoints and saves")]
        [SerializeField] private bool isSaved;

        [SerializeField] private ScenarioVariableValue initialValue = new ScenarioVariableValue();

        // Тип по имени ищется отражением — помним последний; имя могли поменять через SerializedProperty.
        [NonSerialized] private string cachedObjectTypeName;
        [NonSerialized] private Type cachedObjectType;


        public ScenarioVariableDefinition(string variableName, ScenarioVariableKind kind)
        {
            id = CreateId();
            this.variableName = variableName;
            this.kind = kind;
        }


        public string Id => id;

        public string Name
        {
            get => variableName;
            set => variableName = value;
        }

        public ScenarioVariableKind Kind => kind;

        /// <summary>Какой тип подходит переменной Object. Тип не найден или не задан — любой объект.</summary>
        public Type ObjectType
        {
            get
            {
                if (cachedObjectType == null || cachedObjectTypeName != objectTypeName)
                {
                    var type = string.IsNullOrEmpty(objectTypeName) ? null : Type.GetType(objectTypeName);
                    cachedObjectType = type ?? typeof(Object);
                    cachedObjectTypeName = objectTypeName;
                }

                return cachedObjectType;
            }
            set => objectTypeName = value != null && value != typeof(Object) ? StoredTypeNameOf(value) : null;
        }

        public bool IsExposed
        {
            get => isExposed;
            set => isExposed = value;
        }

        public bool IsSaved
        {
            get => isSaved;
            set => isSaved = value;
        }

        /// <summary>Значение на старте экземпляра графа, если привязка не задала своё.</summary>
        public ScenarioVariableValue InitialValue => initialValue;


        /// <summary>«Полное.Имя, Сборка» без версии: обновление Unity или пакета не теряет тип.</summary>
        public static string StoredTypeNameOf(Type type)
        {
            return $"{type.FullName}, {type.Assembly.GetName().Name}";
        }


        /// <summary>Id новой переменной — один формат и для редактора, и для сборки кодом.</summary>
        public static string CreateId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
