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


    /// <summary>
    /// Лестничный марш в объекте из коробок: ступени — коробки объекта, поэтому режутся и повторяются как остальные.
    /// Вид — сплошной объём до пола или отдельные плиты ступеней, под которыми пусто.
    /// Число ступеней — от высоты: подъём ступени подгоняется, чтобы верх лестницы пришёлся ровно на высоту.
    /// Ходят по невидимому скату через носики ступеней — он только в коллайдере. Может пробить перекрытие над собой.
    /// Опора — угол с наименьшими координатами, как у коробки. В игру не попадает — остаётся собранный меш.
    /// </summary>
    [FDescription("Лестница: ступени — коробки объекта, ходят по невидимому скату через носики. Число ступеней — от высоты. " +
                  "Вид — отдельные плиты ступеней или сплошной объём до пола. Опора — угол с наименьшими координатами.")]
    [AddComponentMenu("FeatherKit/Live Builder/Box Model Stairs")]
    [DisallowMultipleComponent]
    public class BoxModelStairs : MonoBehaviour
    {
        // С какой высоты над полом лестницы режется перекрытие, м: ниже голова не достаёт, стены и пол вокруг не трогаются.
        public const float HeadroomHeight = 2.1f;

        // Мельче ступень не бывает, м.
        public const float MinStepDepth = 0.1f;

        [Header("Форма")]
        [Tooltip("Куда лестница идёт вверх, по осям объекта.")]
        [SerializeField] private BoxModelStairsDirection direction = BoxModelStairsDirection.PositiveZ;

        [Tooltip("Высота лестницы — от пола до верха последней ступени, м. Обычно высота этажа: 4.")]
        [Min(0.2f)]
        [SerializeField] private float height = 4f;

        [Tooltip("Ширина марша, м.")]
        [Min(0.5f)]
        [SerializeField] private float width = 2f;

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

        // Ступеней столько, чтобы подъём был ближе всего к желаемому.
        public int StepCount => Mathf.Max(1, Mathf.RoundToInt(height / stepHeight));

        // Длина марша по направлению подъёма, м.
        public float Length => StepCount * stepDepth;

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
    }
}
