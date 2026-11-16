using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Расстояние пересекло порог: стало меньше или больше. «Caller» — объект первой точки.
    /// - срабатывает на пересечении, а не пока условие верно; снова — только отойдя на запас 0,25 м назад
    /// - запас гасит дребезг: игрок, стоящий на самой границе, не дёргает событие каждую проверку
    /// </summary>
    [Serializable]
    [ScenarioBlock("Space/Distance Crossed", Description = "Fires when the distance between two points crosses a threshold")]
    public class DistanceCrossedEvent : ScenarioPolledEvent
    {
        private const float Hysteresis = 0.25f;

        [ScenarioLabel("From")]
        [SerializeField] private ScenarioPoint from = new ScenarioPoint(ScenarioPointKind.Player);

        [ScenarioLabel("To")]
        [SerializeField] private ScenarioPoint to = new ScenarioPoint(ScenarioPointKind.ThisObject);

        [ScenarioLabel("When")]
        [SerializeField] private ScenarioDistanceCrossing crossing;

        [Min(0f)]
        [ScenarioLabel("Distance, m")]
        [SerializeField] private float distance = 3f;

        [Tooltip("Ignore the height difference: measure on the floor plan")]
        [ScenarioLabel("Horizontal Only")]
        [SerializeField] private bool isHorizontalOnly;

        [NonSerialized] private bool hasMeasured;
        [NonSerialized] private bool isCloser;


        public ScenarioPoint From => from;

        public ScenarioPoint To => to;

        public ScenarioDistanceCrossing Crossing
        {
            get => crossing;
            set => crossing = value;
        }

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Max(0f, value);
        }

        public bool IsHorizontalOnly
        {
            get => isHorizontalOnly;
            set => isHorizontalOnly = value;
        }

        public override string Summary => (crossing == ScenarioDistanceCrossing.ComesCloserThan ? "closer than " : "farther than ")
                                          + $"{distance:0.##} m";


        protected override void ResetPollState()
        {
            hasMeasured = false;
        }

        // Первое измерение только запоминает, где мы: событие — про пересечение, а не про начальное место.
        protected override void CheckNow(ScenarioRun probe)
        {
            if (!ScenarioDistanceMeasure.TryMeasure(probe, from, to, isHorizontalOnly, out var measured))
                return;

            if (!hasMeasured)
            {
                hasMeasured = true;
                isCloser = measured < distance;
                return;
            }

            if (!isCloser && measured < (crossing == ScenarioDistanceCrossing.ComesCloserThan ? distance : distance - Hysteresis))
                ChangeSide(probe, toCloser: true);
            else if (isCloser && measured > (crossing == ScenarioDistanceCrossing.MovesFartherThan ? distance : distance + Hysteresis))
                ChangeSide(probe, toCloser: false);
        }

        private void ChangeSide(ScenarioRun probe, bool toCloser)
        {
            isCloser = toCloser;

            if (toCloser != (crossing == ScenarioDistanceCrossing.ComesCloserThan))
                return;

            var fromTransform = from.ResolveTransform(probe);
            Fire(fromTransform != null ? fromTransform.gameObject : null);
        }
    }


    /// <summary>Какое пересечение ловить. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioDistanceCrossing
    {
        [InspectorName("Comes Closer Than")] ComesCloserThan,
        [InspectorName("Moves Farther Than")] MovesFartherThan
    }
}
