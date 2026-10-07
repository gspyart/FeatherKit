using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Предложение правила: «над этим стоит показать вот это».
    /// - префаб в кандидате, а не в правиле: одно правило показывает разный вид в разных случаях;
    /// - цель необязательна: кандидат держится и за точку мира — метку эвакуации, место высадки;
    /// - своя прибавка к высоте, а не готовая точка: абсолютная отстала бы от бегущей цели.
    /// </summary>
    public readonly struct IconCandidate
    {
        /// <summary>Кто предложил. Нужен, чтобы разрешить спор за цель по приоритету.</summary>
        public readonly IconRule Rule;

        /// <summary>Над кем. Null — кандидат держится за <see cref="Point"/>.</summary>
        public readonly FEntityBase Entity;

        public readonly Vector3 Point;

        /// <summary>Что показать. Выбирает правило — на каждого кандидата своё.</summary>
        public readonly RectTransform Prefab;

        /// <summary>Чем подменить у края экрана. Пусто — останется та же самая.</summary>
        public readonly RectTransform EdgePrefab;

        /// <summary>Прибавка к смещению правила для ЭТОЙ цели, м: складывается с ним, а не заменяет.
        /// Ноль — висит как все.</summary>
        public readonly Vector3 Offset;

        /// <summary>Чем различать точки одного правила. Null — правило и есть ключ.</summary>
        private readonly object pointKey;


        public IconCandidate(IconRule rule, FEntityBase entity, RectTransform prefab,
            RectTransform edgePrefab = null, Vector3 offset = default)
        {
            Rule = rule;
            Entity = entity;
            Point = Vector3.zero;
            Prefab = prefab;
            EdgePrefab = edgePrefab;
            Offset = offset;
            pointKey = null;
        }

        /// <summary>Кандидат в точке мира. <paramref name="key"/> отличает точки друг от друга: без него
        /// три метки эвакуации одного правила слились бы в одну.</summary>
        public IconCandidate(IconRule rule, Vector3 point, RectTransform prefab,
            RectTransform edgePrefab = null, object key = null, Vector3 offset = default)
        {
            Rule = rule;
            Entity = null;
            Point = point;
            Prefab = prefab;
            EdgePrefab = edgePrefab;
            Offset = offset;
            pointKey = key;
        }


        public bool IsValid => Rule != null && Prefab != null;

        /// <summary>Ключ спора за место: одна цель — одна иконка. Неоспаривающее правило добавляет в ключ
        /// себя — его иконка встаёт рядом с чужой; ключ стабилен между пересчётами.</summary>
        public object Key => Rule != null && Rule.SharesTarget ? (object)(Rule, TargetKey) : TargetKey;

        // За что держится кандидат: сущность, своя точка или само правило.
        private object TargetKey => Entity != null ? (object)Entity : pointKey ?? Rule;
    }
}
