using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Кто сейчас внутри триггеров на объекте сценария. Часть Scenario.
    /// - ведётся всегда, а не только пока кто-то слушает: блок, подписавшийся позже, узнаёт уже вошедших
    /// - коллайдер считается столько раз, сколько вошёл
    /// - ловит уход без OnTriggerExit: коллайдер выключили, уничтожили или унесли телепортом — и шлёт за него выход
    /// </summary>
    public class ScenarioTriggerOccupants
    {
        private readonly Component owner;
        private readonly Dictionary<Collider, int> entriesByCollider = new Dictionary<Collider, int>();
        private readonly List<Collider> ownTriggers = new List<Collider>();
        private readonly List<Collider> silentlyLeftColliders = new List<Collider>();


        public ScenarioTriggerOccupants(Component owner)
        {
            this.owner = owner;
        }

        /// <summary>Коллайдеры внутри, уже без уничтоженных.</summary>
        public IReadOnlyCollection<Collider> CollidersInside
        {
            get
            {
                RemoveDestroyed();

                return entriesByCollider.Keys;
            }
        }


        public void HandleEntered(Collider other)
        {
            if (other == null)
                return;

            // Свои триггеры пересобираем, когда зона заселяется: так видны и добавленные после старта.
            if (entriesByCollider.Count == 0)
                CollectOwnTriggers();

            entriesByCollider.TryGetValue(other, out var count);
            entriesByCollider[other] = count + 1;
        }

        public void HandleExited(Collider other)
        {
            if (other == null || !entriesByCollider.TryGetValue(other, out var count))
                return;

            if (count > 1)
                entriesByCollider[other] = count - 1;
            else
                entriesByCollider.Remove(other);
        }


        // ── Уход без OnTriggerExit ───────────────────────────────────────

        /// <summary>
        /// Выпустить гостей, ушедших молча, и прислать выход за каждый засчитанный вход — иначе Wait For «вышел» зависнет.
        /// Уничтоженному выход не шлём: «Caller» уже нет.
        /// </summary>
        public void ReleaseSilentlyLeft(Action<Collider> sendExit)
        {
            if (entriesByCollider.Count == 0)
                return;

            silentlyLeftColliders.Clear();

            foreach (var collider in entriesByCollider.Keys)
            {
                if (HasLeftSilently(collider))
                    silentlyLeftColliders.Add(collider);
            }

            foreach (var collider in silentlyLeftColliders)
            {
                var count = entriesByCollider[collider];
                entriesByCollider.Remove(collider);

                for (var entry = 0; entry < count && collider != null; entry++)
                    sendExit(collider);
            }
        }

        // Выключенный коллайдер Unity из триггера не выпускает; телепорт «выключил — перенёс — включил» — тоже.
        private bool HasLeftSilently(Collider collider)
        {
            return collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                   || IsAwayFromOwnTriggers(collider);
        }

        // Грубо, по габаритам: телепорт уносит далеко, а точный выход у края пришлёт сама физика.
        private bool IsAwayFromOwnTriggers(Collider collider)
        {
            if (ownTriggers.Count == 0)
                return false;

            var bounds = collider.bounds;

            foreach (var trigger in ownTriggers)
            {
                if (trigger != null && trigger.enabled && trigger.gameObject.activeInHierarchy
                    && trigger.bounds.Intersects(bounds))
                    return false;
            }

            return true;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        // Сообщения о триггере идут на объект коллайдера и на объект его Rigidbody — только такие триггеры наши.
        private void CollectOwnTriggers()
        {
            owner.GetComponentsInChildren(true, ownTriggers);

            for (var index = ownTriggers.Count - 1; index >= 0; index--)
            {
                if (!IsOwnTrigger(ownTriggers[index]))
                    ownTriggers.RemoveAt(index);
            }
        }

        private bool IsOwnTrigger(Collider collider)
        {
            if (!collider.isTrigger)
                return false;

            var body = collider.attachedRigidbody;

            return collider.gameObject == owner.gameObject || (body != null && body.gameObject == owner.gameObject);
        }

        // Уничтоженный коллайдер выхода из триггера не присылает.
        private void RemoveDestroyed()
        {
            silentlyLeftColliders.Clear();

            foreach (var collider in entriesByCollider.Keys)
            {
                if (collider == null)
                    silentlyLeftColliders.Add(collider);
            }

            foreach (var collider in silentlyLeftColliders)
                entriesByCollider.Remove(collider);
        }
    }
}
