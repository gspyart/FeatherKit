using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Кто сейчас внутри триггеров на объекте сценария. Часть Scenario.
    /// - ведётся всегда, а не только пока кто-то слушает: блок, подписавшийся позже, узнаёт уже вошедших
    /// - коллайдер считается столько раз, сколько вошёл; уничтоженные выкидываются при чтении
    /// </summary>
    public class ScenarioTriggerOccupants
    {
        private readonly Dictionary<Collider, int> entriesByCollider = new Dictionary<Collider, int>();
        private readonly List<Collider> destroyedColliders = new List<Collider>();


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


        // Уничтоженный коллайдер выхода из триггера не присылает.
        private void RemoveDestroyed()
        {
            destroyedColliders.Clear();

            foreach (var collider in entriesByCollider.Keys)
            {
                if (collider == null)
                    destroyedColliders.Add(collider);
            }

            foreach (var collider in destroyedColliders)
                entriesByCollider.Remove(collider);
        }
    }
}
