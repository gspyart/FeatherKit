using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Кого каждый вырез (<see cref="GeometryCutter"/>) резал при последней сборке — обратная карта к поиску вырезов постройки.
    /// По ней область пересборки (<see cref="LiveBuilderRebuildScope"/>) находит постройки, из которых сдвинутый проём ушёл. Только в памяти.
    /// </summary>
    public static class GeometryCutterLastTargets
    {
        private static readonly Dictionary<Component, List<GeometryCutter>> CuttersByOwner = new();
        private static readonly Dictionary<GeometryCutter, HashSet<Component>> OwnersByCutter = new();


        // Меш постройки только что прорезан этими вырезами.
        public static void Remember(Component owner, List<GeometryCutter> cutters)
        {
            if (CuttersByOwner.TryGetValue(owner, out var previous))
                foreach (var cutter in previous)
                    if (OwnersByCutter.TryGetValue(cutter, out var owners))
                        owners.Remove(owner);

            CuttersByOwner[owner] = new List<GeometryCutter>(cutters);

            foreach (var cutter in cutters)
            {
                if (!OwnersByCutter.TryGetValue(cutter, out var owners))
                    OwnersByCutter[cutter] = owners = new HashSet<Component>();

                owners.Add(owner);
            }
        }


        // Постройки, чей меш вырез резал в последний раз; удалённые пропускаются.
        public static List<Component> OwnersCutBy(GeometryCutter cutter)
        {
            var owners = new List<Component>();

            if (cutter is null || !OwnersByCutter.TryGetValue(cutter, out var cut))
                return owners;

            foreach (var owner in cut)
                if (owner != null)
                    owners.Add(owner);

            return owners;
        }


        // Постройки и вырезы удалены или их сцена закрыта — их записи больше не нужны. Возвращает, сколько записей убрано.
        public static int ForgetDestroyed()
        {
            var forgotten = CuttersByOwner.Count + OwnersByCutter.Count;
            RemoveDestroyedKeys(CuttersByOwner);
            RemoveDestroyedKeys(OwnersByCutter);

            foreach (var owners in OwnersByCutter.Values)
                owners.RemoveWhere(owner => owner == null);

            return forgotten - CuttersByOwner.Count - OwnersByCutter.Count;
        }

        // Уничтоженный объект как ключ словаря жив, но сравнение с null у Unity его выдаёт.
        private static void RemoveDestroyedKeys<TKey, TValue>(Dictionary<TKey, TValue> map) where TKey : Object
        {
            var destroyed = new List<TKey>();

            foreach (var key in map.Keys)
                if ((Object)key == null)
                    destroyed.Add(key);

            foreach (var key in destroyed)
                map.Remove(key);
        }
    }
}
