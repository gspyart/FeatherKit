using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Где стена стоит относительно линии: линия по оси стены (посередине), по её левому или правому краю.</summary>
    public enum SplineWallAlignment
    {
        [InspectorName("Center")] Center,
        [InspectorName("Left of Line")] Left,
        [InspectorName("Right of Line")] Right
    }


    /// <summary>
    /// Стена по линии: сплайн на этом же объекте — путь стены, у точки свой режим — угол или плавный изгиб.
    /// Толщина и высота — здесь, а с типом (<see cref="SplineWallType"/>) толщина и материал — из типа; на этажах выше — повтор (<see cref="LiveBuilderArray"/>).
    /// Замкнутая стена в здании может быть комнатой: стороны вдоль стен здания не строятся, но остаются в линии.
    /// У замкнутой стены могут быть свои пол и потолок — плиты по внутренней стороне, своими материалами.
    /// Точки перегородки, лежащие на стене здания, держатся за неё: двигаешь стену здания — точки едут следом.
    /// Проёмы — вырезы (<see cref="GeometryCutter"/>), обычно её дети, и вырезающие коробки объекта из коробок, в котором лежит стена, — насквозь.
    /// Меш собирает редактор и хранит файлом рядом со сценой; в игре компонент — только данные.
    /// </summary>
    [FDescription("Стена по линии: путь — сплайн на этом объекте, у точки — угол или плавно. Проёмы — компонент «Geometry Cutter» " +
                  "и вырезающие коробки объекта из коробок, в котором лежит стена.")]
    [AddComponentMenu("FeatherKit/Live Builder/Spline Wall")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class SplineWall : MonoBehaviour
    {
        [Header("Size")]
        [Tooltip("Тип стены: толщина и материал — из него, общие со всеми стенами этого типа. Пусто — свои ниже.")]
        [SerializeField] private SplineWallType wallType;

        [Tooltip("Толщина стены, м. Наружная — 0,3, перегородка — 0,15. С типом — из типа.")]
        [Min(0.02f)]
        [SerializeField] private float thickness = 0.3f;

        [Tooltip("Высота стены от линии вверх, м. Стена на этаж — 3,8: от пола до потолка.")]
        [Min(0.05f)]
        [SerializeField] private float height = 3.8f;

        [Tooltip("Где стена относительно линии: посередине или целиком с одной стороны — удобно обводить по краю пола.")]
        [SerializeField] private SplineWallAlignment alignment = SplineWallAlignment.Center;

        [Tooltip("Комната в здании: стороны, что легли на стены здания, не строятся — там уже его стена. Точки остаются: " +
                 "поменял контур комнаты или здания — какие стороны строить, пересчитается само. Работает в привязке к зданию.")]
        [SerializeField] private bool skipsSidesAlongBuildingWalls;

        [Tooltip("Докуда стена в здании. До потолка — этаж минус перекрытие. Весь этаж — сквозь перекрытие до пола следующего: " +
                 "шахта лифта без щелей между этажами. Своя — высота выше. Работает в привязке к зданию.")]
        [SerializeField] private SplineWallHeightInBuilding heightInBuilding = SplineWallHeightInBuilding.ToCeiling;

        [Space]
        [Header("Floor and Ceiling")]
        [Tooltip("Плита пола внутри замкнутой стены, на её низу. В здании — свой пол комнаты поверх пола здания, своим материалом. У открытой линии пола нет.")]
        [SerializeField] private bool hasFloor;

        [Tooltip("Плита потолка внутри замкнутой стены, под её верхом. В здании — свой потолок комнаты под перекрытием здания. У открытой линии нет.")]
        [SerializeField] private bool hasCeiling;

        [Tooltip("Толщина пола и потолка, м. 0,05 — отделка поверх пола здания, 0,2 — настоящее перекрытие.")]
        [Min(0.01f)]
        [SerializeField] private float slabThickness = 0.05f;

        [Tooltip("Материал пола. Пусто — материал стены.")]
        [SerializeField] private Material floorMaterial;

        [Tooltip("Материал потолка снизу. Пусто — материал стены.")]
        [SerializeField] private Material ceilingMaterial;

        // Старые копии вверх: их читает только миграция — переводит в повтор (LiveBuilderArray).
        [SerializeField, HideInInspector] private int copiesUp = 1;
        [SerializeField, HideInInspector] private float copyStep = 4f;

        [Space]
        [Header("Curves")]
        [Tooltip("Длина одного прямого кусочка на плавном изгибе, м. Меньше — глаже, но тяжелее меш. 0,5 — заметно гладко.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float curveStep = 0.5f;

        [Tooltip("Из скольких прямых кусков скруглённый угол (радиус — голубой ручкой у выбранной точки). 1 — срез, фаска; 6 — кругло.")]
        [Range(1, 16)]
        [SerializeField] private int cornerSegments = 6;

        [Space]
        [Header("Look and Collision")]
        [Tooltip("Материал стены. Пусто — серый материал набора.")]
        [SerializeField] private Material material;

        [Tooltip("Собирать коллайдер по мешу — в стену можно упереться.")]
        [SerializeField] private bool hasCollider = true;

        [Space]
        [Header("Texture")]
        [Tooltip("World — текстура по миру: ряды плиток сходятся через этажи и соседние постройки, сдвинул постройку — рисунок остаётся на месте. " +
                 "Object — текстура едет вместе с постройкой. Старые постройки — Object, новые — World.")]
        [SerializeField] private LiveBuilderTextureAlignment textureAlignment = LiveBuilderTextureAlignment.Object;

        [Tooltip("Сдвиг, масштаб и поворот текстуры: стороны, торцы (верх, откосы проёмов), пол, потолок. Нет строки — как есть.")]
        [SerializeField] private LiveBuilderTextureFitList<SplineWallTextureSurface> textureFits = new();

        // Чем собран текущий меш: изменилось — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;

        // Перегородка в здании: точки, что лежат на внутренней стороне его стен, и где они на его контуре. Пишет редактор.
        [SerializeField, HideInInspector] private List<SplineWallBuildingAnchor> buildingAnchors = new();

        // Какими были своя линия, контур здания и число его точек при прошлой сверке — так видно, что двигали: точки стены или стену здания.
        [SerializeField, HideInInspector] private int syncedOwnLineHash;
        [SerializeField, HideInInspector] private int syncedBuildingLineHash;
        [SerializeField, HideInInspector] private int syncedBuildingKnotCount;


        public SplineWallType WallType
        {
            get => wallType;
            set => wallType = value;
        }

        // С типом — из типа.
        public float Thickness
        {
            get => wallType != null ? wallType.Thickness : thickness;
            set => thickness = Mathf.Max(0.02f, value);
        }

        public float Height
        {
            get => height;
            set => height = Mathf.Max(0.05f, value);
        }

        public SplineWallHeightInBuilding HeightInBuilding
        {
            get => heightInBuilding;
            set => heightInBuilding = value;
        }

        public SplineWallAlignment Alignment
        {
            get => alignment;
            set => alignment = value;
        }

        public bool SkipsSidesAlongBuildingWalls
        {
            get => skipsSidesAlongBuildingWalls;
            set => skipsSidesAlongBuildingWalls = value;
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

        // Есть ли у стены пол или потолок — только у замкнутой линии они строятся.
        public bool HasSlabs => hasFloor || hasCeiling;

        public float SlabThickness
        {
            get => slabThickness;
            set => slabThickness = Mathf.Max(0.01f, value);
        }

        public Material FloorMaterial
        {
            get => floorMaterial;
            set => floorMaterial = value;
        }

        public Material CeilingMaterial
        {
            get => ceilingMaterial;
            set => ceilingMaterial = value;
        }

        // Только для миграции старых данных.
        public int LegacyCopiesUp
        {
            get => copiesUp;
            set => copiesUp = Mathf.Max(1, value);
        }

        public float LegacyCopyStep
        {
            get => copyStep;
            set => copyStep = Mathf.Max(0.1f, value);
        }

        public float CurveStep => curveStep;

        public int CornerSegments => cornerSegments;

        // С типом — из типа.
        public Material Material
        {
            get => wallType != null ? wallType.Material : material;
            set => material = value;
        }

        public bool HasCollider => hasCollider;

        public LiveBuilderTextureAlignment TextureAlignment
        {
            get => textureAlignment;
            set => textureAlignment = value;
        }

        public LiveBuilderTextureFitList<SplineWallTextureSurface> TextureFits => textureFits ??= new LiveBuilderTextureFitList<SplineWallTextureSurface>();

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }

        public List<SplineWallBuildingAnchor> BuildingAnchors => buildingAnchors;

        public int SyncedOwnLineHash
        {
            get => syncedOwnLineHash;
            set => syncedOwnLineHash = value;
        }

        public int SyncedBuildingLineHash
        {
            get => syncedBuildingLineHash;
            set => syncedBuildingLineHash = value;
        }

        public int SyncedBuildingKnotCount
        {
            get => syncedBuildingKnotCount;
            set => syncedBuildingKnotCount = value;
        }


        private void Reset()
        {
            textureAlignment = LiveBuilderTextureAlignment.World;
        }
    }
}
