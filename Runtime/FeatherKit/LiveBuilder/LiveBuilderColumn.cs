using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Место текстуры на колонне: бок, верх, низ.</summary>
    public enum LiveBuilderColumnSurface
    {
        Sides,
        Top,
        Bottom
    }


    /// <summary>
    /// Колонна — цилиндр из 4–32 сторон со своим мешем: сечение — эллипс, вписанный в прямоугольник основания, высота — своя.
    /// - материалы бока, верха и низа, фаска кромок, подгонка текстуры, коллайдер — как у объекта из коробок
    /// - в вырезах не участвует: не режет и не режется; копии — повтором (<see cref="LiveBuilderArray"/>) в её же меше
    /// - меш собирает редактор, в игре компонент — только данные
    /// </summary>
    [FDescription("Колонна: цилиндр из 4–32 сторон, сечение — эллипс в прямоугольнике основания. Опора — угол основания с наименьшими координатами. " +
                  "Вырезы её не режут и сама она не режет. Ряд колонн — повтор Live Builder Array на ней или над ней.")]
    [AddComponentMenu("FeatherKit/Live Builder/Live Builder Column")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LiveBuilderColumn : MonoBehaviour
    {
        public const int MinSides = 4;
        public const int MaxSides = 32;

        [Header("Shape")]
        [Tooltip("Сколько сторон у сечения. 4 — квадратный столб, 8 — граненый, 16 и больше — круглый. С 12 сторон бок гладкий.")]
        [Range(MinSides, MaxSides)]
        [SerializeField] private int sides = 16;

        [Tooltip("Размер по осям колонны, м: X и Z — прямоугольник основания (сечение вписано в него), Y — высота.")]
        [SerializeField] private Vector3 size = new(0.4f, 3f, 0.4f);

        [Space]
        [Header("Materials")]
        [Tooltip("Материал бока и кромок фаски. Пусто — серый материал набора.")]
        [SerializeField] private Material material;

        [Tooltip("Материал верха. Пусто — как бок.")]
        [SerializeField] private Material topMaterial;

        [Tooltip("Материал низа. Пусто — как бок.")]
        [SerializeField] private Material bottomMaterial;

        [Space]
        [Header("Edge Bevel")]
        [Tooltip("Срез верхней и нижней кромки, м. 0 — кромки острые. Не больше половины высоты и радиуса.")]
        [Min(0f)]
        [SerializeField] private float bevelSize;

        [Tooltip("Из скольких кусков срез кромки. 1 — плоская фаска, 4 и больше — скругление.")]
        [Range(1, 8)]
        [SerializeField] private int bevelSegments = 1;

        [Space]
        [Header("Collisions")]
        [Tooltip("Собирать коллайдер по мешу — в колонну упираешься.")]
        [SerializeField] private bool hasCollider = true;

        [Space]
        [Header("Texture")]
        [Tooltip("World — текстура по миру: сдвинул колонну — рисунок по высоте остаётся на месте. Object — едет вместе с колонной.")]
        [SerializeField] private LiveBuilderTextureAlignment textureAlignment = LiveBuilderTextureAlignment.Object;

        [Tooltip("Сдвиг, масштаб и поворот текстуры: бок (U — по кругу, V — высота), верх, низ. Нет строки — как есть.")]
        [SerializeField] private LiveBuilderTextureFitList<LiveBuilderColumnSurface> textureFits = new();

        // Чем собран текущий меш: изменилось — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;


        public int Sides
        {
            get => sides;
            set => sides = Mathf.Clamp(value, MinSides, MaxSides);
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

        public Material TopMaterial
        {
            get => topMaterial;
            set => topMaterial = value;
        }

        public Material BottomMaterial
        {
            get => bottomMaterial;
            set => bottomMaterial = value;
        }

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

        public bool HasCollider
        {
            get => hasCollider;
            set => hasCollider = value;
        }

        public LiveBuilderTextureAlignment TextureAlignment
        {
            get => textureAlignment;
            set => textureAlignment = value;
        }

        public LiveBuilderTextureFitList<LiveBuilderColumnSurface> TextureFits => textureFits ??= new LiveBuilderTextureFitList<LiveBuilderColumnSurface>();

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }


        private void Reset()
        {
            textureAlignment = LiveBuilderTextureAlignment.World;
        }
    }
}
