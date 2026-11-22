using FeatherKit.Attributes;
using FeatherKit.LevelDesign;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Форма выреза: прямоугольник (со скруглёнными углами — арка) или круг (овал, если ширина и высота разные).</summary>
    public enum GeometryCutterShape
    {
        [InspectorName("Box")] Box,
        [InspectorName("Circle")] Circle
    }


    /// <summary>
    /// Где вырез режет, как составные группы в Chisel. Правило — LiveBuilderCutRules в редакторе.
    /// - OwnObject — ближайшая постройка над вырезом (объект из коробок, стена, здание) и всё в ней, кроме вложенных объектов из коробок
    /// - EverythingItTouches — всё, чего касается, через любые объекты
    /// - LegacyOwnGroup — старое «Своя группа», работает как OwnObject; в инспекторе не выбрать
    /// </summary>
    public enum GeometryCutterScope
    {
        [InspectorName("Own object")] OwnObject = 0,
        [InspectorName("Own object (legacy)")] LegacyOwnGroup = 1,
        [InspectorName("Everything it touches")] EverythingItTouches = 2
    }


    /// <summary>
    /// Единственный вырез Live Builder — любой формы под любым углом: дверь, окно, арка, ниша, зал внутри объекта из коробок.
    /// - форма на плоскости XY выреза, протянута вдоль Z; стенки дыры строятся сами, материалы — общий и по сторонам
    /// - где режет — <see cref="GeometryCutterScope"/>, что — <see cref="LiveBuilderCutTargets"/>; свои дочерние объекты и свой префаб не режет
    /// - помнит место на линии стены или контура здания: двигаешь точки — едет со своим отрезком
    /// - размер для модификатора Array (ряд окон «от края»); режет только в редакторе — в игру попадает готовый меш
    /// </summary>
    [FDescription("Вырез: прорезает постройки Live Builder, которые пересекает, под любым углом. Коробка по осям объекта режет его точно, " +
                  "остальное — вычитанием из меша. Глубина — по синей оси.")]
    [AddComponentMenu("FeatherKit/Live Builder/Geometry Cutter")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class GeometryCutter : MonoBehaviour, IArrayCopySize
    {
        // Самая маленькая сторона выреза, м: тоньше — уже не дыра, а щель в расчёте.
        public const float MinSize = 0.02f;

        [Header("Shape")]
        [Tooltip("Прямоугольник — со скруглёнными углами получается арка. Круг — круглое или овальное окно.")]
        [SerializeField] private GeometryCutterShape shape = GeometryCutterShape.Box;

        [Tooltip("Ширина, м. Дверь — 1,5, окно — 2.")]
        [Min(MinSize)]
        [SerializeField] private float width = 1.5f;

        [Tooltip("Высота от опоры вверх, м. Дверь — 2,5.")]
        [Min(MinSize)]
        [SerializeField] private float height = 2.5f;

        [Tooltip("Глубина, м, поровну в обе стороны от опоры. Насквозь — больше толщины стены: 0,3 + запас.")]
        [Min(MinSize)]
        [SerializeField] private float depth = 0.5f;

        [Tooltip("Где низ середины дыры относительно точки объекта, м, в его осях. Нужен, когда внутри проёма лежит визуал: " +
                 "точка объекта стоит, где удобно визуалу, а дыра — где надо. 0 — дыра от точки объекта.")]
        [SerializeField] private Vector3 shapeOffset;

        [Space]
        [Header("Corner Rounding")]
        [Tooltip("Радиус двух верхних углов, м. Половина ширины — полукруглая арка. 0 — прямые углы.")]
        [Min(0f)]
        [SerializeField] private float topCornerRadius;

        [Tooltip("Радиус двух нижних углов, м. 0 — прямые углы.")]
        [Min(0f)]
        [SerializeField] private float bottomCornerRadius;

        [Tooltip("Из скольких прямых кусочков угол скругления. Больше — глаже, но тяжелее меш.")]
        [Range(1, 16)]
        [SerializeField] private int cornerSegments = 6;

        [Tooltip("Сколько сторон у круга. 24 — уже кругло.")]
        [Range(6, 64)]
        [SerializeField] private int circleSegments = 24;

        // Старые копии вверх: их читает только миграция — переводит в повтор (LiveBuilderArray).
        [SerializeField, HideInInspector] private int copiesUp = 1;
        [SerializeField, HideInInspector] private float copyStep = 4f;

        [Space]
        [Header("Cutting")]
        [Tooltip("Что прорезает. Всё — объекты из коробок, стены и здания целиком. " +
                 "Только перекрытия — пол, перекрытия и крышу зданий: шахта, дыра под лестницу. Только стены — стены по линии и стены зданий.")]
        [SerializeField] private LiveBuilderCutTargets cutTargets = LiveBuilderCutTargets.Everything;

        [Tooltip("Own object — ближайшая постройка над вырезом и всё в ней, во вложенные объекты из коробок не заходит. " +
                 "Everything it touches — всё, чего касается, через любые объекты.")]
        [SerializeField] private GeometryCutterScope scope = GeometryCutterScope.OwnObject;

        [Space]
        [Header("Look")]
        [Tooltip("Материал стенок дыры. Пусто — материал того, что режем.")]
        [SerializeField] private Material revealMaterial;

        [Tooltip("Свой материал у пола, потолка и стен дыры — как у сторон коробки. Пусто — материал стенок выше.")]
        [SerializeField] private GeometryCutterSideMaterials sideMaterials = new();

        [Tooltip("Сдвиг, масштаб и поворот текстуры на стенках дыры — те же места, что у материалов: All, пол, потолок, стены, каждая стена. " +
                 "Нет строки — как есть.")]
        [SerializeField] private LiveBuilderTextureFitList<BoxModelSideSlot> textureFits = new();

        [Space]
        [Header("Place in Wall")]
        [Tooltip("Двигаешь точки стены по линии или контура здания, в котором лежит проём, — он едет со своим отрезком и поворачивается за ним. " +
                 "Выключено — стоит, где поставили. Проёмы на стенах инструменты Cut и Box ставят включёнными.")]
        [SerializeField] private bool followsWallLine;

        // Место на линии стены: какой сплайн и где на нём — номер точки с долей до следующей (2,4 — между второй и третьей). Пишет редактор.
        [SerializeField, HideInInspector] private int lineSplineIndex;
        [SerializeField, HideInInspector] private float placeOnLine;

        // Где вырез относительно линии в этом месте: сдвиг поперёк и вдоль неё и поворот — в осях линии (X вдоль, Y вверх, Z поперёк).
        [SerializeField, HideInInspector] private Vector3 offsetFromLine;
        [SerializeField, HideInInspector] private Quaternion rotationOnLine = Quaternion.identity;

        // Какими были линия, число её точек и место выреза при прошлой сверке — так видно, что двигали: линию или сам вырез.
        [SerializeField, HideInInspector] private int syncedLineHash;
        [SerializeField, HideInInspector] private int syncedKnotCount;
        [SerializeField, HideInInspector] private Vector3 syncedLocalPosition;
        [SerializeField, HideInInspector] private Quaternion syncedLocalRotation = Quaternion.identity;

        // Толщина стены при прошлой сверке, м: поменялась — сквозной вырез подгоняется под новую. 0 — ещё не запомнена.
        [SerializeField, HideInInspector] private float syncedWallThickness;


        public GeometryCutterShape Shape
        {
            get => shape;
            set => shape = value;
        }

        public float Width
        {
            get => width;
            set => width = Mathf.Max(MinSize, value);
        }

        public float Height
        {
            get => height;
            set => height = Mathf.Max(MinSize, value);
        }

        public float Depth
        {
            get => depth;
            set => depth = Mathf.Max(MinSize, value);
        }

        public Vector3 ShapeOffset
        {
            get => shapeOffset;
            set => shapeOffset = value;
        }

        // Не больше половины ширины и высоты: иначе скругления верха и низа налезут друг на друга.
        public float TopCornerRadius
        {
            get => Mathf.Clamp(topCornerRadius, 0f, Mathf.Min(width, height) / 2f);
            set => topCornerRadius = Mathf.Max(0f, value);
        }

        public float BottomCornerRadius
        {
            get => Mathf.Clamp(bottomCornerRadius, 0f, Mathf.Min(width, height - TopCornerRadius) / 2f);
            set => bottomCornerRadius = Mathf.Max(0f, value);
        }

        public int CornerSegments => cornerSegments;

        public int CircleSegments => circleSegments;

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

        public LiveBuilderCutTargets CutTargets
        {
            get => cutTargets;
            set => cutTargets = value;
        }

        public GeometryCutterScope Scope
        {
            get => scope;
            set => scope = value;
        }

        public Material RevealMaterial
        {
            get => revealMaterial;
            set => revealMaterial = value;
        }

        public GeometryCutterSideMaterials SideMaterials => sideMaterials ??= new GeometryCutterSideMaterials();

        public LiveBuilderTextureFitList<BoxModelSideSlot> TextureFits => textureFits ??= new LiveBuilderTextureFitList<BoxModelSideSlot>();

        // IArrayCopySize: копии массива «от края» встают по ширине, высоте и глубине выреза, с его масштабом.
        public Vector3 ArrayCopySize => Vector3.Scale(new Vector3(width, height, depth), transform.lossyScale);

        // ── Место на линии стены ─────────────────────────────────────────

        public bool FollowsWallLine
        {
            get => followsWallLine;
            set => followsWallLine = value;
        }

        public int LineSplineIndex
        {
            get => lineSplineIndex;
            set => lineSplineIndex = value;
        }

        public float PlaceOnLine
        {
            get => placeOnLine;
            set => placeOnLine = value;
        }

        public Vector3 OffsetFromLine
        {
            get => offsetFromLine;
            set => offsetFromLine = value;
        }

        public Quaternion RotationOnLine
        {
            get => rotationOnLine;
            set => rotationOnLine = value;
        }

        public int SyncedLineHash
        {
            get => syncedLineHash;
            set => syncedLineHash = value;
        }

        public int SyncedKnotCount
        {
            get => syncedKnotCount;
            set => syncedKnotCount = value;
        }

        public Vector3 SyncedLocalPosition
        {
            get => syncedLocalPosition;
            set => syncedLocalPosition = value;
        }

        public Quaternion SyncedLocalRotation
        {
            get => syncedLocalRotation;
            set => syncedLocalRotation = value;
        }

        public float SyncedWallThickness
        {
            get => syncedWallThickness;
            set => syncedWallThickness = value;
        }
    }
}
