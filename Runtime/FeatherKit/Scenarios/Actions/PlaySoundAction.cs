using System;
using System.Collections;
using FeatherKit.Logging;
using AppContext = FeatherKit.Contexts.AppContext;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Разовый звук через AudioPlayer: в точке объекта или без места, «в голове».</summary>
    [Serializable]
    [ScenarioBlock("Sound/Play Sound")]
    public class PlaySoundAction : ScenarioLongAction
    {
        [ScenarioLabel("Sound")]
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [ScenarioLabel("Volume")]
        [SerializeField] private float volume = 1f;

        [Tooltip("Plays from the object's position and gets quieter with distance. Unchecked — equally loud everywhere")]
        [ScenarioLabel("At Object Position")]
        [SerializeField] private bool isPositional = true;

        [Tooltip("Where the sound comes from when it plays at the object's position")]
        [ScenarioLabel("Source")]
        [SerializeField] private ScenarioTarget source = new ScenarioTarget();


        public override string Summary => clip != null ? clip.name : "no sound selected";


        public override IEnumerator Perform(ScenarioRun run)
        {
            // Не обрыв, а предупреждение: в заготовке звук часто ещё не подобран, а остальное уже работает.
            if (clip == null)
            {
                FLog.Warning(run.Scenario, ("Scenario ", FLogColor.Muted), (run.Scenario.name, FLogColor.Value),
                    (": no sound selected — step skipped", FLogColor.Muted));
                yield break;
            }

            var audio = AppContext.Current.Audio;

            if (isPositional)
                audio.Play3D(clip, source.ResolveRequired(run).transform.position, volume);
            else
                audio.Play2D(clip, volume);

            yield return new WaitForSeconds(clip.length);
        }
    }
}
