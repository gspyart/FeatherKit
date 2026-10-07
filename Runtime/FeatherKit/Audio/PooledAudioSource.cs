using System.Collections;
using FeatherKit.Pooling;
using UnityEngine;

namespace FeatherKit.Audio
{
    /// <summary>
    /// Источник звука из пула: достал, дал клип — он проиграет и вернётся в пул сам.
    /// Отдельный проигрыватель-обёртка не нужен, звать некого:
    ///
    /// <code>pool.Get(audioPrefab, point, Quaternion.identity).Play(clip);</code>
    ///
    /// Настройки громкости, спада по дистанции и микшера живут на префабе — здесь только
    /// клип и громкость конкретного проигрывания.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioSource : MonoBehaviour, IPoolable
    {
        private AudioSource source;
        private PoolManager owner;
        private Coroutine releaseRoutine;


        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || source == null)
                return;

            StopReleaseRoutine();

            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();

            // Срок — по реальной длительности: при другой высоте клип играет дольше или короче.
            var pitch = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            releaseRoutine = StartCoroutine(ReleaseAfter(clip.length / pitch));
        }

        // Второй Play до возврата в пул не должен оставить прежний таймер: он вернул бы
        // источник в пул дважды.
        private void StopReleaseRoutine()
        {
            if (releaseRoutine == null)
                return;

            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }


        protected virtual void Awake()
        {
            source = GetComponent<AudioSource>();
        }


        // Реальное время: звук играет независимо от timeScale, и на паузе обычный
        // WaitForSeconds не досчитал бы — источник завис бы занятым навсегда.
        private IEnumerator ReleaseAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (owner != null)
                owner.Release(this);
            else
                Destroy(gameObject);
        }


        // IPoolable — явно: снаружи, кроме пула, эти хуки дёргать не должны
        void IPoolable.OnSpawned(PoolManager pool)
        {
            owner = pool;
        }

        void IPoolable.OnDespawned()
        {
            StopReleaseRoutine();

            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }
}
