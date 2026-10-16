using System;
using System.Collections;
using FeatherKit.Logging;
using AppContext = FeatherKit.Contexts.AppContext;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Разовый звук через AudioPlayer: в точке объекта или без места, «в голове».</summary>
    [Serializable]
    [ScenarioBlock("Звук/Проиграть звук")]
    public class PlaySoundAction : ScenarioAction
    {
        [ScenarioLabel("Звук")]
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [ScenarioLabel("Громкость")]
        [SerializeField] private float volume = 1f;

        [Tooltip("Звучит из точки объекта и тише вдали. Без галки — одинаково громко везде")]
        [ScenarioLabel("В точке объекта")]
        [SerializeField] private bool isPositional = true;

        [Tooltip("Откуда звучит, если звук в точке объекта")]
        [ScenarioLabel("Откуда")]
        [SerializeField] private ScenarioTarget source = new ScenarioTarget();

        [Tooltip("Следующий шаг начнётся, когда звук доиграет")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd;


        public override string Summary => clip != null
            ? clip.name + (waitsForEnd ? " · ждать конца" : "")
            : "звук не выбран";


        public override IEnumerator Perform(ScenarioRun run)
        {
            // Не обрыв, а предупреждение: в заготовке звук часто ещё не подобран, а остальное уже работает.
            if (clip == null)
            {
                FLog.Warning(run.Scenario, ("Сценарий ", FLogColor.Muted), (run.Scenario.name, FLogColor.Value),
                    (": звук не выбран — шаг пропущен", FLogColor.Muted));
                yield break;
            }

            var audio = AppContext.Current.Audio;

            if (isPositional)
                audio.Play3D(clip, source.ResolveRequired(run).transform.position, volume);
            else
                audio.Play2D(clip, volume);

            if (waitsForEnd)
                yield return new WaitForSeconds(clip.length);
        }
    }
}
