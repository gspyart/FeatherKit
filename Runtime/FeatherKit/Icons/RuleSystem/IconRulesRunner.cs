using System.Collections.Generic;
using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Верхний слой иконок: решает, ЧТО показать и над кем, для заданного снаружи наблюдателя.
    /// - крутит правила-ассеты и собирает кандидатов, редко: обход реестров дорог;
    /// - разбирает спор за цель: одна цель — одна иконка, побеждает правило выше в списке;
    /// - держит показанное в согласии с найденным, заявками к <see cref="IconsTracker"/>.
    /// </summary>
    public class IconRulesRunner : MonoBehaviour
    {
        [Tooltip("Кто показывает иконки. Пусто — ищется на этом же объекте")]
        [SerializeField] private IconsTracker icons;

        [Tooltip("Правила сверху вниз: чем выше, тем важнее. Когда на одну цель " +
                 "претендуют двое, побеждает то, что стоит раньше")]
        [SerializeField] private List<IconRule> rules = new List<IconRule>();

        [Tooltip("Как часто пересматривать, кому положена иконка, сек. Ноль — каждый кадр. " +
                 "Десятые доли человеку незаметны, а обход реестров стоит дороже")]
        [SerializeField] private float refreshInterval = 0.2f;

        private readonly List<IconCandidate> candidates = new List<IconCandidate>();
        private readonly Dictionary<object, IconCandidate> best = new Dictionary<object, IconCandidate>();
        private readonly Dictionary<object, TrackedIcon> shown = new Dictionary<object, TrackedIcon>();

        // Кто завёл иконку. Нужно, чтобы заметить смену правила над той же целью: вид
        // должен смениться, а не остаться от прежнего.
        private readonly Dictionary<object, IconRule> owners = new Dictionary<object, IconRule>();
        private readonly List<object> toRemove = new List<object>();

        // Место правила в списке: чем меньше число, тем важнее. Собирается на каждом
        // проходе — список правил можно поменять и на ходу.
        private readonly Dictionary<IconRule, int> ranks = new Dictionary<IconRule, int>();

        private float timeLeft;
        private FEntityBase viewer;


        /// <summary>
        /// Для кого крутим правила. Ставит снаружи тот, кто знает, кто сейчас смотрит:
        /// бегунок живёт в ките и про игру не знает. Пусто — показывать некому.
        /// </summary>
        public FEntityBase Viewer
        {
            get => viewer;
            set
            {
                viewer = value;

                // Висящие иконки тоже переходят к новому: их кнопки действуют от его имени.
                foreach (var icon in shown.Values)
                {
                    if (icon != null)
                        icon.Request.Viewer = value;
                }
            }
        }


        // ── Пересчёт ──────────────────────────────────────────────────────

        /// <summary>Пересобрать иконки немедленно: подобрали предмет, началась волна.</summary>
        public void Refresh()
        {
            if (icons == null)
                return;

            var context = new IconRuleContext(Viewer);

            Collect(context);
            ResolveByPriority();
            Sync();
        }

        private void Collect(in IconRuleContext context)
        {
            candidates.Clear();
            ranks.Clear();

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

                if (rule == null)
                    continue;

                ranks[rule] = i;

                // Нужен ли правилу игрок и всё остальное — решает оно само.
                if (!rule.CanShow(context))
                    continue;

                var from = candidates.Count;

                rule.Collect(candidates, context);

                // Обрезаем хвост правила, а не общий список: лимит у каждого свой.
                var limit = rule.MaxIcons;

                if (limit > 0 && candidates.Count - from > limit)
                    candidates.RemoveRange(from + limit, candidates.Count - from - limit);
            }
        }

        // Одна цель — одна иконка. Спорят правила, а не кандидаты: побеждает то, что стоит
        // в списке выше. Два кандидата от одного правила на одну цель — берётся первый.
        private void ResolveByPriority()
        {
            best.Clear();

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];

                if (!candidate.IsValid)
                    continue;

                if (!best.TryGetValue(candidate.Key, out var current) || RankOf(candidate) < RankOf(current))
                    best[candidate.Key] = candidate;
            }
        }

        // Правила нет в списке — считаем самым неважным: такой кандидат мог прийти
        // от правила, которое сняли на ходу.
        private int RankOf(IconCandidate candidate)
        {
            return ranks.TryGetValue(candidate.Rule, out var rank) ? rank : int.MaxValue;
        }

        private void Sync()
        {
            DropRemoved();

            foreach (var pair in best)
            {
                // Уже показана — но вид мог смениться: сумка заполнилась, и над тем же
                // предметом теперь другая иконка. Обновляем заявку, а не заводим вторую.
                if (shown.TryGetValue(pair.Key, out var existing))
                {
                    existing.Request.Prefab = pair.Value.Prefab;
                    existing.Request.EdgePrefab = pair.Value.EdgePrefab;

                    continue;
                }

                var icon = Show(pair.Value);

                if (icon == null)
                    continue;

                shown[pair.Key] = icon;
                owners[pair.Key] = pair.Value.Rule;
            }
        }

        // Снимаем то, что выпало из списка, сменило правило или чью цель уже уничтожили.
        // Ключи собираем в буфер: словарь нельзя править прямо во время обхода.
        private void DropRemoved()
        {
            toRemove.Clear();

            foreach (var pair in shown)
            {
                var icon = pair.Value;
                var alive = icon != null && !icon.IsReleased;

                var owner = owners.TryGetValue(pair.Key, out var rule) ? rule : null;

                if (!alive || !best.TryGetValue(pair.Key, out var candidate) || candidate.Rule != owner)
                    toRemove.Add(pair.Key);
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                if (shown.TryGetValue(toRemove[i], out var icon) && icon != null)
                    icon.Release();

                shown.Remove(toRemove[i]);
                owners.Remove(toRemove[i]);
            }
        }

        private TrackedIcon Show(IconCandidate candidate)
        {
            var rule = candidate.Rule;

            var request = new IconRequest
            {
                Prefab = candidate.Prefab,
                EdgePrefab = candidate.EdgePrefab,

                // Смещение правила плюс поправка кандидата: «над макушкой» настраивают
                // в ассете один раз, а рост цели правило добавляет сверху.
                Offset = rule.Offset + candidate.Offset,
                Offscreen = rule.Offscreen,
                ClampByIconEdge = rule.ClampByIconEdge,
                AvoidOverlap = rule.AvoidOverlap,
                Viewer = Viewer,

                // Условие спрашивают каждый кадр: «в радиусе», «ещё не подобран» меняется
                // быстрее, чем мы обходим реестры.
                Condition = entity => rule.ShouldShow(entity, new IconRuleContext(Viewer))
            };

            return candidate.Entity != null
                ? icons.Attach(candidate.Entity, request)
                : icons.Attach(candidate.Point, request);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (icons == null)
                icons = GetComponent<IconsTracker>();
        }

        private void Update()
        {
            timeLeft -= Time.deltaTime;

            if (timeLeft > 0f)
                return;

            timeLeft = refreshInterval;

            Refresh();
        }
    }
}
