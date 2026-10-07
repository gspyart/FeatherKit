using FeatherKit.Entities;
using FeatherKit.Pooling;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Одноразовый эффект из пула: вспышка попадания, дымок смерти, взрыв. Здесь же живёт
    /// и долгий эффект — аура бафа, ветер за спиной: он тоже показывается один раз и один
    /// раз уходит в пул, разница только в сроке.
    ///
    /// Сколько жить — спрашивает у самой системы частиц, а не берёт из инспектора: руками
    /// выставленное время рано или поздно разъезжается с настройками партикла, и эффект
    /// либо обрубается на середине, либо висит пустым.
    ///
    /// Кроме одного случая: ЗАЦИКЛЕННЫЙ партикл не кончится никогда, и спрашивать у него
    /// нечего. Тогда срок берётся из поля рядом — и это единственный способ сделать ауру
    /// на пятнадцать секунд, не переписывая длину эмиссии в каждой системе префаба.
    /// </summary>
    public class PooledEffect : FEntityBase
    {
        [Tooltip("Сколько жить, когда партиклы ответить не могут: их нет вовсе (один звук, " +
                 "вспышка света) или среди них есть зацикленный. Этим же полем задают срок " +
                 "ауре и шлейфу бафа. С обычным конечным партиклом не используется")]
        [SerializeField] private float fallbackLifetime = 0.4f;

        [Tooltip("Запас поверх длительности партикла, сек — чтобы хвост шлейфа не обрубался")]
        [SerializeField] private float tailPadding = 0.1f;

        private ParticleSystem[] systems;
        private float lifetime;
        private float timeLeft;
        private bool playing;


        protected override void Awake()
        {
            base.Awake();

            systems = GetComponentsInChildren<ParticleSystem>(true);
            lifetime = CalculateLifetime();
        }

        // Запускается сам: эффект достали из пула — значит, его хотят видеть. Отдельный
        // Play был бы лишним шагом, который однажды забудут сделать.
        protected override void OnSpawned()
        {
            base.OnSpawned();

            timeLeft = lifetime;
            playing = true;

            for (var i = 0; i < systems.Length; i++)
                systems[i].Play(true);
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();

            playing = false;

            // Чистим следом: инстанс переиспользуется, и старые частицы всплыли бы
            // в новом месте в первом же кадре.
            for (var i = 0; i < systems.Length; i++)
                systems[i].Clear(true);
        }


        private float CalculateLifetime()
        {
            // Партиклов нет вовсе, или среди них есть бессрочный — время сказать некому,
            // и его задают руками. Спрашивается это ДО подсчёта: у зацикленной системы
            // тоже есть своя длина, и в общем подсчёте она давала бы полсекунды вместо
            // поля из инспектора.
            if (systems.Length == 0 || HasEndlessSystem())
                return Mathf.Max(fallbackLifetime, 0.01f);

            var longest = 0f;

            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;

                longest = Mathf.Max(longest,
                    main.duration + main.startLifetime.constantMax + main.startDelay.constantMax);
            }

            // Самая долгая ветка: пока идёт эмиссия плюс жизнь последней родившейся частицы.
            return Mathf.Max(longest + tailPadding, 0.01f);
        }

        // Одного зацикленного хватает, чтобы бессрочным стал весь эффект: он продолжит
        // сыпать частицы и после того, как отыграли все остальные.
        private bool HasEndlessSystem()
        {
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i].main.loop)
                    return true;
            }

            return false;
        }


        private void Update()
        {
            if (!playing)
                return;

            // Реальное время: эффект — это фидбек, он доигрывает и на паузе.
            timeLeft -= Time.unscaledDeltaTime;

            if (timeLeft <= 0f)
                ReturnToPool();
        }
    }
}
