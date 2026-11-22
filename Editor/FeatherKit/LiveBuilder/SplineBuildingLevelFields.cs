using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Поля здания про этажи уровня (<see cref="SplineBuildingLevelPlacement"/>) — для инспектора здания и секции Construction панели:
    /// - на каком этаже уровня стоит (номер для людей) или свободное
    /// - высота этажа: уровня или своя
    /// Правки — с Undo, через <see cref="SplineBuildingLevelSync"/>.
    /// </summary>
    public static class SplineBuildingLevelFields
    {
        private static readonly string[] StoreyHeightSources = { "Level", "Own" };


        public static void Draw(SplineBuilding building)
        {
            DrawLevelStorey(building);
            DrawStoreyHeightSource(building);
        }

        // Номер — как в окне: 1 — первый этаж, −1 — подвал; нулевого нет.
        private static void DrawLevelStorey(SplineBuilding building)
        {
            var placement = building.LevelPlacement;

            using (new EditorGUILayout.HorizontalScope())
            {
                var isOnLevelStorey = EditorGUILayout.Toggle(new GUIContent("On Level Storey",
                    "Здание стоит на этаже уровня: поменяли этажи уровня — встаёт низом на пол этого этажа. Выключено — свободное."), placement.IsOnLevelStorey);

                if (isOnLevelStorey != placement.IsOnLevelStorey)
                {
                    if (isOnLevelStorey)
                        SplineBuildingLevelSync.PutOnLevelStorey(building, SplineBuildingLevelSync.NearestLevelStoreyOf(building));
                    else
                        SplineBuildingLevelSync.MakeFree(building);
                }

                if (!placement.IsOnLevelStorey)
                    return;

                var number = EditorGUILayout.DelayedIntField(LiveBuilderStoreys.NumberOf(placement.LevelStorey), GUILayout.Width(48f));

                if (number != 0 && number != LiveBuilderStoreys.NumberOf(placement.LevelStorey))
                    SplineBuildingLevelSync.PutOnLevelStorey(building, number > 0 ? number - 1 : number);
            }
        }

        private static void DrawStoreyHeightSource(SplineBuilding building)
        {
            var placement = building.LevelPlacement;
            var source = EditorGUILayout.Popup(new GUIContent("Storey Height", "Уровня — как у этажей в разделе Storeys окна Live Builder, меняется вместе с ними. Своя — у этого здания."),
                placement.UsesLevelStoreyHeight ? 0 : 1, StoreyHeightSources);

            if ((source == 0) != placement.UsesLevelStoreyHeight)
                SplineBuildingLevelSync.UseLevelStoreyHeight(building, source == 0);
        }
    }
}
