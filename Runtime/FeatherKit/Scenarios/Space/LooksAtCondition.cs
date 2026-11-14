using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Смотрящий — по умолчанию глаза игрока — смотрит на цель, в направлении или вдоль оси объекта.</summary>
    [Serializable]
    [ScenarioBlock("Space/Looks At", Description = "The looker is looking at a point, along a direction or along an object's axis")]
    public class LooksAtCondition : ScenarioCondition
    {
        [SerializeField] private ScenarioGazeCheck gaze = new ScenarioGazeCheck();


        public ScenarioGazeCheck Gaze => gaze;

        public override string Summary => gaze.Summary;


        protected override bool Check(ScenarioRun run)
        {
            return gaze.IsLooking(run);
        }
    }
}
