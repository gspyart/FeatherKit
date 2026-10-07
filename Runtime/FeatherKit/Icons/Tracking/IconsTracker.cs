using System.Collections.Generic;
using FeatherKit.Entities;
using FeatherKit.Helpers;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Средний слой иконок: держит иконку над целью, пока её не снимут. Заявку дают один раз.
    /// - каждый кадр считает точку от центра сущности и спрашивает условие показа;
    /// - замечает, что цель пропала, и убирает иконку;
    /// - связывает с целью все <see cref="IconView"/> на корне иконки.
    /// Рисует <see cref="IconsRenderer"/>, решает «кому и что» <see cref="IconRulesRunner"/>.
    /// </summary>
    public class IconsTracker : MonoBehaviour
    {
        [Tooltip("Кто рисует. Пусто — ищется на этом же объекте")]
        [SerializeField] private IconsRenderer icons;

        private readonly List<TrackedIcon> active = new List<TrackedIcon>();

        // Буфер под виды на иконке: связываем их на каждой смене объекта, и новый список
        // каждый раз был бы мусором.
        private readonly List<IconView> views = new List<IconView>();


        public int Count => active.Count;


        // ── Заказ ─────────────────────────────────────────────────────────

        /// <summary>
        /// Повесить иконку на сущность и забыть: дальше трекер держит её сам. Снимают
        /// по возвращённой ручке (<see cref="TrackedIcon.Release"/>). Null — заявка пустая.
        /// </summary>
        public TrackedIcon Attach(FEntityBase entity, IconRequest request)
        {
            if (entity == null)
                return null;

            return Add(new TrackedIcon(entity, entity.Center, request));
        }

        /// <summary>То же над точкой мира: для того, у чего объекта нет вовсе, — метки эвакуации.</summary>
        public TrackedIcon Attach(Vector3 point, IconRequest request)
        {
            return Add(new TrackedIcon(null, point, request));
        }

        private TrackedIcon Add(TrackedIcon icon)
        {
            if (icon.Request == null || icon.Request.Prefab == null)
                return null;

            active.Add(icon);

            return icon;
        }


        // ── Правила показа ────────────────────────────────────────────────

        private void UpdateIcon(TrackedIcon icon)
        {
            if (!ShouldShow(icon))
            {
                icons.Hide(icon);
                icon.Instance = null;

                return;
            }

            var request = icon.Request;

            var point = icon.HasEntity
                ? ScreenPoint.Scene(icon.Entity.transform, CenterOffset(icon))
                : ScreenPoint.Scene(icon.Point, request.Offset);

            var instance = icons.Show(icon, request.Prefab, point, request.Offscreen, request.EdgePrefab,
                request.ClampByIconEdge, request.AvoidOverlap);

            // Объект сменился: появился впервые или рендер подменил его на краевой.
            if (instance != icon.Instance)
            {
                icon.Instance = instance;

                Bind(icon);
            }
        }

        private bool ShouldShow(TrackedIcon icon)
        {
            if (icon.IsLost)
                return false;

            var condition = icon.Request.Condition;

            return condition == null || condition(icon.Entity);
        }

        // Рендер следит за трансформом, а иконке нужен центр сущности: у персонажа
        // transform.position в ногах, и иконка висела бы по пояс.
        private Vector3 CenterOffset(TrackedIcon icon)
        {
            return icon.Entity.Center - icon.Entity.transform.position + icon.Request.Offset;
        }

        // Все виды на корне, а не первый: полоска, стамина и размен — отдельные компоненты,
        // и каждый сам разбирает, над кем висит.
        private void Bind(TrackedIcon icon)
        {
            if (icon.Instance == null)
                return;

            icon.Instance.GetComponents(views);

            for (var i = 0; i < views.Count; i++)
                views[i].Bind(icon);

            views.Clear();
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (icons == null)
                icons = GetComponent<IconsRenderer>();
        }

        private void Update()
        {
            if (icons == null)
                return;

            // С конца: снятые выбрасываем в этом же проходе, и сдвиг хвоста не пропустит соседа.
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var icon = active[i];

                if (icon.IsReleased || icon.IsLost)
                {
                    icons.Hide(icon);
                    active.RemoveAt(i);

                    continue;
                }

                UpdateIcon(icon);
            }
        }

        private void OnDestroy()
        {
            if (icons == null)
                return;

            for (var i = 0; i < active.Count; i++)
                icons.Hide(active[i]);

            active.Clear();
        }
    }
}
