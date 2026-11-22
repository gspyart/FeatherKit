using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Одна найденная проверкой уровня странность: что не так — по-человечески, и с чем — чтобы выделить одним кликом.</summary>
    public readonly struct LiveBuilderLevelIssue
    {
        public LiveBuilderLevelIssue(string text, Object target)
        {
            Text = text;
            Target = target;
        }


        public string Text { get; }
        public Object Target { get; }
    }


    /// <summary>
    /// Проверка уровня — всё подозрительное на сцене одним списком, а не только у выделенного: здание с перекрещённым контуром,
    /// здание, повёрнутое внутри своего объекта, здание мимо этажей уровня по ошибке.
    /// Здание на платформе или на крыше стоит не на этаже уровня нарочно — это не ошибка. Ищет по кнопке Check Level в окне Live Builder.
    /// </summary>
    public static class LiveBuilderLevelCheck
    {
        // Низ здания ближе этого к полу этажа уровня, но не на нём, — похоже на промах, а не на платформу или крышу, м.
        private const float NearMissDistance = 0.25f;


        // ── Проверка ─────────────────────────────────────────────────────

        public static List<LiveBuilderLevelIssue> FindIssues()
        {
            var issues = new List<LiveBuilderLevelIssue>();
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
                if (building.gameObject.activeInHierarchy)
                    AddBuildingIssues(building, issues);

            return issues;
        }

        private static void AddBuildingIssues(SplineBuilding building, List<LiveBuilderLevelIssue> issues)
        {
            if (SplineBuildingMeshBuilder.IsContourBroken(building))
                issues.Add(new LiveBuilderLevelIssue("The outline of building '" + building.name + "' crosses itself: part of the floor and roof is not built.", building.gameObject));

            if (IsRotatedInsideModel(building))
                issues.Add(new LiveBuilderLevelIssue("Building '" + building.name + "' is rotated inside its object: stairs and interior boxes will drift away from it.",
                    building.gameObject));

            AddLevelStoreyIssue(building, issues);
        }

        // Мимо этажей уровня — только когда похоже на ошибку: низ промахнулся мимо пола этажа на сантиметры
        // или стоит на нём, но высота этажа другая — выше первого этажи окна и здания разойдутся.
        private static void AddLevelStoreyIssue(SplineBuilding building, List<LiveBuilderLevelIssue> issues)
        {
            if (!SplineBuildingLevelAlignment.IsOnLevelFloor(building))
            {
                var offset = SplineBuildingLevelAlignment.OffsetFromLevelFloorOf(building);

                if (Mathf.Abs(offset) < NearMissDistance)
                    issues.Add(new LiveBuilderLevelIssue("Building '" + building.name + "' is " + Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(offset) * 100f)) + " cm " +
                                                         (offset > 0f ? "above" : "below") + " the level storey floor — looks like a miss. " +
                                                         "Select it: Put On Level Storeys.", building.gameObject));
                return;
            }

            if (!SplineBuildingLevelAlignment.HasLevelStoreyHeight(building))
                issues.Add(new LiveBuilderLevelIssue("Building '" + building.name + "' has a " + LiveBuilderSceneText.FormatMeters(building.StoreyHeight) +
                                                     " m storey, the level has " + LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.StoreyHeight) +
                                                     " m: above storey 1 the window's storeys and the building's will not match.", building.gameObject));
        }

        // Здание повёрнуто внутри своего объекта из коробок: лестницы и коробки интерьера идут по осям объекта и разъедутся со зданием.
        public static bool IsRotatedInsideModel(SplineBuilding building)
        {
            var model = building.transform.parent != null ? building.transform.parent.GetComponentInParent<BoxModel>() : null;
            return model != null && Quaternion.Angle(model.transform.rotation, building.transform.rotation) >= 0.01f;
        }
    }
}
