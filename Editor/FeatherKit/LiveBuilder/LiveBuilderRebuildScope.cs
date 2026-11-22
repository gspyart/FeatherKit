using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что могла задеть правка — по этому черновой проход очереди (<see cref="LiveBuilderRebuildQueue"/>) сверяет и пересобирает только задетое.
    /// - изменённый объект: внешняя постройка над ним со всем, что в ней, а нет постройки — сам объект со всем, что в нём
    /// - вырез: ещё постройки, которые он резал в прошлый раз (<see cref="GeometryCutterLastTargets"/>) и которые задевает теперь
    /// - пустая область — вся сцена; чистовой проход всегда идёт по всей сцене
    /// </summary>
    public sealed class LiveBuilderRebuildScope
    {
        // Запас к габаритам при поиске построек, которые вырез задевает теперь, м.
        private const float TouchMargin = 0.05f;

        private readonly HashSet<Transform> roots = new();
        private readonly HashSet<GeometryCutter> changedCutters = new();
        private readonly HashSet<Spline> changedSplines = new();
        private readonly HashSet<Component> constructions = new();
        private bool isWholeScene;
        private bool isResolved;


        // Вся сцена: так проходят чистовой проход и вызовы без области.
        public static LiveBuilderRebuildScope WholeScene => new() { isWholeScene = true };


        public bool IsWholeScene => isWholeScene || roots.Count == 0 && changedCutters.Count == 0 && changedSplines.Count == 0;


        // ── Что поменялось ───────────────────────────────────────────────

        // Правка без объекта (кодом, массивом копий) — задеть могла что угодно.
        public void MarkWholeScene()
        {
            isWholeScene = true;
        }


        // Вырезы в изменённом объекте (cuttersInside — их уже нашла очередь) режут и то, что снаружи постройки над ним, — каждый отдельно.
        public void AddChangedObject(GameObject changed, GeometryCutter[] cuttersInside)
        {
            if (changed == null)
                return;

            roots.Add(OuterConstructionOf(changed.transform));
            isResolved = false;

            foreach (var cutter in cuttersInside)
                AddChangedCutter(cutter);
        }

        // Самая внешняя постройка над объектом (или он сам) — объект из коробок, стена по линии, здание; нет такой — сам объект.
        private static Transform OuterConstructionOf(Transform changed)
        {
            var outer = changed;

            for (var node = changed; node != null; node = node.parent)
                if (IsConstruction(node))
                    outer = node;

            return outer;
        }


        public void AddChangedCutter(GeometryCutter cutter)
        {
            changedCutters.Add(cutter);
            isResolved = false;
        }


        // Точку сплайна тянут ручкой: задето то, чей это сплайн, — ищется при проходе.
        public void AddChangedSpline(Spline spline)
        {
            changedSplines.Add(spline);
            isResolved = false;
        }


        // ── Проверка ─────────────────────────────────────────────────────

        // Постройки или сверяемые объекты вида T в области, по сцене или открытому префабу.
        public List<T> Find<T>() where T : Component
        {
            var found = new List<T>();

            foreach (var component in StageUtility.GetCurrentStageHandle().FindComponentsOfType<T>())
                if (Contains(component))
                    found.Add(component);

            return found;
        }


        public bool Contains(Component component)
        {
            if (component == null)
                return false;

            if (IsWholeScene)
                return true;

            Resolve();

            if (constructions.Contains(component))
                return true;

            foreach (var root in roots)
                if (root != null && component.transform.IsChildOf(root))
                    return true;

            return false;
        }


        // ── Разбор вырезов и сплайнов ────────────────────────────────────

        // Сплайны — в их постройки; вырезы — в постройки, что они резали и что задевают теперь. Один раз на проход.
        private void Resolve()
        {
            if (isResolved)
                return;

            isResolved = true;
            constructions.Clear();
            ResolveSplines();

            foreach (var cutter in changedCutters)
            {
                if (cutter == null)
                    continue;

                foreach (var owner in GeometryCutterLastTargets.OwnersCutBy(cutter))
                    AddConstruction(owner);

                AddTouchedNow(cutter);
            }
        }

        private void ResolveSplines()
        {
            if (changedSplines.Count == 0)
                return;

            foreach (var container in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineContainer>())
                foreach (var spline in container.Splines)
                    if (changedSplines.Contains(spline))
                        roots.Add(OuterConstructionOf(container.transform));
        }

        // Часть здания — значит, само здание: его сверяет и собирает пересборщик зданий.
        private void AddConstruction(Component owner)
        {
            constructions.Add(owner);

            if (owner is SplineBuildingPart)
            {
                var building = owner.GetComponentInParent<SplineBuilding>(true);

                if (building != null)
                    constructions.Add(building);
            }
        }

        // По габаритам мешей: постройку, которую вырез прорезал целиком, черновик может не найти — её догонит чистовой проход.
        private void AddTouchedNow(GeometryCutter cutter)
        {
            var cutterBounds = GeometryCutterLookup.WorldBoundsOf(cutter);
            cutterBounds.Expand(TouchMargin * 2f);

            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var model in stage.FindComponentsOfType<BoxModel>())
                AddIfTouched(model, cutterBounds);

            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                AddIfTouched(wall, cutterBounds);

            foreach (var part in stage.FindComponentsOfType<SplineBuildingPart>())
                AddIfTouched(part, cutterBounds);
        }

        private void AddIfTouched(Component owner, Bounds cutterBounds)
        {
            if (owner.TryGetComponent<MeshRenderer>(out var meshRenderer) && meshRenderer.bounds.Intersects(cutterBounds))
                AddConstruction(owner);
        }


        private static bool IsConstruction(Transform node)
        {
            return node.GetComponent<BoxModel>() != null || node.GetComponent<SplineWall>() != null || node.GetComponent<SplineBuilding>() != null
                   || node.GetComponent<LiveBuilderColumn>() != null;
        }
    }
}
