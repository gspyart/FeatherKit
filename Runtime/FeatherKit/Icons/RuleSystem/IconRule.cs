using System.Collections.Generic;
using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Правило показа иконок ассетом: кого ищем и что над ними рисуем — настройка, а не код.
    /// - общее для всех: смещение, поведение у края, сколько иконок разом;
    /// - как искать и чем ограничивать, решает наследник: радиус, задания, точка на карте.
    /// Спор за одну цель решает порядок правил в списке бегунка: чем выше, тем важнее.
    /// </summary>
    public abstract class IconRule : ScriptableObject
    {
        [Header("Правило")]
        [Tooltip("Смещение от центра цели, м. Вверх — иконка над головой")]
        [SerializeField] private Vector3 offset = Vector3.up;

        [Tooltip("Что делать, когда цель ушла за край экрана")]
        [SerializeField] private OffscreenMode offscreen = OffscreenMode.Hide;

        [Tooltip("Сколько иконок этого правила показывать разом. Ноль — сколько найдётся. " +
                 "Ограничение спасает от каши, когда добыча лежит кучей")]
        [SerializeField] private int maxIcons = 5;

        [Tooltip("Не спорить за цель: иконка встаёт ПОВЕРХ чужой, а не вместо неё. " +
                 "Обычное правило — одна цель, одна иконка; исключение нужно тому, что " +
                 "обязано быть видно всегда, — предупреждению об ударе рядом с полоской " +
                 "здоровья. Тогда развести их можно только высотой")]
        [SerializeField] private bool sharesTarget;

        [Tooltip("Прижимать по краю САМОЙ иконки, а не по её середине: широкая панель " +
                 "у края встанет внутрь целиком, а не наполовину за ним. Обычной иконке " +
                 "это не нужно — сдвиг уводит её от цели, а маленькая и так помещается")]
        [SerializeField] private bool clampByIconEdge;

        [Tooltip("Не наезжать на соседние иконки: сошедшиеся вплотную разъедутся " +
                 "и встанут краями с отступом. Ценой того, что иконка перестаёт висеть " +
                 "ТОЧНО над целью, — стрелке-указателю это противопоказано, она соврёт " +
                 "направлением")]
        [SerializeField] private bool avoidOverlap;


        /// <summary>Общее смещение правила. Кандидат может добавить к нему своё.</summary>
        public Vector3 Offset => offset;
        public OffscreenMode Offscreen => offscreen;
        public int MaxIcons => maxIcons;

        /// <summary>Делит ли правило цель с остальными: иконка встаёт поверх чужой, а не вместо.</summary>
        public bool SharesTarget => sharesTarget;

        /// <summary>Прижимать иконку по её краю, а не по середине: широкое окно не обрежется.</summary>
        public bool ClampByIconEdge => clampByIconEdge;

        /// <summary>Разъезжаться с соседями. Плата — иконка висит над целью не точно.</summary>
        public bool AvoidOverlap => avoidOverlap;


        // ── Поиск ─────────────────────────────────────────────────────────

        /// <summary>
        /// Работает ли правило сейчас вообще: раз на пересчёт, до обхода реестров. Общего
        /// «нет игрока — нет иконок» нет: метки бывают нужны и после смерти.
        /// </summary>
        public virtual bool CanShow(in IconRuleContext context)
        {
            return true;
        }

        /// <summary>Сложить кандидатов: над кем и что показать. Зовётся не каждый кадр.</summary>
        public abstract void Collect(List<IconCandidate> buffer, in IconRuleContext context);

        /// <summary>Показывать ли иконку прямо сейчас. Каждый кадр — поэтому только дешёвое.</summary>
        public virtual bool ShouldShow(FEntityBase entity, in IconRuleContext context)
        {
            return true;
        }
    }
}
