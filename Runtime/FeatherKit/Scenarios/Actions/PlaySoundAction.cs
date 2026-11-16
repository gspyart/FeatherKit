using System;
using System.Collections;
using FeatherKit.Logging;
using AppContext = FeatherKit.Contexts.AppContext;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Разовый звук через AudioPlayer: в точке или без места, «в голове».
    /// Старые поля «clip» и «source» переносятся в новые перед работой и кнопкой «Upgrade Fields».
    /// </summary>
    [Serializable]
    [ScenarioBlock("Sound/Play Sound", Description = "Play a sound once — at a point or everywhere equally loud")]
    public class PlaySoundAction : ScenarioLongAction, IScenarioUpgradableBlock
    {
        [SerializeField, HideInInspector] private AudioClip clip;
        [SerializeField, HideInInspector] private ScenarioTarget source;

        [ScenarioLabel("Sound")]
        [SerializeField] private ScenarioValue<AudioClip> sound = new ScenarioValue<AudioClip>();

        [Range(0f, 1f)]
        [ScenarioLabel("Volume")]
        [SerializeField] private float volume = 1f;

        [Tooltip("Plays from the point and gets quieter with distance. Unchecked — equally loud everywhere")]
        [ScenarioLabel("At Point")]
        [SerializeField] private bool isPositional = true;

        [Tooltip("Where the sound comes from when it plays at a point")]
        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint soundPoint = new ScenarioPoint(ScenarioPointKind.ThisObject);


        public ScenarioValue<AudioClip> Sound
        {
            get
            {
                TryUpgradeLegacyFields();
                return sound;
            }
        }

        public ScenarioPoint SoundPoint
        {
            get
            {
                TryUpgradeLegacyFields();
                return soundPoint;
            }
        }

        public override string Summary => sound.UsesVariable ? "graph variable" : sound.Value != null ? sound.Value.name : "no sound selected";


        public override IEnumerator Perform(ScenarioRun run)
        {
            TryUpgradeLegacyFields();
            var playedClip = sound.Get(run);

            // Не обрыв, а предупреждение: в заготовке звук часто ещё не подобран, а остальное уже работает.
            if (playedClip == null)
            {
                FLog.Warning(run.Scenario, ("Scenario ", FLogColor.Muted), (run.Scenario.name, FLogColor.Value),
                    (": no sound selected — step skipped", FLogColor.Muted));
                yield break;
            }

            var audio = AppContext.Current.Audio;

            if (isPositional)
                audio.Play3D(playedClip, RequirePosition(run), volume);
            else
                audio.Play2D(playedClip, volume);

            yield return new WaitForSeconds(playedClip.length);
        }

        private Vector3 RequirePosition(ScenarioRun run)
        {
            if (!soundPoint.TryGetPosition(run, out var position))
                throw new InvalidOperationException($"Point “{soundPoint.Summary}” not found");

            return position;
        }


        // IScenarioUpgradableBlock
        // Новое поле главнее: задано — старое просто очищается, не затирая его.
        public bool TryUpgradeLegacyFields()
        {
            var hasChanged = false;

            if (ScenarioFieldMigration.IsAssigned(clip))
            {
                if (!ScenarioFieldMigration.IsAssigned(sound.Value) && !sound.UsesVariable)
                    sound.Value = clip;

                clip = null;
                hasChanged = true;
            }

            if (source != null && !source.IsDefault)
            {
                if (soundPoint.IsDefault)
                    soundPoint = source.ToPoint();

                source = new ScenarioTarget();
                hasChanged = true;
            }

            return hasChanged;
        }
    }
}
