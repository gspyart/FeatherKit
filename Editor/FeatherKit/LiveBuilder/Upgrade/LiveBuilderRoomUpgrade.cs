using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Комната старых данных (<see cref="BoxModelRoom"/>) — в новые переключатели оболочки: раньше «без стен» убирало и пол с потолком.
    /// Остальные старые «комнаты» — замкнутая стена и здание в один этаж — остаются стеной и зданием, их обновлять не нужно.
    /// </summary>
    public static class LiveBuilderRoomUpgrade
    {
        public static void Upgrade(IEnumerable<BoxModel> legacyModels, LiveBuilderUpgradeReport report)
        {
            foreach (var model in legacyModels)
                foreach (var room in model.GetComponentsInChildren<BoxModelRoom>(true))
                {
                    if (room.HasWalls || !room.HasFloor && !room.HasCeiling || LiveBuilderGroups.GroupOf(room.transform.parent) != model
                        || PrefabUtility.IsPartOfPrefabInstance(room))
                        continue;

                    room.HasFloor = false;
                    room.HasCeiling = false;
                    report.AddRestructured(room, "room without walls: floor and ceiling switched off, as it was built before");
                }
        }
    }
}
