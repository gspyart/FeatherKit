using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Порядок резки вырезающих коробок (<see cref="LiveBuilderHierarchyOrder"/>): как слои — коробка режет стены и здания своего объекта,
    /// что стоят выше неё в списке объектов; что ниже — поставлено после неё, поверх дыры, и остаётся целым («вырезал, добавил, вырезал»).
    /// Здесь помнится для каждой коробки, что она режет и что после неё целое: карточка коробки показывает оба списка и «Поставить ниже»
    /// (<see cref="LiveBuilderBoxPartCard"/>). Перестановка в списке поменяла, режет ли коробка стену, — сообщение в окне сцены и строка в консоли.
    /// Списки пополняют пересборки стен и зданий; выделенную коробку карточка проверяет сразу (<see cref="CheckNow"/>), а после перезагрузки скриптов
    /// и открытия сцены списки всех вырезающих коробок заполняются заново — чтобы первая же перестановка дала сообщение.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderCutOrderNotices
    {
        // Сколько держится сообщение в окне сцены, с.
        private const double NotificationSeconds = 4.0;

        private static readonly Dictionary<Component, List<Component>> CutTargetsByBox = new();
        private static readonly Dictionary<Component, List<Component>> SkippedTargetsByBox = new();

        // Что сообщили, пока шла проверка этой коробки: остальное из её списков — уже не про неё (отодвинули, удалили).
        private static readonly HashSet<Component> CheckedTargets = new();
        private static Component checkingBox;

        // Последняя проверенная сразу коробка и версия сцены тогда — карточку рисуют много раз в секунду, проверять — раз на правку.
        private static Component lastCheckedBox;
        private static int lastCheckedSceneVersion = -1;


        // Скрипты перезагрузились или открыли сцену — списки пусты: заполнить, когда редактор освободится.
        static LiveBuilderCutOrderNotices()
        {
            EditorApplication.delayCall += RememberAllCutBoxes;
            EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += RememberAllCutBoxes;
        }


        // ── Что сообщают пересборки ──────────────────────────────────────

        // Коробка режет цель: стоит ниже неё и задевает. Раньше она цель не резала — порядок поменяли: сказать.
        public static void ReportBoxCuts(Component box, Component target)
        {
            if (box == null || target == null)
                return;

            RememberChecked(box, target);
            var wasSkipped = Remove(SkippedTargetsByBox, box, target);
            Add(CutTargetsByBox, box, target);

            if (wasSkipped)
                Notify("«" + box.name + "» теперь режет «" + target.name + "»: стоит ниже в списке.", target);
        }


        // Коробка задевает цель, но стоит выше неё: цель поставлена после коробки и целая. Раньше резала — порядок поменяли: сказать.
        public static void ReportBoxAboveTarget(Component box, Component target)
        {
            if (box == null || target == null)
                return;

            RememberChecked(box, target);
            var wasCut = Remove(CutTargetsByBox, box, target);
            Add(SkippedTargetsByBox, box, target);

            if (wasCut)
                Notify("«" + box.name + "» теперь выше «" + target.name + "» в списке и не режет её. Ctrl+Z — вернуть.", target);
        }

        private static void RememberChecked(Component box, Component target)
        {
            if (box == checkingBox)
                CheckedTargets.Add(target);
        }

        private static void Notify(string text, Object context)
        {
            Debug.Log("Live Builder: " + text, context);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent(text), NotificationSeconds);
        }


        // ── Проверка сразу ───────────────────────────────────────────────

        // Проверить вырезающую коробку сейчас, не дожидаясь пересборки: стены и здания её объекта собирают свои проёмы без меша —
        // и сами сообщают сюда, режет она их или стоит выше. Чего не сообщили — уже не про неё. Раз на версию сцены.
        public static void CheckNow(BoxModelPart box)
        {
            if (box == null || box.Mode != BoxModelPartMode.Cut)
                return;

            var sceneVersion = BoxModelRebuilder.SceneVersion;

            if (box == lastCheckedBox && sceneVersion == lastCheckedSceneVersion)
                return;

            lastCheckedBox = box;
            lastCheckedSceneVersion = sceneVersion;
            checkingBox = box;
            CheckedTargets.Clear();

            try
            {
                CollectAround(box);
            }
            finally
            {
                checkingBox = null;
            }

            ForgetUnchecked(CutTargetsByBox, box);
            ForgetUnchecked(SkippedTargetsByBox, box);
        }

        // Что режет каждая вырезающая коробка сцены — опора для сообщений о перестановке. Во время игры не нужно.
        private static void RememberAllCutBoxes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (var part in StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModelPart>())
                if (part.Mode == BoxModelPartMode.Cut && part.gameObject.activeInHierarchy)
                    CheckNow(part);
        }

        // Стены и здания объекта, в котором лежит коробка, собирают свои проёмы и дыры в перекрытиях.
        private static void CollectAround(BoxModelPart box)
        {
            var stage = StageUtility.GetStageHandle(box.gameObject);

            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                if (IsInSameModel(box, wall))
                    foreach (var path in SplineWallBuildingSides.PathsToBuild(wall))
                        SplineWallRebuilder.CollectHoles(wall, path);

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
            {
                if (!IsInSameModel(box, building))
                    continue;

                foreach (var path in SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep))
                    SplineWallRebuilder.CollectHoles(building, path);

                SplineBuildingRebuilder.CollectSlabHoles(building);
            }
        }

        // Стена или здание, которые коробка вообще может резать: она лежит в их объекте вокруг.
        private static bool IsInSameModel(BoxModelPart box, Component target)
        {
            if (!target.gameObject.activeInHierarchy)
                return false;

            var model = SplineWallRebuilder.OuterModelOf(target);
            return model != null && box.transform.IsChildOf(model.transform);
        }

        private static void ForgetUnchecked(Dictionary<Component, List<Component>> targetsByBox, Component box)
        {
            if (targetsByBox.TryGetValue(box, out var targets))
                targets.RemoveAll(target => !CheckedTargets.Contains(target));
        }


        // ── Списки для окна ──────────────────────────────────────────────

        // Стены и здания, которые коробка режет: стоят выше неё в списке и задевают её.
        public static List<Component> TargetsCutBy(Component box)
        {
            return Current(CutTargetsByBox, box, target => !LiveBuilderHierarchyOrder.IsBefore(box.transform, target.transform));
        }


        // Стены и здания, которые коробка задевает, но не режет: стоят ниже неё в списке — поставлены после.
        public static List<Component> TargetsSkippedBy(Component box)
        {
            return Current(SkippedTargetsByBox, box, target => LiveBuilderHierarchyOrder.IsBefore(box.transform, target.transform));
        }

        // Список коробки без удалённых и тех, у кого порядок уже другой.
        private static List<Component> Current(Dictionary<Component, List<Component>> targetsByBox, Component box, System.Predicate<Component> isStillTrue)
        {
            if (box == null || !targetsByBox.TryGetValue(box, out var targets))
                return new List<Component>();

            targets.RemoveAll(target => target == null || !isStillTrue(target));
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


        // ── Помощники ────────────────────────────────────────────────────

        private static void Add(Dictionary<Component, List<Component>> targetsByBox, Component box, Component target)
        {
            if (!targetsByBox.TryGetValue(box, out var targets))
                targetsByBox[box] = targets = new List<Component>();

            if (!targets.Contains(target))
                targets.Add(target);
        }

        // Убрать цель из списка коробки. Была там — true.
        private static bool Remove(Dictionary<Component, List<Component>> targetsByBox, Component box, Component target)
        {
            return targetsByBox.TryGetValue(box, out var targets) && targets.Remove(target);
        }
    }
}
