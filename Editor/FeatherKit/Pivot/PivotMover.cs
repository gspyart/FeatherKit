using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Переносит точку опоры (пивот) объекта, не двигая его детей: объект встаёт в новую
    /// точку, дети возвращаются на прежние места в мире. Поворот и масштаб не меняются.
    /// Только для объектов без своей геометрии — меш или коллайдер на нём уехал бы вместе с пивотом.
    /// Исключение — объекты, чья форма записана относительно пивота: их переносит свой <see cref="IShapePivotMover"/>, пересчитывая форму.
    /// Каждый перенос отменяется через Ctrl+Z одним шагом.
    /// </summary>
    public static class PivotMover
    {
        private static IShapePivotMover[] shapePivotMovers;


        // Все переносы с пересчётом формы, какие есть в проекте: находятся по типу один раз.
        private static IShapePivotMover[] ShapePivotMovers => shapePivotMovers ??= TypeCache.GetTypesDerivedFrom<IShapePivotMover>()
            .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IShapePivotMover)Activator.CreateInstance(type))
            .ToArray();


        // ── Можно ли перенести ───────────────────────────

        public static bool CanMovePivot(Transform root) => ExplainWhyCannotMovePivot(root) == null;


        // Почему у объекта пивот не перенести; null — можно.
        public static string ExplainWhyCannotMovePivot(Transform root)
        {
            if (ShapePivotMoverOf(root) != null)
                return null;

            if (root.childCount == 0)
                return $"«{root.name}»: нет детей — переносить пивот не у чего, проще подвинуть сам объект";

            if (root.TryGetComponent<Renderer>(out _) || root.TryGetComponent<Collider>(out _))
                return $"«{root.name}»: на нём самом меш или коллайдер — они уехали бы вместе с пивотом";

            return null;
        }


        // ── Перенос ──────────────────────────────────────

        public static void MoveToChildrenCenter(IReadOnlyList<Transform> roots, ChildrenCenterPoint point)
        {
            var undoName = point == ChildrenCenterPoint.BottomCenter ? "Пивот в центр низа детей" : "Пивот в центр детей";
            MovePivots(roots, root => ChildrenCenterFinder.FindPoint(root, point), undoName);
        }


        // Новый пивот каждого объекта — точка мира от findNewPivot. Кому нельзя — пропускаются с объяснением в консоли.
        public static void MovePivots(IReadOnlyList<Transform> roots, Func<Transform, Vector3> findNewPivot, string undoName)
        {
            var movable = roots.Where(CanMovePivot).ToList();

            foreach (var root in roots.Except(movable))
                Debug.LogWarning(ExplainWhyCannotMovePivot(root), root);

            if (movable.Count == 0)
                return;

            // Точки считаются до переноса: у соседей по выделению они не должны зависеть от того, кого перенесли первым.
            var newPivots = movable.Select(findNewPivot).ToList();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            for (var index = 0; index < movable.Count; index++)
            {
                var root = movable[index];
                var shapePivotMover = ShapePivotMoverOf(root);

                if (shapePivotMover != null)
                {
                    shapePivotMover.MovePivot(root, newPivots[index]);
                    continue;
                }

                Undo.RecordObjects(RootAndChildren(root).ToArray(), undoName);
                MovePivotKeepingChildren(root, newPivots[index]);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static IEnumerable<Transform> RootAndChildren(Transform root) => root.Cast<Transform>().Prepend(root);

        // Внуков не трогаем: они едут вместе со своими родителями-детьми.
        private static void MovePivotKeepingChildren(Transform root, Vector3 newPivot)
        {
            var children = root.Cast<Transform>().ToArray();
            var childPositions = children.Select(child => child.position).ToArray();

            root.position = newPivot;

            for (int i = 0; i < children.Length; i++)
                children[i].position = childPositions[i];
        }


        // ── Общие помощники ──────────────────────────────

        // Перенос с пересчётом формы, которому принадлежит объект; обычный объект — null.
        private static IShapePivotMover ShapePivotMoverOf(Transform root)
        {
            return ShapePivotMovers.FirstOrDefault(mover => mover.IsOwnerOf(root));
        }
    }
}
