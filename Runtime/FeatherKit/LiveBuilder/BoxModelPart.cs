using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Что коробка делает с объектом: добавляет объём или вырезает дыру.</summary>
    public enum BoxModelPartMode
    {
        [InspectorName("Добавить")] Add,
        [InspectorName("Вырезать")] Cut
    }


    /// <summary>
    /// Одна коробка объекта из коробок (<see cref="BoxModel"/>): добавляет объём или вырезает дыру.
    /// Опора — угол с наименьшими координатами по всем осям объекта, размер — в метрах. Коробки не поворачиваются: всегда по осям объекта.
    /// Материал можно задать всей коробке или отдельно низу, верху и бокам — у вырезающей это пол, потолок и стены дыры.
    /// В игре не нужна: объект коробки помечен EditorOnly и в сборку не попадает.
    /// </summary>
    [FDescription("Коробка объекта из коробок: добавляет объём или вырезает дыру. Опора — угол с наименьшими координатами, размер — в метрах. " +
                  "Поворачивать нельзя. В игру не попадает — остаётся только собранный меш.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Part")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BoxModelPart : MonoBehaviour
    {
        [Tooltip("Добавить объём или вырезать дыру.")]
        [SerializeField] private BoxModelPartMode mode = BoxModelPartMode.Add;

        [Tooltip("Размер коробки по осям объекта, м.")]
        [SerializeField] private Vector3 size = new(3f, 3f, 3f);

        [Header("Материалы")]
        [Tooltip("Материал граней этой коробки. Пусто — материал объекта. У вырезающей — материал стенок дыры.")]
        [SerializeField] private Material material;

        [Tooltip("Материал низа. У вырезающей коробки — пол комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material bottomMaterial;

        [Tooltip("Материал верха. У вырезающей коробки — потолок комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material topMaterial;

        [Tooltip("Материал четырёх боков. У вырезающей коробки — стены комнаты. Пусто — общий материал коробки.")]
        [SerializeField] private Material sideMaterial;


        public BoxModelPartMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public Vector3 Size
        {
            get => size;
            set => size = Vector3.Max(value, Vector3.zero);
        }

        public Material Material
        {
            get => material;
            set => material = value;
        }

        public Material BottomMaterial
        {
            get => bottomMaterial;
            set => bottomMaterial = value;
        }

        public Material TopMaterial
        {
            get => topMaterial;
            set => topMaterial = value;
        }

        public Material SideMaterial
        {
            get => sideMaterial;
            set => sideMaterial = value;
        }
    }
}
