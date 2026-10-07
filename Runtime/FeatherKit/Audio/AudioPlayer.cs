using FeatherKit.Lifecycle;
using UnityEngine;
using UnityEngine.Audio;

namespace FeatherKit.Audio
{
    /// <summary>
    /// ЗАВОДИТСЯ КОНТЕКСТОМ САМ. Своего создавать не надо — бери готовый
    /// из <c>AppContext.Current.Audio</c>.
    ///
    /// Разовые звуки: удар, подбор, клик. Ничего не спавнит на каждый выстрел — держит
    /// готовый набор источников и крутит их по кругу:
    ///
    /// <code>
    /// Audio.Play2D(clickClip);                 // интерфейс, музыка события — без позиции
    /// Audio.Play3D(hitClip, enemy.Center);     // в точке мира, с затуханием по дистанции
    /// </code>
    ///
    /// Почему не <c>AudioSource.PlayClipAtPoint</c>: он на каждый звук создаёт объект и
    /// уничтожает его следом. В бою это десятки объектов в секунду и работа сборщику мусора.
    ///
    /// Каналы кончились — самый старый звук обрывается. Это сознательно: одновременных
    /// звуков в бою всё равно больше, чем ухо различает, а расти без предела нельзя.
    ///
    /// Нужен звук, которым надо управлять после запуска — зациклить, оборвать, таскать
    /// за объектом, — это не сюда: бери <see cref="PooledAudioSource"/> из пула, он даёт
    /// живой источник.
    /// </summary>
    public class AudioPlayer : MonoBehaviour, IReleasable
    {
        private const int DefaultChannels = 12;

        private AudioSource flat;
        private AudioSource[] channels;
        private int nextChannel;

        private float defaultMin = 4f;
        private float defaultMax = 40f;


        /// <summary>Общая громкость: множитель ко всему, что здесь играет.</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// Шина, куда идут звуки без своей группы. Ставится один раз при старте игры;
        /// сами группы живут в микшере игры, библиотека про них ничего не знает.
        /// </summary>
        public AudioMixerGroup DefaultGroup { get; set; }

        /// <summary>Сколько звуков в мире могут звучать одновременно.</summary>
        public int ChannelCount => channels?.Length ?? 0;


        // internal: проигрыватель один и заводится вместе с корневым контекстом. Второй
        // такой же просто съел бы вдвое больше каналов, не давая ничего взамен.
        internal static AudioPlayer Create(string hostName, Transform parent, int channels = DefaultChannels)
        {
            var host = new GameObject(hostName);
            host.transform.SetParent(parent, false);

            var player = host.AddComponent<AudioPlayer>();
            player.Build(Mathf.Max(1, channels));

            return player;
        }


        /// <summary>
        /// Звук без позиции: интерфейс, оповещения, всё, что слышно одинаково отовсюду.
        ///
        /// group — шина микшера, куда его сводить. Не задана — берётся <see cref="DefaultGroup"/>.
        /// </summary>
        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f, AudioMixerGroup group = null)
        {
            if (clip == null || flat == null)
                return;

            // Высота у PlayOneShot общая на источник: сменить её значит сдвинуть и всё, что
            // уже звучит. Поэтому звук с нестандартной высотой уходит на свой канал.
            if (!Mathf.Approximately(pitch, 1f))
            {
                PlayOnChannel(clip, position: null, volume, pitch, 0f, group);
                return;
            }

            flat.outputAudioMixerGroup = group != null ? group : DefaultGroup;

            // PlayOneShot, а не Play: плоскому источнику не нужны свободные каналы,
            // он смешивает сколько угодно звуков сразу.
            flat.PlayOneShot(clip, Mathf.Clamp01(volume) * Volume);
        }

        /// <summary>
        /// Звук в точке мира: громкость и панорама считаются от позиции слушателя.
        ///
        /// radius — за сколько метров его ещё слышно. Шагу хватает десятка, взрыву мало
        /// и полусотни, поэтому дальность у каждого звука своя. Ноль — общая дальность
        /// из <see cref="SetDistance"/>.
        /// </summary>
        public void Play3D(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f,
            float radius = 0f, AudioMixerGroup group = null)
        {
            if (clip == null)
                return;

            PlayOnChannel(clip, position, volume, pitch, radius, group);
        }

        // Один канал на звук. position == null — плоский звук без позиции.
        private void PlayOnChannel(AudioClip clip, Vector3? position, float volume, float pitch, float radius,
            AudioMixerGroup group)
        {
            var source = TakeChannel();
            if (source == null)
                return;

            ApplyDistance(source, radius);

            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.transform.position = position ?? Vector3.zero;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * Volume;
            source.pitch = pitch;
            source.outputAudioMixerGroup = group != null ? group : DefaultGroup;
            source.Play();
        }

        /// <summary>
        /// Тот же звук со случайным сдвигом высоты. Нужен чаще, чем кажется: один и тот же
        /// клип, повторённый десять раз подряд, слышен как заедающая пластинка.
        /// </summary>
        public void Play3DVaried(AudioClip clip, Vector3 position, float volume = 1f, float pitchSpread = 0.1f,
            float radius = 0f, AudioMixerGroup group = null)
        {
            Play3D(clip, position, volume, 1f + Random.Range(-pitchSpread, pitchSpread), radius, group);
        }

        /// <summary>
        /// Дальность по умолчанию — для звуков, которым своя не задана. min — докуда звук
        /// на полной громкости, max — где он затухает до нуля.
        /// </summary>
        public void SetDistance(float min, float max)
        {
            defaultMin = Mathf.Max(0.1f, min);
            defaultMax = Mathf.Max(defaultMin + 0.1f, max);
        }

        public void StopAll()
        {
            if (flat != null)
                flat.Stop();

            for (var i = 0; i < channels.Length; i++)
                channels[i].Stop();
        }

        /// <summary>Контекст гасит свои сервисы: звук глохнет. Объект не уничтожаем —
        /// его создал контекст, он же и унесёт.</summary>
        public void Release()
        {
            StopAll();
        }


        private void Build(int count)
        {
            flat = CreateSource("Flat", 0f);

            channels = new AudioSource[count];

            for (var i = 0; i < count; i++)
                channels[i] = CreateSource($"Channel {i}", 1f);
        }

        // Каждый канал на своём объекте: позиция звука — это позиция объекта, и одному
        // источнику пришлось бы прыгать между точками, обрывая предыдущий звук.
        private AudioSource CreateSource(string sourceName, float spatialBlend)
        {
            var host = new GameObject(sourceName);
            host.transform.SetParent(transform, false);

            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 40f;

            return source;
        }

        // Полная громкость держится на четверти радиуса: если начинать затухание прямо
        // от источника, близкий звук слышно тише, чем ждёшь.
        private void ApplyDistance(AudioSource source, float radius)
        {
            source.minDistance = radius > 0f ? radius * 0.25f : defaultMin;
            source.maxDistance = radius > 0f ? radius : defaultMax;
        }

        // Сначала ищем свободный канал, и только если все заняты — обрываем следующий
        // по кругу. Так самый старый звук уходит первым, а не случайный.
        private AudioSource TakeChannel()
        {
            for (var i = 0; i < channels.Length; i++)
            {
                var index = (nextChannel + i) % channels.Length;

                if (channels[index].isPlaying)
                    continue;

                nextChannel = (index + 1) % channels.Length;

                return channels[index];
            }

            var stolen = channels[nextChannel];
            nextChannel = (nextChannel + 1) % channels.Length;

            return stolen;
        }
    }
}
