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
    /// здание, повёрнутое внутри своего объекта, здание мимо этажей уровня по ошибке, привязка к этажу, которого у здания нет (объект спрятан).
    /// Здание на платформе или на крыше стоит не на этаже уровня нарочно — это не ошибка. Ищет по кнопке во вкладке «Уровень».
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

            foreach (var link in stage.FindComponentsOfType<SplineBuildingStoreyLink>())
                if (link.IsValid && (link.Storey < 0 || link.Storey >= link.Building.StoreyCount))
                    issues.Add(new LiveBuilderLevelIssue("«" + link.name + "» стоит на этаже " + (link.Storey + 1) + ", а у здания «" + link.Building.name +
                                                         "» этажей " + link.Building.StoreyCount + ": спрятан, пока этаж не вернётся.", link.gameObject));

            return issues;
        }

        private static void AddBuildingIssues(SplineBuilding building, List<LiveBuilderLevelIssue> issues)
        {
            if (SplineBuildingMeshBuilder.IsContourBroken(building))
                issues.Add(new LiveBuilderLevelIssue("Контур здания «" + building.name + "» пересекает сам себя: часть пола и крыши не построена.", building.gameObject));

            if (IsRotatedInsideModel(building))
                issues.Add(new LiveBuilderLevelIssue("Здание «" + building.name + "» повёрнуто внутри своего объекта: лестницы и коробки интерьера разъедутся с ним.",
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
                    issues.Add(new LiveBuilderLevelIssue("Здание «" + building.name + "» на " + Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(offset) * 100f)) + " см " +
                                                         (offset > 0f ? "выше" : "ниже") + " пола этажа уровня — похоже на промах. " +
                                                         "Выдели его: «Поставить на этажи уровня».", building.gameObject));
                return;
            }

            if (!SplineBuildingLevelAlignment.HasLevelStoreyHeight(building))
                issues.Add(new LiveBuilderLevelIssue("У здания «" + building.name + "» этаж " + LiveBuilderSceneText.FormatMeters(building.StoreyHeight) +
                                                     " м, у уровня " + LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.StoreyHeight) +
                                                     " м: выше первого этажа «этаж» в окне и в здании разойдутся.", building.gameObject));
        }

        // Здание повёрнуто внутри своего объекта из коробок: лестницы и коробки интерьера идут по осям объекта и разъедутся со зданием.
        public static bool IsRotatedInsideModel(SplineBuilding building)
        {
            var model = building.transform.parent != null ? building.transform.parent.GetComponentInParent<BoxModel>() : null;
            return model != null && Quaternion.Angle(model.transform.rotation, building.transform.rotation) >= 0.01f;
        }
    }
}
