using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Повтор: объект и всё под ним ещё раз со сдвигом по осям X, Y, Z этого объекта — через любые постройки внутри.
    /// - копии — часть результата: их строят сами коробки, вырезы, стены и здания, в иерархии копий нет
    /// - шаг Y — высота этажа (здания над повтором, иначе уровня), число Y — «до верха здания»
    /// - пропуски по каждой оси: «1, 5», «2-4», «last»; копия 1 — место самого оригинала
    /// - по X: ровно N или «сколько влезет» в длину, по осям или вдоль стены; узор из дочерних вариантов («ABA»)
    /// </summary>
    [FDescription("Повтор: объект и всё внутри — ещё раз со сдвигом по осям, копии считаются из оригинала. " +
                  "Шаг Y — высота этажа, число Y — до верха здания, пропуски — номера копий по оси: «1, 5», «2-4», «last».")]
    [AddComponentMenu("FeatherKit/Live Builder/Live Builder Array")]
    [DisallowMultipleComponent]
    public class LiveBuilderArray : MonoBehaviour
    {
        // Больше копий на цепочку вложенных повторов не строится: 200 × 200 — уже 40 000 и зависший редактор.
        public const int MaxCopies = 5000;

        // Больше копий по одной оси не бывает: защита от случайного лишнего нуля.
        public const int MaxCountPerAxis = 1000;

        [Header("Count")]
        [Tooltip("Сколько всего по X, вместе с оригиналом. 1 — без повтора.")]
        [Range(1, MaxCountPerAxis)]
        [SerializeField] private int countX = 1;

        [Tooltip("Сколько всего по Y, вместе с оригиналом. 1 — без повтора.")]
        [Range(1, MaxCountPerAxis)]
        [SerializeField] private int countY = 2;

        [Tooltip("Сколько всего по Z, вместе с оригиналом. 1 — без повтора.")]
        [Range(1, MaxCountPerAxis)]
        [SerializeField] private int countZ = 1;

        [Tooltip("Число по Y — до верхнего этажа здания, в котором стоит повтор: добавил этаж — добавилась копия.")]
        [SerializeField] private bool countYToBuildingTop;

        [Space]
        [Header("Step")]
        [Tooltip("Шаг копий по оси X этого объекта, м.")]
        [SerializeField] private float stepX = 2f;

        [Tooltip("Шаг копий по оси Y этого объекта, м. Работает, если шаг — не высота этажа.")]
        [SerializeField] private float stepY = 3f;

        [Tooltip("Шаг копий по оси Z этого объекта, м.")]
        [SerializeField] private float stepZ = 2f;

        [Tooltip("Шаг Y — высота этажа: здания, если повтор в нём, иначе этажей уровня.")]
        [SerializeField] private bool stepYIsStoreyHeight = true;

        [Space]
        [Header("Skip")]
        [Tooltip("Какие копии по X не строить, номера с 1: «1, 5», «2-4», «last» — последняя. Копия 1 — сам оригинал.")]
        [SerializeField] private string skipX = "";

        [Tooltip("Какие копии по Y не строить, номера с 1: «1, 5», «2-4», «last» — последняя (лестница без верхнего этажа).")]
        [SerializeField] private string skipY = "";

        [Tooltip("Какие копии по Z не строить, номера с 1: «1, 5», «2-4», «last» — последняя.")]
        [SerializeField] private string skipZ = "";

        [Space]
        [Header("Fill")]
        [Tooltip("Count — ровно Count X копий; Fill — сколько влезет по X в длину Length с отступами Margin, шаг растягивается поровну.")]
        [SerializeField] private LiveBuilderArrayMode mode = LiveBuilderArrayMode.Count;

        [Tooltip("Длина ряда по X для Fill, м. Вдоль стены 0 — вся сторона между углами, оригинал встаёт на первое место.")]
        [SerializeField] private float length;

        [Tooltip("Отступ от краёв ряда для Fill, м.")]
        [SerializeField] private float margin;

        [Space]
        [Header("Along")]
        [Tooltip("Axes — по осям объекта; Wall — по X вдоль линии стены или контура здания, с поворотом по стене, углы пропускаются.")]
        [SerializeField] private LiveBuilderArrayAlong along = LiveBuilderArrayAlong.Axes;

        [Space]
        [Header("Pattern")]
        [Tooltip("Варианты — дочерние объекты: первый — A, второй — B… Каждая копия показывает один вариант. Пусто — копируется всё.")]
        [SerializeField] private List<Transform> variants = new();

        [Tooltip("Порядок вариантов по X: «AB» — A B A B…, «ABBA». Пусто — всегда первый. Пропуски узор не сдвигают.")]
        [SerializeField] private string pattern = "";


        // ── Число ────────────────────────────────────────────────────────

        public int CountX
        {
            get => countX;
            set => countX = ClampCount(value);
        }

        public int CountY
        {
            get => countY;
            set => countY = ClampCount(value);
        }

        public int CountZ
        {
            get => countZ;
            set => countZ = ClampCount(value);
        }

        public bool CountYToBuildingTop
        {
            get => countYToBuildingTop;
            set => countYToBuildingTop = value;
        }


        // ── Шаг ──────────────────────────────────────────────────────────

        public float StepX
        {
            get => stepX;
            set => stepX = value;
        }

        public float StepY
        {
            get => stepY;
            set => stepY = value;
        }

        public float StepZ
        {
            get => stepZ;
            set => stepZ = value;
        }

        public bool StepYIsStoreyHeight
        {
            get => stepYIsStoreyHeight;
            set => stepYIsStoreyHeight = value;
        }


        // ── Пропуски ─────────────────────────────────────────────────────

        public string SkipX
        {
            get => skipX;
            set => skipX = value ?? "";
        }

        public string SkipY
        {
            get => skipY;
            set => skipY = value ?? "";
        }

        public string SkipZ
        {
            get => skipZ;
            set => skipZ = value ?? "";
        }


        // ── Fill и вдоль стены ───────────────────────────────────────────

        public LiveBuilderArrayMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public float Length
        {
            get => length;
            set => length = Mathf.Max(0f, value);
        }

        public float Margin
        {
            get => margin;
            set => margin = Mathf.Max(0f, value);
        }

        public LiveBuilderArrayAlong Along
        {
            get => along;
            set => along = value;
        }


        // ── Узор ─────────────────────────────────────────────────────────

        public List<Transform> Variants => variants ??= new List<Transform>();

        public string Pattern
        {
            get => pattern;
            set => pattern = value ?? "";
        }


        // ── По номеру оси ────────────────────────────────────────────────

        // Ось — 0, 1, 2: X, Y, Z. Для расчёта, который идёт по осям циклом.
        public int CountOf(int axis)
        {
            return axis == 0 ? countX : axis == 1 ? countY : countZ;
        }


        public float StepOf(int axis)
        {
            return axis == 0 ? stepX : axis == 1 ? stepY : stepZ;
        }


        public string SkipOf(int axis)
        {
            return (axis == 0 ? skipX : axis == 1 ? skipY : skipZ) ?? "";
        }


        private static int ClampCount(int value)
        {
            return Mathf.Clamp(value, 1, MaxCountPerAxis);
        }
    }


    /// <summary>Сколько копий по X: ровно Count X или сколько влезет в длину (<see cref="LiveBuilderArray.Length"/>).</summary>
    public enum LiveBuilderArrayMode
    {
        Count = 0,
        Fill = 1
    }


    /// <summary>Куда идут копии по X: по осям объекта или вдоль линии стены или контура здания, поворачиваясь с ней.</summary>
    public enum LiveBuilderArrayAlong
    {
        Axes = 0,
        Wall = 1
    }
}
