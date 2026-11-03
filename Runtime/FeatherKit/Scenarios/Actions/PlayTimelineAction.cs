using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace FeatherKit.Scenarios
{
    /// <summary>Проиграть Timeline с начала — для постановочных сцен.</summary>
    [Serializable]
    [ScenarioBlock("Animation/Play Timeline")]
    public class PlayTimelineAction : ScenarioLongAction
    {
        [Tooltip("Whose PlayableDirector. Empty — the one on the scenario object")]
        [ScenarioLabel("Timeline")]
        [SerializeField] private PlayableDirector director;


        public override string Summary => director != null ? director.name : "this object";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var targetDirector = run.ComponentOrOwners(director);

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
    }
}
