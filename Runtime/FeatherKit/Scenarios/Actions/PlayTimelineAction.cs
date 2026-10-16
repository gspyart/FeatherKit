using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace FeatherKit.Scenarios
{
    /// <summary>Проиграть Timeline с начала — для постановочных сцен.</summary>
    [Serializable]
    [ScenarioBlock("Анимация/Проиграть Timeline")]
    public class PlayTimelineAction : ScenarioAction
    {
        [Tooltip("Чей PlayableDirector. Пусто — на объекте сценария")]
        [ScenarioLabel("Timeline")]
        [SerializeField] private PlayableDirector director;

        [Tooltip("Следующий шаг начнётся, когда Timeline доиграет")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd = true;


        public override string Summary => (director != null ? director.name : "этот объект")
                                          + (waitsForEnd ? " · ждать конца" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            var targetDirector = run.ComponentOrOwners(director);

            if (targetDirector == null)
                throw new InvalidOperationException("PlayableDirector не найден");

            targetDirector.time = 0;
            targetDirector.Play();

            if (!waitsForEnd)
                yield break;

            // По времени, а не только по состоянию: Timeline с Wrap Mode «Hold» не останавливается никогда.
            while (targetDirector != null
                   && targetDirector.state == PlayState.Playing
                   && targetDirector.time < targetDirector.duration)
                yield return null;
        }
    }
}
