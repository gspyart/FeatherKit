using FeatherKit.Attributes;
using FeatherKit.LevelDesign;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Форма выреза: прямоугольник (со скруглёнными углами — арка) или круг (овал, если ширина и высота разные).</summary>
    public enum GeometryCutterShape
    {
        [InspectorName("Прямоугольник")] Box,
        [InspectorName("Круг")] Circle
    }


    /// <summary>
    /// Вырез: объём, который прорезает всё построенное Live Builder, что он пересекает, — объекты из коробок, стены по линии, здания.
    /// В отличие от вырезающей коробки, поворачивается как угодно: окно в косой стене, круглое окно, арка, пролом под углом.
    /// Форма — на плоскости XY выреза, протянута вдоль его Z: опора — низ посередине, ширина по X, высота вверх по Y, глубина по Z в обе стороны.
    /// Стенки дыры строятся сами. Не режет свои дочерние объекты и свой префаб: дверь или окно-префаб с вырезом внутри
    /// прорезает стену, куда его поставили, а свою раму — нет. Режет только в редакторе: в игру попадает уже прорезанный меш.
    /// «Копии вверх» — тот же вырез на этажах выше: окно на все этажи одним объектом.
    /// Вырез в стене по линии или в здании может помнить своё место на линии: двигаешь точки — он едет со своим отрезком и поворачивается за ним.
    /// Своего меша у выреза нет, поэтому размер для модификатора Array (ряд окон «от края») он сообщает сам — ширина, высота, глубина.
    /// </summary>
    [FDescription("Вырез: прорезает постройки Live Builder, которые пересекает, под любым углом. Опора — низ посередине, глубина — по синей оси.")]
    [AddComponentMenu("FeatherKit/Live Builder/Geometry Cutter")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class GeometryCutter : MonoBehaviour, IStoreyCopies, IArrayCopySize
    {
        // Самая маленькая сторона выреза, м: тоньше — уже не дыра, а щель в расчёте.
        public const float MinSize = 0.02f;

        [Header("Форма")]
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

        [Space]
        [Header("Скругление")]
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

        [Space]
        [Header("Копии вверх")]
        [Tooltip("Сколько всего таких вырезов друг над другом, вместе с этим. 1 — один; окно на все 5 этажей — 5.")]
        [Range(1, 60)]
        [SerializeField] private int copiesUp = 1;

        [Tooltip("Шаг копий вверх, м — обычно высота этажа: 4.")]
        [Min(0.1f)]
        [SerializeField] private float copyStep = 4f;

        [Space]
        [Header("Вид")]
        [Tooltip("Материал стенок дыры. Пусто — материал того, что режем.")]
        [SerializeField] private Material revealMaterial;

        [Space]
        [Header("Место в стене")]
        [Tooltip("Двигаешь точки стены по линии или контура здания, в котором лежит вырез, — он едет со своим отрезком и поворачивается за ним. " +
                 "Выключено — стоит, где поставили. Вырезы на стенах «Проём» и «Коробка» ставят включёнными.")]
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

        public Material RevealMaterial
        {
            get => revealMaterial;
            set => revealMaterial = value;
        }

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
    }
}
