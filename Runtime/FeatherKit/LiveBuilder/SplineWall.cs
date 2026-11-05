using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Где стена стоит относительно линии: линия посередине стены, по её левому или правому краю.</summary>
    public enum SplineWallAlignment
    {
        [InspectorName("По центру")] Center,
        [InspectorName("Слева от линии")] Left,
        [InspectorName("Справа от линии")] Right
    }


    /// <summary>
    /// Стена по линии: сплайн на этом же объекте — путь стены, у точки свой режим — угол или плавный изгиб.
    /// Толщина и высота — здесь, а с типом (<see cref="SplineWallType"/>) толщина и материал — из типа; «копии вверх» — та же стена на этажах выше (перегородка на все этажи здания).
    /// Замкнутая стена в здании может быть комнатой: стороны вдоль стен здания не строятся, но остаются в линии.
    /// Точки перегородки, лежащие на стене здания, держатся за неё: двигаешь стену здания — точки едут следом.
    /// Проёмы — вырезы (<see cref="GeometryCutter"/>), обычно её дети, и вырезающие коробки объекта из коробок, в котором лежит стена, — насквозь.
    /// Меш собирает редактор и хранит файлом рядом со сценой; в игре компонент — только данные.
    /// </summary>
    [FDescription("Стена по линии: путь — сплайн на этом объекте, у точки — угол или плавно. Проёмы — вырезы «Geometry Cutter» " +
                  "и вырезающие коробки объекта из коробок, в котором лежит стена.")]
    [AddComponentMenu("FeatherKit/Live Builder/Spline Wall")]
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class SplineWall : MonoBehaviour, IStoreyCopies
    {
        [Header("Размер")]
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
        [Header("Копии вверх")]
        [Tooltip("Сколько всего таких стен друг над другом, вместе с этой. Перегородка на все этажи здания — по числу этажей.")]
        [Range(1, 60)]
        [SerializeField] private int copiesUp = 1;

        [Tooltip("Шаг копий вверх, м — обычно высота этажа: 4.")]
        [Min(0.1f)]
        [SerializeField] private float copyStep = 4f;

        [Space]
        [Header("Изгибы")]
        [Tooltip("Длина одного прямого кусочка на плавном изгибе, м. Меньше — глаже, но тяжелее меш. 0,5 — заметно гладко.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float curveStep = 0.5f;

        [Tooltip("Из скольких прямых кусков скруглённый угол (радиус — голубой ручкой у выбранной точки). 1 — срез, фаска; 6 — кругло.")]
        [Range(1, 16)]
        [SerializeField] private int cornerSegments = 6;

        [Space]
        [Header("Вид и столкновения")]
        [Tooltip("Материал стены. Пусто — серый материал набора.")]
        [SerializeField] private Material material;

        [Tooltip("Собирать коллайдер по мешу — в стену можно упереться.")]
        [SerializeField] private bool hasCollider = true;

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

        public int CopiesUp
        {
            get => copiesUp;
            set => copiesUp = Mathf.Clamp(value, 1, 60);
        }

        public float CopyStep
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
    }
}
