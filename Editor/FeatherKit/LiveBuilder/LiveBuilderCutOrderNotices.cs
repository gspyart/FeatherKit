using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вырезающие коробки, которые стоят выше стены или здания в иерархии и потому их не режут (<see cref="LiveBuilderHierarchyOrder"/>):
    /// одно сообщение в консоль на пару «коробка — стена» за сессию, со ссылкой на стену, и список для окна — карточка коробки
    /// показывает, что она не режет, с кнопкой «Поставить ниже» (<see cref="LiveBuilderBoxPartCard"/>). Порядок поправили — пара из списка уходит.
    /// Список пополняют пересборки стен и зданий, а выделенную коробку карточка проверяет сразу (<see cref="CheckNow"/>) — и после перезапуска Unity.
    /// </summary>
    public static class LiveBuilderCutOrderNotices
    {
        private static readonly HashSet<(EntityId Box, EntityId Target)> ReportedPairs = new();
        private static readonly Dictionary<Component, List<Component>> SkippedTargetsByBox = new();

        // Последняя проверенная сразу коробка и версия сцены тогда — карточку рисуют много раз в секунду, проверять — раз на правку.
        private static Component lastCheckedBox;
        private static int lastCheckedSceneVersion = -1;


        // Коробка прорезала бы цель, но стоит выше неё — сказать об этом один раз и запомнить для окна.
        public static void ReportBoxAboveTarget(Component box, Component target)
        {
            if (box == null || target == null)
                return;

            if (!SkippedTargetsByBox.TryGetValue(box, out var targets))
                SkippedTargetsByBox[box] = targets = new List<Component>();

            if (!targets.Contains(target))
                targets.Add(target);

            if (!ReportedPairs.Add((box.GetEntityId(), target.GetEntityId())))
                return;

            Debug.Log("Live Builder: вырезающая коробка «" + box.name + "» стоит выше «" + target.name +
                      "» в иерархии и больше её не режет. Нужно резать — перетащи коробку ниже.", target);
        }


        // Проверить вырезающую коробку сейчас, не дожидаясь пересборки: стены и здания вокруг неё, перед которыми она стоит, собирают свои проёмы
        // без меша — и сами сообщают сюда (<see cref="ReportBoxAboveTarget"/>). Раз на версию сцены.
        public static void CheckNow(BoxModelPart box)
        {
            if (box == null || box.Mode != BoxModelPartMode.Cut)
                return;

            var sceneVersion = BoxModelRebuilder.SceneVersion;

            if (box == lastCheckedBox && sceneVersion == lastCheckedSceneVersion)
                return;

            lastCheckedBox = box;
            lastCheckedSceneVersion = sceneVersion;
            var stage = StageUtility.GetStageHandle(box.gameObject);

            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                if (IsCheckedTarget(box, wall))
                    foreach (var path in SplineWallBuildingSides.PathsToBuild(wall))
                        SplineWallRebuilder.CollectHoles(wall, path);

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
            {
                if (!IsCheckedTarget(box, building))
                    continue;

                foreach (var path in SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep))
                    SplineWallRebuilder.CollectHoles(building, path);

                SplineBuildingRebuilder.CollectSlabHoles(building);
            }
        }

        // Стена или здание, которые коробка могла бы резать (она в их объекте вокруг), а стоит перед ними.
        private static bool IsCheckedTarget(BoxModelPart box, Component target)
        {
            if (!target.gameObject.activeInHierarchy || !LiveBuilderHierarchyOrder.IsBefore(box.transform, target.transform))
                return false;

            var model = SplineWallRebuilder.OuterModelOf(target);
            return model != null && box.transform.IsChildOf(model.transform);
        }


        // Стены и здания, которые эта коробка не режет из-за порядка, — только те, перед которыми она всё ещё стоит.
        public static List<Component> TargetsSkippedBy(Component box)
        {
            if (box == null || !SkippedTargetsByBox.TryGetValue(box, out var targets))
                return new List<Component>();

            targets.RemoveAll(target => target == null || !LiveBuilderHierarchyOrder.IsBefore(box.transform, target.transform));
            return new List<Component>(targets);
        }


        // Коробку — сразу за целью среди тех же соседей: теперь она ниже и режет её. У разных родителей — нельзя, false: тогда перетаскивают руками.
        public static bool TryPlaceBelow(Component box, Component target)
        {
            if (box.transform.parent != target.transform.parent)
                return false;

            var targetIndex = target.transform.GetSiblingIndex();
            var index = box.transform.GetSiblingIndex() < targetIndex ? targetIndex : targetIndex + 1;
            Undo.SetSiblingIndex(box.transform, index, "Place Cut Box Below");
            return true;
        }
    }
}
