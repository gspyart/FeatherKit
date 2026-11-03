using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Изоляция (Shift+H) для Live Builder. Клик в сцене выделяет не меш, а то, что его строит: коробку, комнату, лестницу,
    /// повтор или вырез. Штатная изоляция оставляет видимым только выделенное с детьми — и прячет как раз ту геометрию,
    /// которую правишь. Поэтому в изоляции остаются видны и:
    /// - у коробки, комнаты, лестницы, повтора — весь их объект из коробок со всем, что в нём построено
    /// - у выреза — всё построенное Live Builder, что он режет
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderIsolation
    {
        private static bool isShowingGeometry;


        static LiveBuilderIsolation()
        {
            SceneVisibilityManager.visibilityChanged += ShowGeometryOfIsolatedParts;
        }


        private static SceneVisibilityManager Visibility => SceneVisibilityManager.instance;


        // ── Показ геометрии ──────────────────────────────────────────────

        // Изоляция включилась или поменялась — показать геометрию видимых частей.
        // Показанный объект может открыть новые части, например вырезы внутри него, — поэтому по кругу, пока есть что показать.
        private static void ShowGeometryOfIsolatedParts()
        {
            if (isShowingGeometry || !Visibility.IsCurrentStageIsolated())
                return;

            isShowingGeometry = true;

            try
            {
                var shownObjects = new HashSet<GameObject>();

                while (ShowHiddenGeometry(shownObjects))
                {
                }
            }
            finally
            {
                isShowingGeometry = false;
            }
        }

        // Один проход: показывает спрятанную геометрию видимых частей. Показал хоть что-то — true.
        private static bool ShowHiddenGeometry(HashSet<GameObject> shownObjects)
        {
            var stage = StageUtility.GetCurrentStageHandle();
            var isAnyShown = false;

            foreach (var model in ModelsOfVisibleParts(stage))
                isAnyShown |= ShowOnce(model.gameObject, true, shownObjects);

            foreach (var geometry in GeometryCutByVisibleCutters(stage))
                isAnyShown |= ShowOnce(geometry.gameObject, false, shownObjects);

            return isAnyShown;
        }

        // Каждый объект — не больше раза: если показ не сработал, круг всё равно закончится.
        private static bool ShowOnce(GameObject target, bool includesChildren, HashSet<GameObject> shownObjects)
        {
            if (!Visibility.IsHidden(target) || !shownObjects.Add(target))
                return false;

            Visibility.Show(target, includesChildren);
            return true;
        }


        // ── Коробки, комнаты, лестницы, повторы ──────────────────────────

        // Объекты из коробок, в которых лежат видимые части.
        private static HashSet<BoxModel> ModelsOfVisibleParts(StageHandle stage)
        {
            var models = new HashSet<BoxModel>();

            AddModelsOfVisible<BoxModelPart>(stage, models);
            AddModelsOfVisible<BoxModelRoom>(stage, models);
            AddModelsOfVisible<BoxModelStairs>(stage, models);
            AddModelsOfVisible<BoxModelRepeat>(stage, models);

            return models;
        }

        private static void AddModelsOfVisible<TPart>(StageHandle stage, HashSet<BoxModel> models) where TPart : Component
        {
            foreach (var part in stage.FindComponentsOfType<TPart>())
                if (!Visibility.IsHidden(part.gameObject) && part.GetComponentInParent<BoxModel>() is { } model)
                    models.Add(model);
        }


        // ── Вырезы ───────────────────────────────────────────────────────

        // Спрятанные постройки, которые режет хоть один видимый вырез.
        private static List<Component> GeometryCutByVisibleCutters(StageHandle stage)
        {
            var cutGeometry = new List<Component>();

            if (!HasVisibleCutter(stage))
                return cutGeometry;

            AddHiddenCutGeometry<BoxModel>(stage, cutGeometry);
            AddHiddenCutGeometry<SplineWall>(stage, cutGeometry);
            AddHiddenCutGeometry<SplineBuildingPart>(stage, cutGeometry);

            return cutGeometry;
        }

        private static bool HasVisibleCutter(StageHandle stage)
        {
            foreach (var cutter in stage.FindComponentsOfType<GeometryCutter>())
                if (!Visibility.IsHidden(cutter.gameObject))
                    return true;

            return false;
        }

        private static void AddHiddenCutGeometry<TGeometry>(StageHandle stage, List<Component> cutGeometry) where TGeometry : Component
        {
            foreach (var geometry in stage.FindComponentsOfType<TGeometry>())
            {
                if (!Visibility.IsHidden(geometry.gameObject) || !geometry.TryGetComponent<MeshRenderer>(out var meshRenderer))
                    continue;

                foreach (var cutter in GeometryCutterLookup.CuttersFor(geometry, GeometryCutterLookup.WorldBoundsOf(meshRenderer)))
                {
                    if (Visibility.IsHidden(cutter.gameObject))
                        continue;

                    cutGeometry.Add(geometry);
                    break;
                }
            }
        }
    }
}
