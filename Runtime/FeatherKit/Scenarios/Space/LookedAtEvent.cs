using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Смотрящий начал смотреть, перестал или смотрит — и держит это не меньше Hold Time. «Caller» — смотрящий.
    /// - начало и конец — переходы: смотрел ещё до подписки — «начал» считается с первой проверки
    /// - «While Looking» срабатывает на каждой проверке, пока смотрит
    /// </summary>
    [Serializable]
    [ScenarioBlock("Space/Looked At", Description = "Fires when the looker starts or stops looking at a target, or while it looks")]
    public class LookedAtEvent : ScenarioPolledEvent
    {
        [SerializeField] private ScenarioGazeCheck gaze = new ScenarioGazeCheck();

        [ScenarioLabel("When")]
        [SerializeField] private ScenarioLookMoment moment;

        [Tooltip("How long the looking (or not looking) must last before the event fires, seconds")]
        [Min(0f)]
        [ScenarioLabel("Hold Time, s")]
        [SerializeField] private float holdTime;

        [NonSerialized] private bool isLooking;
        [NonSerialized] private float stateSince;
        [NonSerialized] private bool hasFiredForState;


        public ScenarioGazeCheck Gaze => gaze;

        public ScenarioLookMoment Moment
        {
            get => moment;
            set => moment = value;
        }

        public float HoldTime
        {
            get => holdTime;
            set => holdTime = Mathf.Max(0f, value);
        }

        public override string Summary => moment switch
        {
            ScenarioLookMoment.StartsLooking => "starts · ",
            ScenarioLookMoment.StopsLooking => "stops · ",
            _ => "while · "
        } + gaze.Summary;


        // «Не смотрел» на старте — без срабатывания: «перестал смотреть» требует, чтобы сначала смотрел.
        protected override void ResetPollState()
        {
            isLooking = false;
            stateSince = Time.time;
            hasFiredForState = true;
        }

        protected override void CheckNow(ScenarioRun probe)
        {
            var isLookingNow = gaze.IsLooking(probe);

            if (isLookingNow != isLooking)
            {
                isLooking = isLookingNow;
                stateSince = Time.time;
                hasFiredForState = false;
            }

            if (Time.time - stateSince < holdTime || !ShouldFire())
                return;

            hasFiredForState = true;

            var lookerTransform = gaze.Looker.ResolveTransform(probe);
            Fire(lookerTransform != null ? lookerTransform.gameObject : null);
        }

        private bool ShouldFire()
        {
            return moment switch
            {
                ScenarioLookMoment.StartsLooking => isLooking && !hasFiredForState,
                ScenarioLookMoment.StopsLooking => !isLooking && !hasFiredForState,
                _ => isLooking
            };
        }
    }


    /// <summary>Когда срабатывает Looked At. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioLookMoment
    {
        [InspectorName("Starts Looking")] StartsLooking,
        [InspectorName("Stops Looking")] StopsLooking,
        [InspectorName("While Looking")] WhileLooking
    }
}
