using System;
using System.Collections.Generic;
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
        [SerializeField] private Vector3 vectorValue;
        [SerializeField] private string textValue;
        [SerializeField] private Color colorValue = Color.white;
        [SerializeField] private List<Object> objectListValue = new List<Object>();


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

        public Vector3 VectorValue
        {
            get => vectorValue;
            set => vectorValue = value;
        }

        public string TextValue
        {
            get => textValue;
            set => textValue = value;
        }

        public Color ColorValue
        {
            get => colorValue;
            set => colorValue = value;
        }

        /// <summary>Список объектов: сам список, а не копия. Null не бывает — присвоение null даёт пустой список.</summary>
        public List<Object> ObjectListValue
        {
            get => objectListValue ??= new List<Object>();
            set => objectListValue = value ?? new List<Object>();
        }
    }


    /// <summary>Вид переменной графа. Новые виды — только в конец: в данных хранится число.</summary>
    public enum ScenarioVariableKind
    {
        [InspectorName("Flag")] Flag,
        [InspectorName("Counter")] Counter,
        [InspectorName("Number")] Number,
        [InspectorName("Object")] Object,
        [InspectorName("Vector3")] Vector3,
        [InspectorName("Text")] Text,
        [InspectorName("Color")] Color,
        [InspectorName("Object List")] ObjectList
    }
}
