using System.Collections.Generic;
using System.Linq;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>Куда встаёт новая группа: рядом с объектами — в их общем родителе, или в корне сцены.</summary>
    public enum ObjectGroupPlacement
    {
        NextToObjects,
        SceneRoot
    }


    /// <summary>
    /// Перенос выделенного в группу <see cref="ObjectGroup"/>, заведение новых групп и разгруппировка.
    /// Объекты переезжают, не сдвигаясь в мире, в прежнем порядке иерархии; один Ctrl+Z отменяет весь перенос.
    /// Что можно переносить, решает тоже он: только верхние из выделенного, только из одной сцены, без частей префаба.
    /// </summary>
    public static class ObjectGroupMover
    {
        private const string MoveUndoName = "Move to Group";
        private const string CreateUndoName = "Create Object Group";
        private const string UngroupUndoName = "Ungroup";


        // ── Что переносим ────────────────────────────────

        // Верхние из выделенного в сцене активного объекта, по порядку иерархии. Части префаба Unity переносить не даёт — их пропускаем.
        public static List<Transform> CollectMovableSelection(out int skippedCount)
        {
            var movable = new List<Transform>();
            skippedCount = 0;

            var activeTransform = Selection.activeTransform;
            var selected = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);

            if (selected.Length == 0)
                return movable;

            var scene = activeTransform != null ? activeTransform.gameObject.scene : selected[0].gameObject.scene;

            foreach (var selectedTransform in selected)
            {
                if (selectedTransform.gameObject.scene != scene || IsInsidePrefabInstance(selectedTransform))
                {
                    skippedCount++;
                    continue;
                }

                movable.Add(selectedTransform);
            }

            movable.Sort(CompareByHierarchyOrder);
            return movable;
        }

        // Объект — часть экземпляра префаба, но не его корень: родителя ему поменять нельзя.
        private static bool IsInsidePrefabInstance(Transform target)
        {
            return PrefabUtility.IsPartOfPrefabInstance(target)
                   && PrefabUtility.GetOutermostPrefabInstanceRoot(target) != target.gameObject;
        }

        // Как в окне Hierarchy: сравниваем номера по пути от корня сцены.
        private static int CompareByHierarchyOrder(Transform first, Transform second)
        {
            var firstPath = SiblingIndexPath(first);
            var secondPath = SiblingIndexPath(second);

            for (var index = 0; index < Mathf.Min(firstPath.Count, secondPath.Count); index++)
            {
                if (firstPath[index] != secondPath[index])
                    return firstPath[index].CompareTo(secondPath[index]);
            }

            return firstPath.Count.CompareTo(secondPath.Count);
        }

        private static List<int> SiblingIndexPath(Transform target)
        {
            var path = new List<int>();

            for (var current = target; current != null; current = current.parent)
                path.Insert(0, current.GetSiblingIndex());

            return path;
        }


        // Можно ли класть объекты в группу: группа не среди них и не внутри них — иначе объект лёг бы сам в себя.
        public static bool CanMoveInto(ObjectGroup group, IReadOnlyList<Transform> objects)
        {
            return objects.Count > 0 && objects.All(movedObject => !group.transform.IsChildOf(movedObject));
        }


        // Все объекты уже лежат прямо в этой группе.
        public static bool IsAlreadyIn(ObjectGroup group, IReadOnlyList<Transform> objects)
        {
            return objects.Count > 0 && objects.All(movedObject => movedObject.parent == group.transform);
        }


        // ── Перенос ──────────────────────────────────────

        public static void MoveInto(ObjectGroup group, IReadOnlyList<Transform> objects)
        {
            if (!CanMoveInto(group, objects))
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(MoveUndoName);
            var undoGroup = Undo.GetCurrentGroup();

            foreach (var movedObject in objects)
                Undo.SetTransformParent(movedObject, group.transform, true, MoveUndoName);

            Undo.CollapseUndoOperations(undoGroup);
            ObjectGroupRecentList.Remember(group);
        }


        // Можно ли разгруппировать: у части префаба детей не переложить.
        public static bool CanUngroup(ObjectGroup group) => !PrefabUtility.IsPartOfPrefabInstance(group);


        // Содержимое — к родителю группы, на её место и в прежнем порядке; сама группа удаляется. Ctrl+Z возвращает всё.
        public static void Ungroup(ObjectGroup group)
        {
            if (!CanUngroup(group))
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UngroupUndoName);
            var undoGroup = Undo.GetCurrentGroup();

            var groupTransform = group.transform;
            var parent = groupTransform.parent;
            var siblingIndex = groupTransform.GetSiblingIndex();
            var children = groupTransform.Cast<Transform>().ToList();

            foreach (var child in children)
            {
                Undo.SetTransformParent(child, parent, true, UngroupUndoName);
                child.SetSiblingIndex(siblingIndex++);
            }

            Undo.DestroyObjectImmediate(group.gameObject);
            Undo.CollapseUndoOperations(undoGroup);
        }


        // ── Новая группа ─────────────────────────────────

        // Новая группа сразу с объектами внутри; без объектов — пустая. Ctrl+Z убирает и группу, и перенос за раз.
        public static ObjectGroup CreateGroupWith(string groupName, IReadOnlyList<Transform> objects, Scene scene, ObjectGroupPlacement placement)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(CreateUndoName);
            var undoGroup = Undo.GetCurrentGroup();

            var group = CreateGroup(groupName, objects, scene, placement);
            MoveInto(group, objects);

            Undo.CollapseUndoOperations(undoGroup);
            return group;
        }

        // Пустой объект с меткой: рядом с объектами — в их общем родителе, с осями родителя; в корне — в корне сцены.
        // В обоих случаях — на месте верхнего из объектов, точка — под центром низа: двигать и вращать здание удобно от его основания.
        private static ObjectGroup CreateGroup(string groupName, IReadOnlyList<Transform> objects, Scene scene, ObjectGroupPlacement placement)
        {
            var commonParent = placement == ObjectGroupPlacement.NextToObjects ? CommonParentOf(objects) : null;
            var parent = commonParent ?? PrefabStageRootOf(scene);

            var groupObject = new GameObject(groupName);
            SceneManager.MoveGameObjectToScene(groupObject, scene);
            groupObject.transform.SetParent(parent, false);
            groupObject.transform.position = BottomCenterOf(objects);

            if (objects.Count > 0)
                groupObject.transform.SetSiblingIndex(BranchUnder(parent, objects[0]).GetSiblingIndex());

            var group = groupObject.AddComponent<ObjectGroup>();

            Undo.RegisterCreatedObjectUndo(groupObject, CreateUndoName);
            return group;
        }

        // Ближайший родитель, внутри которого все объекты; null — общий только корень сцены.
        private static Transform CommonParentOf(IReadOnlyList<Transform> objects)
        {
            if (objects.Count == 0)
                return null;

            var candidate = objects[0].parent;

            while (candidate != null && !objects.All(movedObject => movedObject.IsChildOf(candidate)))
                candidate = candidate.parent;

            return candidate;
        }

        // В режиме префаба корень у сцены один — без общего родителя группа встаёт в него.
        private static Transform PrefabStageRootOf(Scene scene)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null && prefabStage.scene == scene ? prefabStage.prefabContentsRoot.transform : null;
        }

        // Тот прямой ребёнок родителя (или корень сцены), внутри которого лежит объект: на его место и встаёт группа.
        private static Transform BranchUnder(Transform parent, Transform target)
        {
            var branch = target;

            while (branch.parent != parent && branch.parent != null)
                branch = branch.parent;

            return branch;
        }

        // Центр низа по рендерерам; рендереров нет — средняя точка объектов на высоте самого низкого.
        private static Vector3 BottomCenterOf(IReadOnlyList<Transform> objects)
        {
            if (objects.Count == 0)
                return Vector3.zero;

            var renderers = objects.SelectMany(movedObject => movedObject.GetComponentsInChildren<Renderer>()).ToList();

            if (renderers.Count > 0)
            {
                var bounds = renderers[0].bounds;

                foreach (var renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);

                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            var center = objects.Aggregate(Vector3.zero, (sum, movedObject) => sum + movedObject.position) / objects.Count;
            return new Vector3(center.x, objects.Min(movedObject => movedObject.position.y), center.z);
        }
    }
}
