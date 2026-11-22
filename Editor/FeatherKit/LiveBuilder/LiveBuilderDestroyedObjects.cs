using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Уборка кешей Live Builder, где ключ — номер объекта (<see cref="EntityId"/>): объект уничтожен — запись больше не нужна.
    /// Сам ничего не помнит; когда убирать — решает очередь пересборки (<see cref="LiveBuilderRebuildQueue"/>).
    /// </summary>
    public static class LiveBuilderDestroyedObjects
    {
        // Убирает записи уничтоженных объектов; возвращает, сколько убрано.
        public static int RemoveFrom<TValue>(Dictionary<EntityId, TValue> cache)
        {
            List<EntityId> destroyed = null;

            foreach (var id in cache.Keys)
                if (EditorUtility.EntityIdToObject(id) == null)
                    (destroyed ??= new List<EntityId>()).Add(id);

            if (destroyed == null)
                return 0;

            foreach (var id in destroyed)
                cache.Remove(id);

            return destroyed.Count;
        }
    }
}
