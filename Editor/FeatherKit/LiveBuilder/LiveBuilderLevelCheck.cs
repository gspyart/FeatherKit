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
    /// здание, повёрнутое внутри своего объекта, здание не на этажах уровня, привязка к этажу, которого у здания нет (объект спрятан),
    /// вырезающая коробка, что задевает стену или здание, но стоит выше
    /// в списке и её не режет (<see cref="LiveBuilderCutOrderNotices"/>). Ищет по кнопке во вкладке «Уровень» — на большом уровне это не бесплатно.
    /// </summary>
    public static class LiveBuilderLevelCheck
    {
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

            foreach (var part in stage.FindComponentsOfType<BoxModelPart>())
                if (part.Mode == BoxModelPartMode.Cut && part.gameObject.activeInHierarchy)
                    AddCutOrderIssues(part, issues);

            return issues;
        }

        private static void AddBuildingIssues(SplineBuilding building, List<LiveBuilderLevelIssue> issues)
        {
            if (SplineBuildingMeshBuilder.IsContourBroken(building))
                issues.Add(new LiveBuilderLevelIssue("Контур здания «" + building.name + "» пересекает сам себя: часть пола и крыши не построена.", building.gameObject));

            if (IsRotatedInsideModel(building))
                issues.Add(new LiveBuilderLevelIssue("Здание «" + building.name + "» повёрнуто внутри своего объекта: лестницы и коробки интерьера разъедутся с ним.",
                    building.gameObject));

            if (!SplineBuildingLevelAlignment.IsAlignedToLevel(building))
                issues.Add(new LiveBuilderLevelIssue("Здание «" + building.name + "» не на этажах уровня: «этаж» в окне и в здании — разные.", building.gameObject));
        }

        // Вырезающая коробка задевает стену или здание, но стоит выше в списке: они поставлены после и целые. Бывает нарочно — проверь.
        private static void AddCutOrderIssues(BoxModelPart part, List<LiveBuilderLevelIssue> issues)
        {
            LiveBuilderCutOrderNotices.CheckNow(part);

            foreach (var target in LiveBuilderCutOrderNotices.TargetsSkippedBy(part))
                issues.Add(new LiveBuilderLevelIssue("Вырезающая коробка «" + part.name + "» задевает «" + target.name + "», но стоит выше в списке и не режет её. " +
                                                     "Нарочно — ничего не делай.", part.gameObject));
        }


        // Здание повёрнуто внутри своего объекта из коробок: лестницы и коробки интерьера идут по осям объекта и разъедутся со зданием.
        public static bool IsRotatedInsideModel(SplineBuilding building)
        {
            var model = building.transform.parent != null ? building.transform.parent.GetComponentInParent<BoxModel>() : null;
            return model != null && Quaternion.Angle(model.transform.rotation, building.transform.rotation) >= 0.01f;
        }
    }
}
