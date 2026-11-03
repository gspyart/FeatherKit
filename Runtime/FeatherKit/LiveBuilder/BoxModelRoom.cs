using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Комната в объекте из коробок со своими настройками: размер, толщина стен и перекрытий, материалы пола, стен и потолка.
    /// Поменял число — комната перестраивается. Внутри комната — вырезающая коробка; «стены вокруг» — ещё и добавляющая коробка
    /// на толщину стен и перекрытий снаружи: так комната стоит сама по себе, в пустоте. Внутри дома стены вокруг не нужны —
    /// стены — то, что осталось от его объёма. Опора — угол пола комнаты с наименьшими координатами, размер — внутренний.
    /// В игру не попадает — остаётся собранный меш.
    /// </summary>
    [FDescription("Комната: внутренний размер, стены и перекрытия, материалы пола, стен и потолка. Поменял — перестроилась. " +
                  "Опора — угол пола с наименьшими координатами.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Room")]
    [DisallowMultipleComponent]
    public class BoxModelRoom : MonoBehaviour
    {
        [Header("Размер")]
        [Tooltip("Внутренний размер комнаты, м: ширина по X, высота от пола до потолка, глубина по Z. Высота на этаж 4 м — 3,8.")]
        [SerializeField] private Vector3 size = new(4f, 3.8f, 4f);

        [Space]
        [Header("Стены вокруг")]
        [Tooltip("Добавить стены, пол и потолок снаружи — комната стоит сама по себе. Внутри дома выключено: стены — его объём.")]
        [SerializeField] private bool hasWalls = true;

        [Tooltip("Толщина стен вокруг, м. Наружная — 0,3.")]
        [Min(0.02f)]
        [SerializeField] private float wallThickness = 0.3f;

        [Tooltip("Толщина пола и потолка вокруг, м. Перекрытие — 0,2.")]
        [Min(0.02f)]
        [SerializeField] private float slabThickness = 0.2f;

        [Space]
        [Header("Материалы")]
        [Tooltip("Пол комнаты. Пусто — материал объекта.")]
        [SerializeField] private Material floorMaterial;

        [Tooltip("Стены комнаты изнутри. Пусто — материал объекта.")]
        [SerializeField] private Material wallMaterial;

        [Tooltip("Потолок комнаты. Пусто — материал объекта.")]
        [SerializeField] private Material ceilingMaterial;


        public Vector3 Size
        {
            get => size;
            set => size = Vector3.Max(value, Vector3.one * 0.1f);
        }

        public bool HasWalls
        {
            get => hasWalls;
            set => hasWalls = value;
        }

        public float WallThickness
        {
            get => wallThickness;
            set => wallThickness = Mathf.Max(0.02f, value);
        }

        public float SlabThickness
        {
            get => slabThickness;
            set => slabThickness = Mathf.Max(0.02f, value);
        }

        public Material FloorMaterial
        {
            get => floorMaterial;
            set => floorMaterial = value;
        }

        public Material WallMaterial
        {
            get => wallMaterial;
            set => wallMaterial = value;
        }

        public Material CeilingMaterial
        {
            get => ceilingMaterial;
            set => ceilingMaterial = value;
        }
    }
}
