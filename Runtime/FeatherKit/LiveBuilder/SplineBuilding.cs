using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Где стены здания относительно контура: контур — их наружная сторона, внутренняя или середина.</summary>
    public enum SplineBuildingContourSide
    {
        [InspectorName("Контур снаружи")] Outside,
        [InspectorName("Контур внутри")] Inside,
        [InspectorName("Контур посередине")] Center
    }


    /// <summary>
    /// Здание по контуру: замкнутый сплайн на этом же объекте — план, у точки свой режим — угол или плавный изгиб.
    /// По контуру — стены на все этажи, между этажами — перекрытия точно по форме, снизу пол, сверху плоская крыша.
    /// Здание стоит на точке опоры: низ плиты пола — на контуре, верх пола первого этажа — на толщину плиты выше.
    /// Проёмы — вырезы (<see cref="GeometryCutter"/>), обычно его дети. Вырезающие коробки объекта из коробок, в котором лежит здание,
    /// режут стены насквозь и перекрытия там, где проходят: лестница с проёмом сверху пробивает пол этажа выше.
    /// Геометрия — отдельные дочерние объекты (<see cref="SplineBuildingPart"/>): «Storey N» со стенами и полом этажа, «Roof» — крыша;
    /// их можно прятать в окне сцены по одному. Меши собирает редактор и хранит файлами рядом со сценой; в игре компонент — только данные.
    /// </summary>
    [FDescription("Здание по контуру: стены на все этажи, перекрытия и крыша по форме замкнутого сплайна. " +
                  "Проёмы — вырезы «Geometry Cutter» и вырезающие коробки объекта, в котором лежит здание.")]
    [AddComponentMenu("FeatherKit/Live Builder/Spline Building")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(SplineContainer))]
    public class SplineBuilding : MonoBehaviour
    {
        // Защита от случайной высотки в тысячу этажей.
        public const int MaxStoreys = 60;

        [Header("Этажи")]
        [Tooltip("Сколько этажей. Перекрытия между ними ставятся сами.")]
        [Range(1, MaxStoreys)]
        [SerializeField] private int storeyCount = 1;

        [Tooltip("Высота этажа от пола до пола, м. По метрикам — 4: комната 3,8 и перекрытие 0,2.")]
        [Min(1f)]
        [SerializeField] private float storeyHeight = 4f;

        [Space]
        [Header("Стены")]
        [Tooltip("Тип наружных стен: толщина и материал снаружи — из него, общие со всеми стенами этого типа. Пусто — свои.")]
        [SerializeField] private SplineWallType wallType;

        [Tooltip("Толщина стен, м. Наружная — 0,3. С типом — из типа.")]
        [Min(0.02f)]
        [SerializeField] private float wallThickness = 0.3f;

        [Tooltip("Где стены относительно контура. Снаружи — контур это габарит здания. Внутри — контур это стены комнаты изнутри.")]
        [SerializeField] private SplineBuildingContourSide contourSide = SplineBuildingContourSide.Outside;

        [Space]
        [Header("Перекрытия")]
        [Tooltip("Толщина пола, перекрытий и крыши, м. Перекрытие — 0,2.")]
        [Min(0.02f)]
        [SerializeField] private float slabThickness = 0.2f;

        [Tooltip("Пол первого этажа — плита на контуре: здание стоит на точке опоры, верх пола — на толщину плиты выше.")]
        [SerializeField] private bool hasFloor = true;

        [Tooltip("Плоская крыша сверху. Выключено — открытая коробка, сверху видно этажи.")]
        [SerializeField] private bool hasRoof = true;

        [Space]
        [Header("Изгибы")]
        [Tooltip("Длина одного прямого кусочка на плавном изгибе, м. Меньше — глаже, но тяжелее меш. 0,5 — заметно гладко.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float curveStep = 0.5f;

        [Tooltip("Из скольких прямых кусков скруглённый угол контура (радиус — голубой ручкой у выбранной точки). 1 — срез, фаска; 6 — кругло.")]
        [Range(1, 16)]
        [SerializeField] private int cornerSegments = 6;

        [Space]
        [Header("Материалы")]
        [Tooltip("Стены снаружи, торцы перекрытий и откосы проёмов. Пусто — серый материал набора.")]
        [SerializeField] private Material outerWallMaterial;

        [Tooltip("Стены изнутри. Пусто — как снаружи.")]
        [SerializeField] private Material innerWallMaterial;

        [Tooltip("Пол этажей — верх перекрытий. Пусто — как стены снаружи.")]
        [SerializeField] private Material floorMaterial;

        [Tooltip("Потолок этажей — низ перекрытий. Пусто — как стены снаружи.")]
        [SerializeField] private Material ceilingMaterial;

        [Tooltip("Верх крыши. Пусто — как стены снаружи.")]
        [SerializeField] private Material roofMaterial;

        [Space]
        [Header("Столкновения")]
        [Tooltip("Собирать коллайдер по мешу — по зданию можно ходить.")]
        [SerializeField] private bool hasCollider = true;

        // Чем собран текущий меш: изменилось — пересборка. Пишет редактор.
        [SerializeField, HideInInspector] private string builtSignature;


        public int StoreyCount
        {
            get => storeyCount;
            set => storeyCount = Mathf.Clamp(value, 1, MaxStoreys);
        }

        public float StoreyHeight
        {
            get => storeyHeight;
            set => storeyHeight = Mathf.Max(1f, value);
        }

        public SplineWallType WallType
        {
            get => wallType;
            set => wallType = value;
        }

        // С типом — из типа.
        public float WallThickness
        {
            get => wallType != null ? wallType.Thickness : wallThickness;
            set => wallThickness = Mathf.Max(0.02f, value);
        }

        public SplineBuildingContourSide ContourSide
        {
            get => contourSide;
            set => contourSide = value;
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

        public bool HasRoof
        {
            get => hasRoof;
            set => hasRoof = value;
        }

        public float CurveStep => curveStep;

        public int CornerSegments => cornerSegments;

        // С типом — из типа.
        public Material OuterWallMaterial
        {
            get => wallType != null ? wallType.Material : outerWallMaterial;
            set => outerWallMaterial = value;
        }

        public Material InnerWallMaterial
        {
            get => innerWallMaterial;
            set => innerWallMaterial = value;
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

        public Material RoofMaterial
        {
            get => roofMaterial;
            set => roofMaterial = value;
        }

        public bool HasCollider => hasCollider;

        public string BuiltSignature
        {
            get => builtSignature;
            set => builtSignature = value;
        }

        // Высота верха пола первого этажа над контуром, м: есть пол — толщина плиты, нет — ноль.
        public float FloorTopHeight => hasFloor ? slabThickness : 0f;

        // Высота верха здания над контуром, м.
        public float TotalHeight => FloorTopHeight + storeyCount * storeyHeight;

        // От пола до потолка этажа, м. Перекрытие толще этажа — не меньше 5 см, как наименьшая высота стены: иначе перегородки сверялись бы без конца.
        public float ClearHeight => Mathf.Max(0.05f, storeyHeight - slabThickness);

        // Высота пола первого этажа в мире, м: контур — на высоте первой точки сплайна.
        public float FirstFloorWorldHeight
        {
            get
            {
                var container = GetComponent<SplineContainer>();
                var contourHeight = container != null && container.Spline != null && container.Spline.Count > 0 ? container.Spline[0].Position.y : 0f;
                return transform.TransformPoint(new Vector3(0f, contourHeight + FloorTopHeight, 0f)).y;
            }
        }


        // Этаж, на высоте которого точка мира: пол этажа — на ней или ниже, с нуля.
        public int StoreyAtHeight(float worldHeight)
        {
            return Mathf.Clamp(Mathf.FloorToInt((worldHeight - FirstFloorWorldHeight) / storeyHeight + 0.01f), 0, storeyCount - 1);
        }
    }
}
