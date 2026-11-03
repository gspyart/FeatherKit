using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Объект из коробок — дом, стена, комната: один меш и один коллайдер, собранные из дочерних коробок <see cref="BoxModelPart"/>.
    /// Коробки добавляют объём или вырезают дыры; что ниже в списке объектов, то главнее. «Фаска рёбер» срезает или скругляет выпуклые рёбра готовой оболочки.
    /// Меш собирает редактор и хранит файлом рядом со сценой; в игре компонент — только данные, ничего не считает.
    /// </summary>
    [FDescription("Объект из коробок: дочерние коробки добавляют объём или вырезают дыры, редактор сам собирает из них один меш. " +
                  "Что ниже в списке объектов, то главнее. Рисовать и тянуть коробки — инструмент «Коробки» в окне сцены (клавиша B).")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BoxModel : MonoBehaviour
    {
        [Tooltip("Материал граней, если у коробки свой не задан.")]
        [SerializeField] private Material defaultMaterial;

        [Tooltip("Собирать коллайдер по мешу — по объекту можно ходить.")]
        [SerializeField] private bool hasCollider = true;

        [Space]
        [Header("Фаска рёбер")]
        [Tooltip("Срез всех выпуклых рёбер наружной оболочки, м: угол дома, край плиты, верх стены. 0 — рёбра острые. " +
                 "0,05 — заметная фаска на блокинге. Не больше половины самой тонкой грани рядом.")]
        [Min(0f)]
        [SerializeField] private float bevelSize;

        [Tooltip("Из скольких кусков срез ребра. 1 — плоская фаска под 45°, 4 и больше — скругление.")]
        [Range(1, 8)]
        [SerializeField] private int bevelSegments = 1;

        // Чем собран текущий меш: изменились коробки — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;


        public Material DefaultMaterial
        {
            get => defaultMaterial;
            set => defaultMaterial = value;
        }

        public bool HasCollider => hasCollider;

        public float BevelSize
        {
            get => bevelSize;
            set => bevelSize = Mathf.Max(0f, value);
        }

        public int BevelSegments
        {
            get => bevelSegments;
            set => bevelSegments = Mathf.Clamp(value, 1, 8);
        }

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }
    }
}
