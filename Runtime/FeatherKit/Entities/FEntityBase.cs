using System;
using System.Collections.Generic;
using FeatherKit.Pooling;
using UnityEngine;

namespace FeatherKit.Entities
{
    /// <summary>
    /// Базовый класс для любого объекта, который спавнится через PoolManager (юниты, снаряды,
    /// интерактивные объекты). Кэширует то, до чего часто нужно быстро достучаться —
    /// рендереры, коллайдеры, теги. Никакой боевой логики (HP, урон) тут нет — переопределяй
    /// поиск компонентов и хуки спауна в наследниках, не трогая Awake целиком.
    /// </summary>
    public class FEntityBase : MonoBehaviour, IPoolable
    {
        [Tooltip("Чем объект является для остальных: по тегам его отбирают в цели, считают " +
                 "и фильтруют. Сравнение по ссылке на ассет, не по строке")]
        [SerializeField] private UnitTag[] tags = Array.Empty<UnitTag>();

        [Header("Точка отсчёта")]
        [Tooltip("От чего считать смысловой центр объекта; если пусто — от самого объекта")]
        [SerializeField] private Transform centerAnchor;

        [Tooltip("Смещение центра в ЛОКАЛЬНЫХ координатах, м. Обычно середина корпуса, а не пятки: " +
                 "от этой точки меряются расстояния до объекта и от него")]
        [SerializeField] private Vector3 centerOffset = new Vector3(0f, 0f, 0f);

        private Renderer[] renderers = Array.Empty<Renderer>();
        private Collider[] colliders = Array.Empty<Collider>();
        private HashSet<UnitTag> tagSet;


        public IReadOnlyList<Renderer> Renderers => renderers;

        public IReadOnlyList<Collider> Colliders => colliders;

        /// <summary>
        /// Смысловой центр объекта — точка, от которой считаются расстояния между объектами:
        /// дистанция до цели, радиус атаки, попал ли в зону взрыва.
        ///
        /// Нужен потому, что transform.position у персонажа лежит в ногах, и мерить от него
        /// значит промахиваться на полроста — особенно заметно на танке, который вдвое выше
        /// бегуна.
        ///
        /// Для отрисовки НЕ предназначен: полоса здоровья и цифры урона задают свою точку
        /// сами, у них разная высота и свои требования.
        ///
        /// Смещение локальное, поэтому у отмасштабированного юнита центр едет вместе
        /// с моделью, а привязка к кости — за анимацией.
        /// </summary>
        public Vector3 Center => centerAnchor != null
            ? centerAnchor.TransformPoint(centerOffset)
            : transform.TransformPoint(centerOffset);

        /// <summary>Пул, из которого объект выдан. Null, если создан обычным Instantiate.</summary>
        protected PoolManager Pool { get; private set; }

        protected virtual string ScriptDescription => $"Base Entity Object[{transform.name}]";


        // ── Теги ──────────────────────────────────────────────────────────

        // tagSet может быть ещё null, если кто-то спросит теги до Awake — это не повод падать.
        public bool HasTag(UnitTag tag) => tag != null && tagSet != null && tagSet.Contains(tag);

        public bool AddTag(UnitTag tag) => tag != null && EnsureTagSet().Add(tag);

        public bool RemoveTag(UnitTag tag) => tag != null && EnsureTagSet().Remove(tag);

        private HashSet<UnitTag> EnsureTagSet()
        {
            if (tagSet == null)
                ResetTags();

            return tagSet;
        }


        private void ResetTags()
        {
            if (tagSet == null)
                tagSet = new HashSet<UnitTag>(tags);
            else
                RefillTags();
        }

        private void RefillTags()
        {
            tagSet.Clear();

            for (var i = 0; i < tags.Length; i++)
            {
                if (tags[i] != null)
                    tagSet.Add(tags[i]);
            }
        }


        // ── Пул ───────────────────────────────────────────────────────────

        /// <summary>Вернуть себя в пул. Объект не из пула — просто уничтожается.</summary>
        public virtual void ReturnToPool()
        {
            if (Pool != null)
                Pool.Release(this);
            else
                Destroy(gameObject);
        }


        /// <summary>Объект выдан из пула — здесь наследник сбрасывает состояние прошлой жизни.</summary>
        protected virtual void OnSpawned() { }

        /// <summary>Объект уходит в пул — здесь наследник отпускает всё, что держал.</summary>
        protected virtual void OnDespawned() { }


        // ── IPoolable ─────────────────────────────────────────────────────
        // Явная реализация: снаружи, кроме самого пула, эти хуки дёргать не должны,
        // а наследники переопределяют привычные protected-методы выше.

        void IPoolable.OnSpawned(PoolManager owner)
        {
            Pool = owner;

            // Теги, навешанные в прошлой жизни инстанса, сбрасываем: иначе враг, которому
            // модификатор добавил тег, останется с ним и после возврата из пула.
            ResetTags();

            OnSpawned();
        }

        void IPoolable.OnDespawned() => OnDespawned();


        // ── Поиск своих компонентов ───────────────────────────────────────

        protected virtual Renderer[] FindRenderers() => GetComponentsInChildren<Renderer>(true);

        protected virtual Collider[] FindColliders() => GetComponentsInChildren<Collider>(true);


        // ── Unity ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (renderers.Length == 0)
                renderers = FindRenderers();

            if (colliders.Length == 0)
                colliders = FindColliders();

            ResetTags();
        }
    }
}
