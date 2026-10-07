using UnityEngine;

namespace FeatherKit.Pooling
{
    /// <summary>
    /// Безопасная ссылка на объект, выданный PoolManager'ом. Решает проблему "объект улетел
    /// обратно в пул (или был переиспользован под другую сущность), а кто-то всё ещё
    /// держит на него голую ссылку".
    ///
    /// Защита от дурака встроена в сам handle: не нужно отдельно спрашивать PoolManager —
    /// <see cref="Instance"/> сам вернёт null, если объект уже не тот, для которого handle был выдан.
    /// </summary>
    public readonly struct PoolHandle<T> where T : Component
    {
        private readonly T rawInstance;
        private readonly ulong instanceId;
        private readonly int generation;
        private readonly PoolManager owner;


        internal PoolHandle(T instance, ulong instanceId, int generation, PoolManager owner)
        {
            rawInstance = instance;
            this.instanceId = instanceId;
            this.generation = generation;
            this.owner = owner;
        }


        // Не был возвращён в пул и не выдан заново под другую сущность.
        public bool IsActual => rawInstance != null && owner != null && owner.IsGenerationActual(instanceId, generation);
        // Null, если не актуален — не кэшируй T отдельно, всегда бери отсюда.
        public T Instance => IsActual ? rawInstance : null;
        public bool IsEmpty => Instance == null;
    }
}
