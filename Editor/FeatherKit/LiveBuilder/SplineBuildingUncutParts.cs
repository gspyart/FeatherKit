using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Нерезаная геометрия частей зданий по подписи части (<see cref="SplineBuildingPartKeys"/>) — в памяти, до перезагрузки скриптов.
    /// Поменялись только вырезы — часть режется заново из готовой геометрии, без сборки стен и плит.
    /// У здания хранится нынешний набор частей и прошлый: Ctrl+Z тоже быстрый.
    /// </summary>
    public static class SplineBuildingUncutParts
    {
        private static readonly Dictionary<EntityId, Dictionary<string, SplineBuildingUncutPart>> PartsByBuilding = new();


        public static bool TryGet(SplineBuilding building, string key, out SplineBuildingUncutPart part)
        {
            part = null;
            return PartsByBuilding.TryGetValue(building.GetEntityId(), out var parts) && parts.TryGetValue(key, out part);
        }


        public static SplineBuildingUncutPart Store(SplineBuilding building, string key, SplineWallGeometry geometry)
        {
            if (!PartsByBuilding.TryGetValue(building.GetEntityId(), out var parts))
                PartsByBuilding[building.GetEntityId()] = parts = new Dictionary<string, SplineBuildingUncutPart>();

            return parts[key] = new SplineBuildingUncutPart(geometry);
        }


        // Сборка здания кончилась: части с этими подписями нынешние, нынешние прежде — прошлые, а что было ещё раньше — забыть.
        public static void ShiftGenerations(SplineBuilding building, List<SplineBuildingPartKey> liveParts)
        {
            if (!PartsByBuilding.TryGetValue(building.GetEntityId(), out var parts))
                return;

            var liveKeys = new HashSet<string>();

            foreach (var livePart in liveParts)
                liveKeys.Add(livePart.Key);

            var dropped = new List<string>();

            foreach (var pair in parts)
            {
                if (liveKeys.Contains(pair.Key))
                    pair.Value.IsPrevious = false;
                else if (pair.Value.IsPrevious)
                    dropped.Add(pair.Key);
                else
                    pair.Value.IsPrevious = true;
            }

            foreach (var key in dropped)
                parts.Remove(key);
        }


        // Здание удалено или его сцена закрыта — его части больше не нужны. Возвращает, сколько зданий забыто.
        public static int ForgetDestroyed()
        {
            return LiveBuilderDestroyedObjects.RemoveFrom(PartsByBuilding);
        }
    }


    /// <summary>Нерезаная геометрия одной части здания и её габарит — по нему ищут вырезы части.</summary>
    public sealed class SplineBuildingUncutPart
    {
        public SplineBuildingUncutPart(SplineWallGeometry geometry)
        {
            Geometry = geometry;
            Bounds = geometry.Bounds;
        }


        public SplineWallGeometry Geometry { get; }
        public Bounds Bounds { get; }

        // Часть из прошлого набора здания: в следующий раз, если не понадобится, забудется.
        public bool IsPrevious { get; set; }
    }
}
