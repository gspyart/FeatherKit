using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace FeatherKit.Scenarios
{
    /// <summary>Проиграть Timeline с начала — для постановочных сцен. Старое поле «director» переносится в новое перед работой и кнопкой «Upgrade Fields».</summary>
    [Serializable]
    [ScenarioBlock("Animation/Play Timeline", Description = "Play a Timeline from the start and wait for its end")]
    public class PlayTimelineAction : ScenarioLongAction, IScenarioUpgradableBlock
    {
        [SerializeField, HideInInspector] private PlayableDirector director;

        [Tooltip("Whose PlayableDirector. Empty — the one on the scenario object")]
        [ScenarioLabel("Timeline")]
        [SerializeField] private ScenarioValue<PlayableDirector> timeline = new ScenarioValue<PlayableDirector>();


        public ScenarioValue<PlayableDirector> Timeline
        {
            get
            {
                TryUpgradeLegacyFields();
                return timeline;
            }
        }

        public override string Summary => timeline.UsesVariable ? "graph variable"
            : timeline.Value != null ? timeline.Value.name : "this object";


        public override IEnumerator Perform(ScenarioRun run)
        {
            TryUpgradeLegacyFields();
            var targetDirector = run.ComponentOrOwners(timeline.Get(run));

            if (targetDirector == null)
                throw new InvalidOperationException("PlayableDirector not found");

            targetDirector.time = 0;
            targetDirector.Play();

            // По времени, а не только по состоянию: Timeline с Wrap Mode «Hold» не останавливается никогда.
            while (targetDirector != null
                   && targetDirector.state == PlayState.Playing
                   && targetDirector.time < targetDirector.duration)
                yield return null;
        }


        // IScenarioUpgradableBlock
        // Новое поле главнее: задано — старое просто очищается, не затирая его.
        public bool TryUpgradeLegacyFields()
        {
            if (!ScenarioFieldMigration.IsAssigned(director))
                return false;

            if (!ScenarioFieldMigration.IsAssigned(timeline.Value) && !timeline.UsesVariable)
                timeline.Value = director;

            director = null;

            return true;
        }
    }
}
