using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LevelDesign
{
    /// <summary>Как разложены копии <see cref="ArrayModifier"/>: рядами по осям объекта или по кругу.</summary>
    public enum ArrayModifierLayout
    {
        [InspectorName("Grid")] Grid,
        [InspectorName("Circle")] Circle
    }


    /// <summary>
    /// Модификатор Array, как в Blender: этот объект — первая копия, остальные встают рядом по сетке или по кругу —
    /// окна вдоль фасада, колонны по кругу зала. Ставится из окна Feather Modifiers кнопкой «Array» на выделенный объект.
    /// Копии — соседи объекта в том же родителе: экземпляры его префаба с теми же правками, а у объекта без префаба — его клоны.
    /// Двигаешь или правишь объект — копии повторяют за ним; руками копии не правят. «Apply» снимает модификатор,
    /// копии остаются обычными объектами. В игре компонент ничего не делает — только хранит настройки и список копий.
    /// </summary>
    [FDescription("Array modifier: this object is the first copy, the others stand next to it in a grid or a circle. " +
                  "Edit or move this object and the copies follow. Apply keeps the copies as ordinary objects.")]
    [AddComponentMenu("FeatherKit/Modifiers/Array")]
    [DisallowMultipleComponent]
    public class ArrayModifier : MonoBehaviour
    {
        // Больше — уже не расстановка окон, а ошибка в числе: редактор не станет плодить тысячи объектов.
        public const int MaxCopies = 1000;

        [Tooltip("Grid — rows along this object's X, Y and Z. Circle — copies around a center, this object is one of them")]
        [SerializeField] private ArrayModifierLayout layout = ArrayModifierLayout.Grid;

        [Tooltip("How many copies along X, Y and Z, counting this object. 6 × 5 × 1 — six windows on five floors")]
        [SerializeField] private Vector3Int counts = new(3, 1, 1);

        [Tooltip("On — the step counts from the object's edge: 0 — copies touch. Off — from one copy's pivot to the next")]
        [SerializeField] private bool stepsFromSize;

        [Tooltip("Step between copies along X, Y and Z, m")]
        [SerializeField] private Vector3 step = new(2f, 4f, 2f);

        [Tooltip("Gap between copies along X, Y and Z, m: 0 — touching, negative — overlapping")]
        [SerializeField] private Vector3 gap;

        [Tooltip("How many copies around the circle, counting this object")]
        [Min(1)]
        [SerializeField] private int circleCount = 8;

        [Tooltip("Distance from the center to each copy, m")]
        [Min(0f)]
        [SerializeField] private float radius = 5f;

        [Tooltip("Part of the circle to fill, degrees. 360 — the whole circle, 180 — half of it")]
        [Range(1f, 360f)]
        [SerializeField] private float arc = 360f;

        [Tooltip("Turn each copy to face the center, like chairs around a table. Off — all copies face the same way")]
        [SerializeField] private bool facesCenter = true;

        [Tooltip("Copy numbers to leave out: 2 — the copy next to this object. For example, a door instead of the third window")]
        [SerializeField] private List<int> skippedCopies = new();

        // Копии, которые собрал редактор, и чем они собраны: изменилось — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private List<GameObject> copies = new();
        [SerializeField, HideInInspector] private string builtSignature;


        public ArrayModifierLayout Layout
        {
            get => layout;
            set => layout = value;
        }

        // Не меньше одной копии по каждой оси.
        public Vector3Int Counts
        {
            get => Vector3Int.Max(counts, Vector3Int.one);
            set => counts = Vector3Int.Max(value, Vector3Int.one);
        }

        public bool StepsFromSize
        {
            get => stepsFromSize;
            set => stepsFromSize = value;
        }

        public Vector3 Step
        {
            get => step;
            set => step = value;
        }

        public Vector3 Gap
        {
            get => gap;
            set => gap = value;
        }

        public int CircleCount
        {
            get => Mathf.Max(1, circleCount);
            set => circleCount = Mathf.Max(1, value);
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public float Arc
        {
            get => Mathf.Clamp(arc, 1f, 360f);
            set => arc = Mathf.Clamp(value, 1f, 360f);
        }

        public bool FacesCenter
        {
            get => facesCenter;
            set => facesCenter = value;
        }

        public List<int> SkippedCopies => skippedCopies;

        public List<GameObject> Copies => copies;

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }
    }
}
