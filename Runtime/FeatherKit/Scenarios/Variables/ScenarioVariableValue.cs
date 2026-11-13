using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значение переменной графа любого вида: начальное у переменной или заданное привязкой на объекте.
    /// Читается то поле, что подходит виду переменной; остальные молчат.
    /// </summary>
    [Serializable]
    public class ScenarioVariableValue
    {
        [SerializeField] private bool flagValue;
        [SerializeField] private int counterValue;
        [SerializeField] private float numberValue;
        [SerializeField] private Object objectValue;


        public bool FlagValue
        {
            get => flagValue;
            set => flagValue = value;
        }

        public int CounterValue
        {
            get => counterValue;
            set => counterValue = value;
        }

        public float NumberValue
        {
            get => numberValue;
            set => numberValue = value;
        }

        public Object ObjectValue
        {
            get => objectValue;
            set => objectValue = value;
        }
    }


    /// <summary>Вид переменной графа.</summary>
    public enum ScenarioVariableKind
    {
        [InspectorName("Flag")] Flag,
        [InspectorName("Counter")] Counter,
        [InspectorName("Number")] Number,
        [InspectorName("Object")] Object
    }
}
