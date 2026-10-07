using UnityEngine;

namespace FeatherKit.CameraEffects
{
    /// <summary>
    /// Характер одной тряски: насколько сильно, как часто и как долго трясёт.
    ///
    /// Ассетом, а не полями на месте вызова: «лёгкий тычок», «удар молота» и «взрыв» —
    /// это три штуки на всю игру, и подбирают их один раз глазами. Записанные в каждое
    /// оружие, те же числа разъехались бы, и одинаковые по смыслу удары начали бы трястись
    /// по-разному без всякой причины.
    ///
    /// Сила при этом задаётся НЕ здесь, а на месте: один профиль, помноженный на силу,
    /// покрывает и слабый удар ножом, и тот же удар в спину.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Camera/Shake Profile", fileName = "Shake_")]
    public class ShakeProfile : ScriptableObject
    {
        [Tooltip("Насколько СМЕЩАЕТ камеру, м. На далёкой камере смещение почти не видно — " +
                 "основную работу делает поворот ниже")]
        [Min(0f)]
        [SerializeField] private float positionAmplitude = 0.08f;

        [Tooltip("Насколько ДОВОРАЧИВАЕТ камеру, градусов. Именно это и читается как тряска: " +
                 "полградуса уже заметно, три — как взрыв под ногами")]
        [Min(0f)]
        [SerializeField] private float rotationAmplitude = 0.8f;

        [Tooltip("Как часто колеблется, Гц. 20-30 — резкая дрожь удара, 5-8 — тяжёлое " +
                 "раскачивание")]
        [Min(0.01f)]
        [SerializeField] private float frequency = 22f;

        [Tooltip("Сколько длится, РЕАЛЬНЫХ секунд. Реальных, потому что тряска — это отклик " +
                 "на удар, и она должна доигрывать даже в замедлении времени")]
        [Min(0.01f)]
        [SerializeField] private float duration = 0.3f;

        [Tooltip("Как затухает: по X — доля длительности, по Y — сила. Резкий старт " +
                 "и плавный спад читаются как удар, ровная полка — как землетрясение")]
        [SerializeField] private AnimationCurve decay = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));

        [Tooltip("Насколько трясти ВДОЛЬ удара: 0 — чистый шум во все стороны, 1 — строго " +
                 "по направлению. Середина обычно и нужна: удар читается, но два одинаковых " +
                 "удара выглядят по-разному")]
        [Range(0f, 1f)]
        [SerializeField] private float directionBias = 0.5f;


        public float PositionAmplitude => positionAmplitude;
        public float RotationAmplitude => rotationAmplitude;
        public float Frequency => frequency;
        public float Duration => duration;
        public float DirectionBias => directionBias;


        /// <summary>
        /// Сила в этот момент тряски. <paramref name="time"/> — доля от длительности.
        /// За её пределами ноль: тряска кончилась.
        /// </summary>
        public float DecayAt(float time)
        {
            if (time >= 1f)
                return 0f;

            return decay != null && decay.length > 0 ? Mathf.Max(0f, decay.Evaluate(time)) : 1f - time;
        }
    }
}
