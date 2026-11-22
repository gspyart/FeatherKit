using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Комната в объекте из коробок: пустота (вырез внутреннего объёма) плюс оболочка вокруг — стены, пол, потолок, каждое включается отдельно.
    /// - своя толщина стен и перекрытий, свои материалы пола, стен и потолка изнутри
    /// - соседние комнаты делят одну стену; в доме из коробок оболочка сливается с его объёмом; в здании стены на его стенах не строятся
    /// - опора — угол пола с наименьшими координатами, размер — внутренний; в игру не попадает — остаётся собранный меш
    /// </summary>
    [FDescription("Комната: пустота плюс стены, пол и потолок вокруг, каждое — своим переключателем. Поменял число — перестроилась.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Room")]
    [DisallowMultipleComponent]
    public class BoxModelRoom : MonoBehaviour
    {
        [Header("Size")]
        [Tooltip("Внутренний размер комнаты, м: ширина по X, высота от пола до потолка, глубина по Z. Высота на этаж 4 м — 3,8.")]
        [SerializeField] private Vector3 size = new(4f, 3.8f, 4f);

        [Space]
        [Header("Shell")]
        [Tooltip("Стены вокруг комнаты. Нет — видна только пустота: в доме из коробок стены — то, что осталось от его объёма.")]
        [SerializeField] private bool hasWalls = true;

        [Tooltip("Толщина стен вокруг, м. Наружная — 0,3, перегородка — 0,12.")]
        [Min(0.02f)]
        [SerializeField] private float wallThickness = 0.3f;

        [Tooltip("Толщина пола и потолка, м. Перекрытие — 0,2.")]
        [Min(0.02f)]
        [SerializeField] private float slabThickness = 0.2f;

        [Tooltip("Плита пола под комнатой. Нет — комната стоит прямо на том, что под ней: в здании — на его перекрытии.")]
        [SerializeField] private bool hasFloor = true;

        [Tooltip("Плита потолка над комнатой. Нет — сверху открыто: двор, крыша без парапета, в здании — его перекрытие.")]
        [SerializeField] private bool hasCeiling = true;

        [Space]
        [Header("Materials")]
        [Tooltip("Пол комнаты изнутри. Пусто — материал объекта.")]
        [SerializeField] private Material floorMaterial;

        [Tooltip("Стены комнаты изнутри. Пусто — материал объекта.")]
        [SerializeField] private Material wallMaterial;

        [Tooltip("Потолок комнаты изнутри. Пусто — материал объекта.")]
        [SerializeField] private Material ceilingMaterial;

        [Tooltip("Сдвиг, масштаб и поворот текстуры изнутри: стены, пол, потолок. Нет строки — как есть.")]
        [SerializeField] private LiveBuilderTextureFitList<BoxModelRoomTextureSurface> textureFits = new();


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

        public bool HasFloor
        {
            get => hasFloor;
            set => hasFloor = value;
        }

        public bool HasCeiling
        {
            get => hasCeiling;
            set => hasCeiling = value;
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

        public LiveBuilderTextureFitList<BoxModelRoomTextureSurface> TextureFits => textureFits ??= new LiveBuilderTextureFitList<BoxModelRoomTextureSurface>();
    }
}
