using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Как летит надпись. Расстояния — в единицах канваса, не в метрах.
    /// Время в кривых нормализовано: 0 — появление, 1 — конец жизни.
    /// Поля *Random - разброс в долях: 0.2 = плюс-минус 20%, 0 = без разброса.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingTextAnimation", menuName = "FeatherKit/Floating Text Animation")]
    public class FloatingTextAnimationConfig : ScriptableObject
    {
        /// <summary> Разыгранные значения для одного текста: дальше он живёт по ним. </summary>
        public struct Variation
        {
            public float LifeTime;
            public Vector2 SpawnOffset;
            public float MoveUp;
            public float MoveSide;
            public float Scale;
            public float StartRotation;
            public float Rotation;
        }

        [Header("Жизнь")]
        [Min(0.01f)]
        public float LifeTime = 0.8f;

        [Range(0f, 1f)]
        public float LifeTimeRandom;

        [Header("Появление")]
        [Tooltip("Постоянное смещение от точки в мире.")]
        public Vector2 SpawnOffset;

        [Tooltip("Случайный разброс, чтобы тексты не ложились друг на друга.")]
        public Vector2 SpawnRandomRadius = new Vector2(45f, 25f);

        [Header("Движение вверх")]
        public float MoveUpDistance = 130f;

        [Range(0f, 1f)]
        public float MoveUpRandom = 0.15f;

        [Tooltip("X - время жизни, Y - доля пройденного пути.")]
        public AnimationCurve MoveUpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Движение вбок")]
        [Tooltip("0 - летит строго вверх.")]
        public float MoveSideDistance = 25f;

        [Range(0f, 1f)]
        public float MoveSideRandom = 0.5f;

        [Tooltip("Кидать влево/вправо случайно. Иначе всегда вправо.")]
        public bool RandomSideDirection = true;

        public AnimationCurve MoveSideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Масштаб")]
        [Min(0.01f)]
        public float ScaleMultiplier = 1f;

        [Range(0f, 1f)]
        public float ScaleRandom;

        [Tooltip("Классический 'поп': вырос -> просел до нормы -> схлопнулся.")]
        public AnimationCurve ScaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.12f, 1.15f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.85f, 1f),
            new Keyframe(1f, 0.7f));

        [Header("Вращение")]
        [Tooltip("Случайный наклон в момент появления, плюс-минус градусы. 0 - текст ровный.")]
        public float StartRotationRandom;

        [Tooltip("На сколько градусов текст довернёт за жизнь. 0 - не крутится.")]
        public float RotationAngle;

        [Range(0f, 1f)]
        public float RotationRandom;

        [Tooltip("Крутить в обе стороны случайно. Иначе всегда по часовой.")]
        public bool RandomRotationDirection = true;

        public AnimationCurve RotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Цвет")]
        [Tooltip("Появиться этим цветом и перейти к цвету из префаба. Выключено - всегда цвет префаба.")]
        public bool UseStartColor;

        public Color StartColor = Color.white;

        [Tooltip("0 - стартовый цвет, 1 - цвет префаба. Прозрачность берётся отдельно, из AlphaCurve.")]
        public AnimationCurve ColorCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 1f));

        [Header("Прозрачность")]
        [Tooltip("Держим 1 подольше и гасим в конце, иначе текст мылится весь полёт.")]
        public AnimationCurve AlphaCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0f));

        /// <summary> Разыгрывает конкретные значения для одного текста. Зовётся один раз при спавне. </summary>
        public Variation CreateVariation()
        {
            float sideSign = !RandomSideDirection || Random.value < 0.5f ? 1f : -1f;
            float rotationSign = !RandomRotationDirection || Random.value < 0.5f ? 1f : -1f;

            return new Variation
            {
                LifeTime = Mathf.Max(Spread(LifeTime, LifeTimeRandom), 0.01f),
                MoveUp = Spread(MoveUpDistance, MoveUpRandom),
                MoveSide = Spread(MoveSideDistance, MoveSideRandom) * sideSign,
                Scale = Mathf.Max(Spread(ScaleMultiplier, ScaleRandom), 0.01f),
                StartRotation = Random.Range(-StartRotationRandom, StartRotationRandom),
                Rotation = Spread(RotationAngle, RotationRandom) * rotationSign,
                SpawnOffset = SpawnOffset + new Vector2(
                    Random.Range(-SpawnRandomRadius.x, SpawnRandomRadius.x),
                    Random.Range(-SpawnRandomRadius.y, SpawnRandomRadius.y))
            };
        }

        public Vector2 EvaluateOffset(in Variation variation, float normalizedTime)
        {
            float up = MoveUpCurve.Evaluate(normalizedTime) * variation.MoveUp;
            float side = MoveSideCurve.Evaluate(normalizedTime) * variation.MoveSide;

            return new Vector2(side, up);
        }

        public float EvaluateScale(in Variation variation, float normalizedTime)
        {
            return ScaleCurve.Evaluate(normalizedTime) * variation.Scale;
        }

        public float EvaluateRotation(in Variation variation, float normalizedTime)
        {
            return variation.StartRotation + RotationCurve.Evaluate(normalizedTime) * variation.Rotation;
        }

        /// <summary> Цвет вместе с прозрачностью. prefabColor - исходный цвет из TMP префаба. </summary>
        public Color EvaluateColor(Color prefabColor, float normalizedTime)
        {
            Color result = prefabColor;

            if (UseStartColor)
                result = Color.Lerp(StartColor, prefabColor, Mathf.Clamp01(ColorCurve.Evaluate(normalizedTime)));

            result.a = Mathf.Clamp01(AlphaCurve.Evaluate(normalizedTime));

            return result;
        }

        private static float Spread(float value, float randomness)
        {
            if (randomness <= 0f)
                return value;

            return value * Random.Range(1f - randomness, 1f + randomness);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SpawnRandomRadius = new Vector2(Mathf.Abs(SpawnRandomRadius.x), Mathf.Abs(SpawnRandomRadius.y));
        }
#endif
    }
}
