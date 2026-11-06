using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>В какую сторону объекта лестница идёт вверх.</summary>
    public enum BoxModelStairsDirection
    {
        [InspectorName("+X")] PositiveX,
        [InspectorName("−X")] NegativeX,
        [InspectorName("+Z")] PositiveZ,
        [InspectorName("−Z")] NegativeZ
    }


    /// <summary>Как выглядит лестница сбоку: сплошной ступенчатый объём до пола или отдельные плиты ступеней.</summary>
    public enum BoxModelStairsStyle
    {
        [InspectorName("Ступени")] SeparateSteps,
        [InspectorName("Сплошная")] Solid
    }


    /// <summary>Сколько маршей: один прямой или два туда-обратно с площадкой посередине высоты (лестничная клетка).</summary>
    public enum BoxModelStairsShape
    {
        [InspectorName("Прямая")] Straight,
        [InspectorName("С площадкой")] Switchback
    }


    /// <summary>
    /// Лестница в объекте из коробок: ступени — коробки объекта, поэтому режутся и повторяются как остальные.
    /// Форма — один прямой марш или два марша туда-обратно с площадкой на середине высоты: вся лестничная клетка одной вещью,
    /// поменял высоту — марши и площадка пересчитались.
    /// Вид — сплошной объём до пола или отдельные плиты ступеней, под которыми пусто.
    /// Число ступеней — от высоты: подъём ступени подгоняется, чтобы верх лестницы пришёлся ровно на высоту.
    /// Ходят по невидимому скату через носики ступеней — он только в коллайдере. Может пробить перекрытие над собой.
    /// Опора — угол с наименьшими координатами, как у коробки. В игру не попадает — остаётся собранный меш.
    /// </summary>
    [FDescription("Лестница: ступени — коробки объекта, ходят по невидимому скату через носики. Число ступеней — от высоты. " +
                  "Форма — прямой марш или два марша с площадкой. Вид — отдельные плиты ступеней или сплошной объём до пола. Опора — угол с наименьшими координатами.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Stairs")]
    [DisallowMultipleComponent]
    public class BoxModelStairs : MonoBehaviour
    {
        // С какой высоты над полом лестницы режется перекрытие, м: ниже голова не достаёт, стены и пол вокруг не трогаются.
        public const float HeadroomHeight = 2.1f;

        // Мельче ступень не бывает, м.
        public const float MinStepDepth = 0.1f;

        [Header("Форма")]
        [Tooltip("Куда лестница идёт вверх, по осям объекта. У лестницы с площадкой — куда идёт первый марш.")]
        [SerializeField] private BoxModelStairsDirection direction = BoxModelStairsDirection.PositiveZ;

        [Tooltip("Прямая — один марш. С площадкой — два марша туда-обратно и площадка на середине высоты: лестничная клетка одной вещью.")]
        [SerializeField] private BoxModelStairsShape shape = BoxModelStairsShape.Straight;

        [Tooltip("Высота лестницы — от пола до верха последней ступени, м. Обычно высота этажа: 4.")]
        [Min(0.2f)]
        [SerializeField] private float height = 4f;

        [Tooltip("Ширина лестницы, м. У лестницы с площадкой — вся клетка: оба марша и зазор между ними.")]
        [Min(0.5f)]
        [SerializeField] private float width = 2f;

        [Tooltip("Глубина площадки между маршами, м — только у лестницы с площадкой. По метрикам — не меньше ширины марша.")]
        [Min(0.5f)]
        [SerializeField] private float landingDepth = 1.6f;

        [Tooltip("Зазор между маршами, м — только у лестницы с площадкой. 0 — марши вплотную, 0,2 — узкий колодец.")]
        [Min(0f)]
        [SerializeField] private float flightGap = 0.2f;

        [Tooltip("Первый марш — с другой стороны клетки. Выключено — у меньших координат поперёк подъёма.")]
        [SerializeField] private bool isFirstFlightOnOtherSide;

        [Tooltip("Ступени — каждая ступень отдельной плитой, под лестницей пусто. Сплошная — ступенчатый объём от пола.")]
        [SerializeField] private BoxModelStairsStyle style = BoxModelStairsStyle.SeparateSteps;

        [Tooltip("Толщина плиты ступени, м — только для вида «Ступени». Равна подъёму (0,2) — ступени сходятся углами; меньше — висят отдельно.")]
        [Min(0.02f)]
        [SerializeField] private float stepThickness = 0.2f;

        [Space]
        [Header("Ступени")]
        [Tooltip("Желаемый подъём одной ступени, м. Точный подгоняется, чтобы ступени ровно легли в высоту. По метрикам — 0,2.")]
        [Min(0.05f)]
        [SerializeField] private float stepHeight = 0.2f;

        [Tooltip("Глубина одной ступени, м. По метрикам — 0,3: на этаж 4 м — 20 ступеней, марш 6 м. Длина марша — ступеней × глубина.")]
        [Min(0.1f)]
        [SerializeField] private float stepDepth = 0.3f;

        [Space]
        [Header("Вокруг")]
        [Tooltip("Проём в перекрытии над маршем, чтобы не биться головой: режет над лестницей от 2,1 м над её полом до её верха. Стены по бокам не режет.")]
        [SerializeField] private bool cutsAbove = true;

        [Tooltip("Материал ступеней. Пусто — материал объекта.")]
        [SerializeField] private Material material;


        public BoxModelStairsDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        public BoxModelStairsShape Shape
        {
            get => shape;
            set => shape = value;
        }

        public float Height
        {
            get => height;
            set => height = Mathf.Max(0.2f, value);
        }

        public float Width
        {
            get => width;
            set => width = Mathf.Max(0.5f, value);
        }

        public float LandingDepth
        {
            get => landingDepth;
            set => landingDepth = Mathf.Max(0.5f, value);
        }

        public float FlightGap
        {
            get => flightGap;
            set => flightGap = Mathf.Max(0f, value);
        }

        public bool IsFirstFlightOnOtherSide
        {
            get => isFirstFlightOnOtherSide;
            set => isFirstFlightOnOtherSide = value;
        }

        public BoxModelStairsStyle Style
        {
            get => style;
            set => style = value;
        }

        public float StepThickness
        {
            get => stepThickness;
            set => stepThickness = Mathf.Max(0.02f, value);
        }

        public float StepHeight => stepHeight;

        public float StepDepth
        {
            get => stepDepth;
            set => stepDepth = Mathf.Max(MinStepDepth, value);
        }

        public bool CutsAbove
        {
            get => cutsAbove;
            set => cutsAbove = value;
        }

        public Material Material
        {
            get => material;
            set => material = value;
        }

        // Ступеней столько, чтобы подъём был ближе всего к желаемому. У лестницы с площадкой — в обоих маршах вместе, не меньше двух.
        public int StepCount => Mathf.Max(IsSwitchback ? 2 : 1, Mathf.RoundToInt(height / stepHeight));

        public bool IsSwitchback => shape == BoxModelStairsShape.Switchback;

        // Ступеней в первом марше (до площадки) и во втором; у прямой второго нет.
        public int FirstFlightStepCount => IsSwitchback ? StepCount / 2 : StepCount;
        public int SecondFlightStepCount => StepCount - FirstFlightStepCount;

        // Длина маршей по направлению подъёма, без площадки, м: марши лежат рядом, длина — по длинному.
        public float FlightLength => Mathf.Max(FirstFlightStepCount, SecondFlightStepCount) * stepDepth;

        // Ширина одного марша, м: у лестницы с площадкой — половина клетки без зазора.
        public float FlightWidth => IsSwitchback ? Mathf.Max(0.25f, (width - flightGap) / 2f) : width;

        // Длина всей лестницы по направлению подъёма, м: у лестницы с площадкой — марши и площадка.
        public float Length => IsSwitchback ? FlightLength + landingDepth : StepCount * stepDepth;

        // Ось объекта, по которой идёт подъём: 0 — X, 2 — Z.
        public int RunAxis => direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.NegativeX ? 0 : 2;

        public bool RunsTowardsPlus => direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ;

        // Размер всей лестницы по осям объекта, м.
        public Vector3 Size
        {
            get
            {
                var size = new Vector3(width, height, width);
                size[RunAxis] = Length;
                return size;
            }
        }


        // Глубина ступени, чтобы лестница легла ровно в эту длину по подъёму: у лестницы с площадкой площадка остаётся своей.
        public float StepDepthForLength(float length)
        {
            var runLength = IsSwitchback ? length - landingDepth : length;
            var steps = IsSwitchback ? Mathf.Max(FirstFlightStepCount, SecondFlightStepCount) : StepCount;
            return Mathf.Max(MinStepDepth, runLength / steps);
        }
    }
}
