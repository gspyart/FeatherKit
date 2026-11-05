using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Этажи уровня Live Builder в самой сцене: высота этажа и пол первого этажа. Одни для всех, кто открывает сцену, на любом компьютере:
    /// здания встают на эти этажи, рисование в пустоте идёт от их пола. Один на сцену; кладёт его Live Builder, когда правишь этажи на вкладке «Уровень».
    /// Какой этаж открыт сейчас и разрез — у каждого свои и хранятся не здесь.
    /// </summary>
    [FDescription("Этажи уровня Live Builder: высота этажа и пол первого этажа — одни для всех, кто открывает сцену. " +
                  "Удобнее править на вкладке «Уровень» окна Live Builder.")]
    [AddComponentMenu("FeatherKit/Live Builder/Level Storeys")]
    [DisallowMultipleComponent]
    public class LiveBuilderLevelStoreys : MonoBehaviour
    {
        // Ниже этажей не бывает: этажи уровня слиплись бы.
        public const float MinStoreyHeight = 0.5f;

        [Tooltip("От пола до пола следующего этажа, м. По метрикам — 4. Новые здания берут её же.")]
        [Min(MinStoreyHeight)]
        [SerializeField] private float storeyHeight = 4f;

        [Tooltip("Высота пола первого этажа в мире, м: от неё считаются этажи. Дом на цоколе 1 м — 1.")]
        [SerializeField] private float groundLevel;


        public float StoreyHeight
        {
            get => storeyHeight;
            set => storeyHeight = Mathf.Max(MinStoreyHeight, value);
        }

        public float GroundLevel
        {
            get => groundLevel;
            set => groundLevel = value;
        }
    }
}
