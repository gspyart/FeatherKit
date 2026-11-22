using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Модуль — готовый префаб для стены (окно, дверь, балкон, батарея): как он встаёт на стену инструментом «Module».
    /// - опора префаба — низ посередине, +Z смотрит наружу, по толщине — середина стены
    /// - низ — на своей высоте над полом этажа; нет компонента — на полу
    /// </summary>
    [FDescription("Как префаб встаёт на стену инструментом Module: опора — низ посередине, +Z — наружу, низ — на этой высоте над полом этажа. " +
                  "Вырез внутри префаба с «Everything it touches» прорезает стену.")]
    [AddComponentMenu("FeatherKit/Live Builder/Live Builder Module")]
    [DisallowMultipleComponent]
    public class LiveBuilderModule : MonoBehaviour
    {
        [Tooltip("Высота низа модуля над полом этажа, м: 0 — дверь, около 0,9 — окно.")]
        [SerializeField] private float bottomAboveFloor;


        public float BottomAboveFloor
        {
            get => bottomAboveFloor;
            set => bottomAboveFloor = value;
        }
    }
}
