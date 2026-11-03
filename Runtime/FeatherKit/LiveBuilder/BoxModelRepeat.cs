using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Повтор внутри объекта из коробок: всё, что лежит в этой группе — коробки, лестницы, вложенные повторы, —
    /// считается ещё раз со сдвигом на шаг, столько раз, сколько копий. Копии не настоящие: поправил оригинал — поменялись все.
    /// Этаж вверх, ряд окон вдоль фасада, колонны. Исключение для одной копии — коробка ниже группы в списке: она главнее.
    /// </summary>
    [FDescription("Повтор: всё внутри группы повторяется со сдвигом на шаг. Копии считаются из оригинала — правишь один, меняются все. " +
                  "Исключение для одной копии — коробка ниже группы в списке объектов.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Repeat")]
    [DisallowMultipleComponent]
    public class BoxModelRepeat : MonoBehaviour
    {
        // Защита от случайных тысяч копий, которые повесят редактор.
        public const int MaxCount = 200;

        [Tooltip("Сколько всего раз, вместе с оригиналом. 1 — без повтора, 5 — оригинал и ещё 4 копии.")]
        [Range(1, MaxCount)]
        [SerializeField] private int count = 2;

        [Tooltip("Сдвиг каждой следующей копии в осях объекта, м. Этажи — (0, 4, 0), окна через 4 м вдоль фасада — (4, 0, 0).")]
        [SerializeField] private Vector3 step = new(0f, 4f, 0f);


        public int Count
        {
            get => count;
            set => count = Mathf.Clamp(value, 1, MaxCount);
        }

        public Vector3 Step
        {
            get => step;
            set => step = value;
        }
    }
}
