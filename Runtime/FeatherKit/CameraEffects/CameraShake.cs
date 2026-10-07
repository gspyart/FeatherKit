using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace FeatherKit.CameraEffects
{
    /// <summary>
    /// Тряска камеры: несколько одновременных встрясок, каждая со своим характером,
    /// направлением и затуханием.
    ///
    /// Расширением Cinemachine, а не правкой трансформа камеры: реальную камеру ведёт
    /// пайплайн, и любую правку он перезапишет в том же кадре. Расширение вклинивается
    /// в конце пайплайна и добавляет поправку к уже посчитанному кадру — это
    /// единственный законный способ.
    ///
    /// Своё, а не Impulse System, потому что у той в обычном режиме импульс ОДНОМЕРНЫЙ:
    /// одна и та же кривая вдоль одного и того же вектора, отличается только амплитуда.
    /// Сто ударов подряд трясут одинаково, и рандомить там нечего — поле Randomize
    /// работает только в legacy-режиме. Здесь у каждой встряски свои сдвиги шума, поэтому
    /// два одинаковых удара выглядят по-разному сами собой.
    ///
    /// Складывать умеет: три попадания в одном кадре дают три встряски, а не перебивают
    /// друг друга. Общий размах при этом подрезан, иначе толпа врагов вытряхнула бы
    /// камеру за пределы сцены.
    /// </summary>
    public class CameraShake : CinemachineExtension
    {
        // Одна идущая встряска. Структура, а не класс: их заводят по несколько за удар,
        // и мусорить объектами в самый горячий момент боя незачем.
        private struct ActiveShake
        {
            public ShakeProfile Profile;
            public float Force;
            public Vector3 Direction;
            public Vector3 NoiseOffsets;
            public float Elapsed;
        }


        [Tooltip("Чем трясти, если позвали без профиля")]
        [SerializeField] private ShakeProfile defaultProfile;

        [Tooltip("Дальше этого камеру не смещаем, м — сколько бы встрясок ни сложилось")]
        [Min(0f)]
        [SerializeField] private float maxPositionOffset = 0.4f;

        [Tooltip("Дальше этого не доворачиваем, градусов")]
        [Min(0f)]
        [SerializeField] private float maxRotationOffset = 4f;

        private readonly List<ActiveShake> shakes = new List<ActiveShake>();

        // Поправка этого кадра. Считается один раз в Update и только читается в пайплайне:
        // тот зовётся по нескольку раз за кадр — на блендах и на каждой камере, — и считать
        // время внутри него значило бы гнать тряску втрое быстрее.
        private Vector3 positionOffset;
        private Vector3 rotationOffset;


        /// <summary>Трясёт ли прямо сейчас.</summary>
        public bool IsShaking => shakes.Count > 0;


        // ── Запуск ────────────────────────────────────────────────────────

        /// <summary>Тряхнуть профилем по умолчанию, в случайную сторону.</summary>
        public void Shake(float force = 1f)
        {
            Shake(defaultProfile, force, Vector3.zero);
        }

        public void Shake(ShakeProfile profile, float force = 1f)
        {
            Shake(profile, force, Vector3.zero);
        }

        /// <summary>
        /// Тряхнуть вдоль направления: удар пришёл слева — камера дёрнулась влево.
        /// Осмысленное направление читается лучше случайного, а разброс поверх него
        /// задаёт сам профиль.
        ///
        /// Нулевое направление — разыграть случайное.
        /// </summary>
        public void Shake(ShakeProfile profile, float force, Vector3 direction)
        {
            if (profile == null || force <= 0f)
                return;

            direction.y = 0f;

            var heading = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Random.insideUnitSphere;

            shakes.Add(new ActiveShake
            {
                Profile = profile,
                Force = force,
                Direction = heading,

                // Вот отсюда и берётся непохожесть: у каждой встряски свой участок шума.
                NoiseOffsets = new Vector3(
                    Random.Range(0f, 1000f),
                    Random.Range(0f, 1000f),
                    Random.Range(0f, 1000f)),

                Elapsed = 0f
            });
        }

        /// <summary>Оборвать всё сейчас же: катсцена, смена сцены, конец боя.</summary>
        public void StopAll()
        {
            shakes.Clear();

            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
        }


        // ── Счёт ──────────────────────────────────────────────────────────

        // Идём с конца: отыгравшая встряска снимается на месте, и удаление не сдвигает
        // то, что ещё не посчитали.
        private void Advance(float deltaTime)
        {
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;

            for (var i = shakes.Count - 1; i >= 0; i--)
            {
                var shake = shakes[i];

                shake.Elapsed += deltaTime;

                var time = shake.Elapsed / shake.Profile.Duration;
                var amount = shake.Profile.DecayAt(time) * shake.Force;

                if (time >= 1f || amount <= 0f)
                {
                    shakes.RemoveAt(i);
                    continue;
                }

                shakes[i] = shake;

                var shape = Shape(shake);

                positionOffset += shape * (shake.Profile.PositionAmplitude * amount);
                rotationOffset += Shape(shake, 100f) * (shake.Profile.RotationAmplitude * amount);
            }

            positionOffset = Vector3.ClampMagnitude(positionOffset, maxPositionOffset);
            rotationOffset = Vector3.ClampMagnitude(rotationOffset, maxRotationOffset);
        }

        /// <summary>
        /// Форма встряски в этот миг: смесь колебания ВДОЛЬ удара и шума во все стороны.
        ///
        /// <paramref name="seed"/> разводит поворот и смещение: с одним и тем же шумом
        /// камера ездила бы и кренилась строго заодно, и тряска выглядела бы плоской.
        /// </summary>
        private static Vector3 Shape(in ActiveShake shake, float seed = 0f)
        {
            var phase = shake.Elapsed * shake.Profile.Frequency;

            // Затухающее колебание вдоль удара — то, что делает тряску направленной.
            var wave = Mathf.Sin(phase * 2f * Mathf.PI);
            var directed = shake.Direction * wave;

            // Шум по каждой оси со своим сдвигом — то, что делает её непохожей.
            var noise = new Vector3(
                Noise(shake.NoiseOffsets.x + seed, phase),
                Noise(shake.NoiseOffsets.y + seed, phase),
                Noise(shake.NoiseOffsets.z + seed, phase));

            return Vector3.Lerp(noise, directed, shake.Profile.DirectionBias);
        }

        // Перлин даёт 0..1, а нам нужно колебание вокруг нуля.
        private static float Noise(float offset, float phase)
        {
            return Mathf.PerlinNoise(offset + phase, 0f) * 2f - 1f;
        }


        // ── Cinemachine ───────────────────────────────────────────────────

        // Finalize — самый конец пайплайна: к этому моменту камера уже поставлена и
        // нацелена, и наша поправка ложится поверх, ничего не ломая.
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || shakes.Count == 0)
                return;

            state.PositionCorrection += positionOffset;
            state.OrientationCorrection *= Quaternion.Euler(rotationOffset);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (shakes.Count == 0)
                return;

            // Реальное время: тряска — это отклик на удар, и в замедлении она должна
            // доигрывать, а не растягиваться вместе с ним.
            Advance(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            StopAll();
        }
    }
}
