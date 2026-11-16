using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Расстояние между двумя точками: ближе, дальше, в промежутке или вне его. «Horizontal Only» — без высоты.</summary>
    [Serializable]
    [ScenarioBlock("Space/Distance", Description = "Compare the distance between two points")]
    public class DistanceCondition : ScenarioCondition
    {
        [ScenarioLabel("From")]
        [SerializeField] private ScenarioPoint from = new ScenarioPoint(ScenarioPointKind.Player);

        [ScenarioLabel("To")]
        [SerializeField] private ScenarioPoint to = new ScenarioPoint(ScenarioPointKind.ThisObject);

        [ScenarioLabel("Is")]
        [SerializeField] private ScenarioDistanceComparison comparison;

        [Tooltip("Meters. For “Between” and “Outside” — the near edge")]
        [Min(0f)]
        [ScenarioLabel("Distance, m")]
        [SerializeField] private float distance = 3f;

        [Tooltip("Meters, the far edge for “Between” and “Outside”")]
        [Min(0f)]
        [ScenarioLabel("Max Distance, m")]
        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Ignore the height difference: measure on the floor plan")]
        [ScenarioLabel("Horizontal Only")]
        [SerializeField] private bool isHorizontalOnly;


        public ScenarioPoint From => from;

        public ScenarioPoint To => to;

        public ScenarioDistanceComparison Comparison
        {
            get => comparison;
            set => comparison = value;
        }

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Max(0f, value);
        }

        public float MaxDistance
        {
            get => maxDistance;
            set => maxDistance = Mathf.Max(0f, value);
        }

        public bool IsHorizontalOnly
        {
            get => isHorizontalOnly;
            set => isHorizontalOnly = value;
        }

        public override string Summary => comparison switch
        {
            ScenarioDistanceComparison.CloserThan => $"{from.Summary} → {to.Summary} < {distance:0.##} m",
            ScenarioDistanceComparison.FartherThan => $"{from.Summary} → {to.Summary} > {distance:0.##} m",
            ScenarioDistanceComparison.Between => $"{from.Summary} → {to.Summary} {distance:0.##}–{maxDistance:0.##} m",
            _ => $"{from.Summary} → {to.Summary} outside {distance:0.##}–{maxDistance:0.##} m"
        };


        // Точки нет — сравнивать нечего: условие не выполнено, а не ошибка.
        protected override bool Check(ScenarioRun run)
        {
            if (!ScenarioDistanceMeasure.TryMeasure(run, from, to, isHorizontalOnly, out var measured))
                return false;

            return comparison switch
            {
                ScenarioDistanceComparison.CloserThan => measured < distance,
                ScenarioDistanceComparison.FartherThan => measured > distance,
                ScenarioDistanceComparison.Between => measured >= distance && measured <= maxDistance,
                _ => measured < distance || measured > maxDistance
            };
        }
    }


    /// <summary>Расстояние между двумя точками — общее для условия Distance и события Distance Crossed.</summary>
    public static class ScenarioDistanceMeasure
    {
        /// <summary>False — какой-то точки нет: объект не найден или не задан.</summary>
        public static bool TryMeasure(ScenarioRun run, ScenarioPoint from, ScenarioPoint to, bool isHorizontalOnly, out float distance)
        {
            distance = 0f;

            if (!from.TryGetPosition(run, out var fromPosition) || !to.TryGetPosition(run, out var toPosition))
                return false;

            var offset = toPosition - fromPosition;

            if (isHorizontalOnly)
                offset.y = 0f;

            distance = offset.magnitude;

            return true;
        }
    }


    /// <summary>Как сравнить расстояние. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioDistanceComparison
    {
        [InspectorName("Closer Than")] CloserThan,
        [InspectorName("Farther Than")] FartherThan,
        [InspectorName("Between")] Between,
        [InspectorName("Outside")] Outside
    }
}
