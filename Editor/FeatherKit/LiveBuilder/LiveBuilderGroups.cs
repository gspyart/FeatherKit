using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Группы Live Builder: объект из коробок (<see cref="BoxModel"/>) как граница вырезов, как Model в Chisel и RealtimeCSG.
    /// Внутри группы — свои коробки, стены, здания, вырезы, вложенные группы и любые объекты (мебель). Вырезающие коробки и вырезы снаружи
    /// в группу не заходят, её вырезы режут только внутри (правило вырезов — <see cref="LiveBuilderCutRules"/>).
    /// Здесь — в какой группе лежит объект и две команды с Undo: «Новая группа» внутри выделенного и «В группу» для выделенного.
    /// В здании группа встаёт в его этаж — выбранный в окне, «И этажи выше» — повтор до верха.
    /// Пункты меню — GameObject → FeatherKit, в панели — секция Group (<see cref="LiveBuilderGroupCard"/>).
    /// </summary>
    public static class LiveBuilderGroups
    {
        private const string NewGroupUndoName = "Create Live Builder Group";
        private const string WrapUndoName = "Group Selection";


        // ── Группа объекта ───────────────────────────────────────────────

        // Ближайший объект из коробок над узлом или сам узел — группа, в которой он лежит. Нет такого — null.
        public static BoxModel GroupOf(Transform node)
        {
            for (var current = node; current != null; current = current.parent)
                if (current.TryGetComponent<BoxModel>(out var model))
                    return model;

            return null;
        }


        // ── Новая группа ─────────────────────────────────────────────────

        // Пустая группа внутри выделенного (ничего не выделено — туда же, куда новый объект). После создания — выделена.
        public static BoxModel CreateGroupInside(GameObject selected)
        {
            var undoGroup = BeginUndo(NewGroupUndoName);
            var parent = NewGroupParentFor(selected);
            var group = BoxModelFactory.CreateGroup(parent);

            if (parent == null && selected != null && selected.scene.IsValid())
                KeepInScene(group, selected.scene);

            var building = parent != null ? parent.GetComponentInParent<SplineBuilding>(true) : null;

            if (building != null)
                AttachToWindowStorey(group, building);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = group.gameObject;
            return group;
        }

        // Куда встаёт новая группа: в выделенный объект из коробок или пустой объект-папку (и в этаж здания); рядом — у коробки, стены, выреза
        // и объекта с мешем; здание и его собранные части — в этаж, выбранный в окне.
        private static Transform NewGroupParentFor(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid())
                return null;

            var building = selected.GetComponentInParent<SplineBuilding>(true);

            if (building != null && (selected == building.gameObject || IsGeneratedBuildingPart(selected.transform)))
                return SplineBuildingStoreys.ContainerOf(building, WindowStoreyOf(building)).transform;

            return CanHoldGroup(selected) ? selected.transform : selected.transform.parent;
        }

        // Группу кладут внутрь объекта из коробок или пустого объекта-папки. Коробка, стена, вырез, повтор и объект с мешем — не папки.
        private static bool CanHoldGroup(GameObject selected)
        {
            if (selected.GetComponent<BoxModel>() != null)
                return true;

            var isPiece = BoxModelShapeCollector.IsPiece(selected) || selected.GetComponent<LiveBuilderArray>() != null
                || selected.GetComponent<GeometryCutter>() != null || selected.GetComponent<SplineWall>() != null;

            return !isPiece && selected.GetComponent<Renderer>() == null;
        }

        // Группа в здании — на пол своего этажа (в этаже здания — его, иначе выбранного в окне) целиком:
        // с «И этажи выше» — её коробки на всех этажах выше, одним мешем.
        private static void AttachToWindowStorey(BoxModel group, SplineBuilding building)
        {
            var parent = group.transform.parent;
            var storey = SplineBuildingStoreys.TryGetStoreyOf(parent, out var storeyBuilding, out var parentStorey) && storeyBuilding == building
                ? Mathf.Min(parentStorey, building.StoreyCount - 1)
                : WindowStoreyOf(building);
            var floorPoint = parent.position;
            floorPoint.y = building.FirstFloorWorldHeight + storey * building.StoreyHeight;

            var localFloor = parent.InverseTransformPoint(floorPoint);
            group.transform.localPosition = RoundToMillimeters(new Vector3(0f, localFloor.y, 0f));

            if (LiveBuilderBuildingContext.IsOnAllStoreys)
                SplineBuildingInteriorPlacement.RepeatOnStoreysAbove(group.transform, building, false);
        }

        // Этаж здания, выбранный в окне; у здания такого нет — ближайший.
        private static int WindowStoreyOf(SplineBuilding building)
        {
            return Mathf.Clamp(LiveBuilderStoreys.CurrentStorey - SplineBuildingLevelAlignment.BaseStoreyOf(building), 0, building.StoreyCount - 1);
        }


        // ── В группу ─────────────────────────────────────────────────────

        // Выделенное — в новую группу у их общего родителя, на месте первого; места в мире не меняются. Нельзя — null.
        public static BoxModel GroupObjects(IReadOnlyList<GameObject> objects)
        {
            if (!CanGroup(objects, out _))
                return null;

            var items = TopmostInHierarchyOrder(objects);
            var commonParent = CommonParentOf(items);
            var building = commonParent != null ? commonParent.GetComponentInParent<SplineBuilding>(true) : null;
            var isBuildingItself = building != null && commonParent == building.transform;

            var undoGroup = BeginUndo(WrapUndoName);
            var parent = isBuildingItself ? InteriorFor(building, items) : commonParent;
            var group = BoxModelFactory.CreateGroup(parent);

            if (parent == null)
            {
                group.transform.SetParent(null, false);
                KeepInScene(group, items[0].gameObject.scene);
            }

            if (!isBuildingItself)
                group.transform.SetSiblingIndex(SiblingIndexUnder(commonParent, items[0]));

            group.transform.localPosition = PivotFor(items, parent);

            foreach (var item in items)
                Undo.SetTransformParent(item, group.transform, true, WrapUndoName);

            AttachGroupToStoreys(group, building);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = group.gameObject;
            LiveBuilderRebuildQueue.RequestCheck();
            return group;
        }

        // Выделенное прямо в здании — в этаж на высоте самого нижнего.
        private static Transform InteriorFor(SplineBuilding building, List<Transform> items)
        {
            var lowest = float.MaxValue;

            foreach (var item in items)
                lowest = Mathf.Min(lowest, item.position.y);

            return SplineBuildingStoreys.ContainerAtHeight(building, lowest).transform;
        }

        // Группа в здании — к его этажам. Свои повторы внутри (коробка «И этажи выше») повтор группы перемножил бы — тогда группа без него.
        private static void AttachGroupToStoreys(BoxModel group, SplineBuilding building)
        {
            if (building == null)
                return;

            if (LiveBuilderBuildingContext.IsOnAllStoreys && !HasStoreyRepeatsInside(group.transform))
                SplineBuildingInteriorPlacement.RepeatOnStoreysAbove(group.transform, building, false);
        }

        private static bool HasStoreyRepeatsInside(Transform node)
        {
            foreach (var array in node.GetComponentsInChildren<LiveBuilderArray>(true))
                if (array.transform != node && (array.CountY > 1 || array.CountYToBuildingTop))
                    return true;

            return false;
        }

        // Номер среди соседей общего родителя у того, в ком лежит первый объект (или у него самого). Родитель null — среди корней сцены.
        private static int SiblingIndexUnder(Transform commonParent, Transform item)
        {
            var node = item;

            while (node.parent != commonParent)
                node = node.parent;

            return node.GetSiblingIndex();
        }

        // Точка группы в осях родителя: посередине выделенного в плане, по самому нижнему — в здании по ней берётся этаж.
        // Целые миллиметры: коробки внутри остаются на сетке расчёта.
        private static Vector3 PivotFor(List<Transform> items, Transform parent)
        {
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;

            foreach (var item in items)
            {
                var local = parent != null ? parent.InverseTransformPoint(item.position) : item.position;
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            var center = (min + max) / 2f;
            return RoundToMillimeters(new Vector3(center.x, min.y, center.z));
        }


        // Можно ли завернуть объекты в группу; нельзя — почему, коротко.
        public static bool CanGroup(IReadOnlyList<GameObject> objects, out string reason)
        {
            reason = "";

            if (objects == null || objects.Count == 0)
            {
                reason = "nothing is selected";
                return false;
            }

            foreach (var selected in objects)
            {
                reason = ReasonNotMovable(selected);

                if (reason != null)
                    return false;

                if (selected.scene != objects[0].scene)
                {
                    reason = "objects are from different scenes";
                    return false;
                }
            }

            var items = TopmostInHierarchyOrder(objects);
            var commonParent = CommonParentOf(items);

            foreach (var item in items)
            {
                if (IsModelPiece(item.gameObject) && !IsInsideOwnModel(item, commonParent))
                {
                    reason = "boxes stay inside their own box model: select them together with what lies in the same model";
                    return false;
                }
            }

            reason = "";
            return true;
        }

        // Почему этот объект не перенести в группу; можно — null.
        private static string ReasonNotMovable(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid() || EditorUtility.IsPersistent(selected))
                return "the selection is not scene objects";

            if (IsGeneratedBuildingPart(selected.transform))
                return "generated building parts are built by the building itself";

            if (IsBuildingInterior(selected.transform))
                return "storeys and the roof of a building stay in it";

            if (PrefabUtility.IsPartOfPrefabInstance(selected) && !PrefabUtility.IsOutermostPrefabInstanceRoot(selected))
                return "part of a prefab instance: only the whole instance can move";

            var prefabStage = PrefabStageUtility.GetPrefabStage(selected);

            if (prefabStage != null && prefabStage.prefabContentsRoot == selected)
                return "the prefab root";

            var parent = selected.transform.parent;

            if (selected.TryGetComponent<GeometryCutter>(out _) && parent != null && (parent.GetComponent<SplineWall>() != null
                    || parent.GetComponent<SplineBuilding>() != null || parent.GetComponent<SplineBuildingStorey>() != null))
                return "an opening of a wall or building would stop cutting them inside a group";

            return null;
        }

        // Коробка, комната, лестница или повтор — часть объекта из коробок.
        private static bool IsModelPiece(GameObject selected)
        {
            return selected.GetComponent<BoxModel>() == null && (BoxModelShapeCollector.IsPiece(selected) || selected.GetComponent<LiveBuilderArray>() != null);
        }

        // Группа встанет внутри объекта, которому принадлежит коробка: иначе коробка уедет из своего объекта и его осей.
        private static bool IsInsideOwnModel(Transform piece, Transform commonParent)
        {
            var ownModel = piece.parent != null ? GroupOf(piece.parent) : null;
            return ownModel != null && commonParent != null && commonParent.IsChildOf(ownModel.transform);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Новая группа отмены: всё, что дальше, отменяется одним Ctrl+Z.
        private static int BeginUndo(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }


        // Группа в корне — в сцене выделенного, а не в активной.
        private static void KeepInScene(BoxModel group, Scene scene)
        {
            if (group.transform.parent == null && group.gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(group.gameObject, scene);
        }


        // Собранная часть здания или что-то внутри неё.
        private static bool IsGeneratedBuildingPart(Transform node)
        {
            return node.GetComponentInParent<SplineBuildingPart>(true) != null;
        }

        // Этаж здания или его крыша.
        private static bool IsBuildingInterior(Transform node)
        {
            return node.TryGetComponent<SplineBuildingStorey>(out _);
        }


        // Выделенное без тех, что лежат внутри других выделенных, — по порядку в иерархии.
        private static List<Transform> TopmostInHierarchyOrder(IReadOnlyList<GameObject> objects)
        {
            var items = new List<Transform>();

            foreach (var selected in objects)
            {
                var isInsideOther = false;

                foreach (var other in objects)
                    if (other != selected && selected.transform.IsChildOf(other.transform))
                        isInsideOther = true;

                if (!isInsideOther && !items.Contains(selected.transform))
                    items.Add(selected.transform);
            }

            items.Sort((first, second) => LiveBuilderHierarchyOrder.IsBefore(first, second) ? -1 : LiveBuilderHierarchyOrder.IsBefore(second, first) ? 1 : 0);
            return items;
        }

        // Самый глубокий общий предок родителей объектов. Все в корне или общего нет — null (корень сцены).
        private static Transform CommonParentOf(List<Transform> items)
        {
            for (var candidate = items[0].parent; candidate != null; candidate = candidate.parent)
            {
                var isCommon = true;

                foreach (var item in items)
                    if (item.parent == null || !item.parent.IsChildOf(candidate))
                        isCommon = false;

                if (isCommon)
                    return candidate;
            }

            return null;
        }

        private static Vector3 RoundToMillimeters(Vector3 value)
        {
            return new Vector3(Mathf.Round(value.x * 1000f) / 1000f, Mathf.Round(value.y * 1000f) / 1000f, Mathf.Round(value.z * 1000f) / 1000f);
        }


        // ── Меню ─────────────────────────────────────────────────────────

        [MenuItem("GameObject/FeatherKit/Live Builder Group", false, 11)]
        private static void CreateGroupFromMenu(MenuCommand command)
        {
            if (IsRepeatedMenuCall(command))
                return;

            CreateGroupInside(command.context as GameObject ?? Selection.activeGameObject);
        }


        [MenuItem("GameObject/FeatherKit/Group Selection (Live Builder)", false, 12)]
        private static void GroupSelectionFromMenu(MenuCommand command)
        {
            if (IsRepeatedMenuCall(command))
                return;

            if (CanGroup(Selection.gameObjects, out var reason))
                GroupObjects(Selection.gameObjects);
            else
                Debug.LogWarning("Live Builder: cannot group — " + reason + ".");
        }


        [MenuItem("GameObject/FeatherKit/Group Selection (Live Builder)", true)]
        private static bool CanGroupSelectionFromMenu()
        {
            return Selection.gameObjects.Length > 0;
        }

        // Меню иерархии зовёт пункт по разу на каждый выделенный объект — делаем только на активном.
        private static bool IsRepeatedMenuCall(MenuCommand command)
        {
            return command.context != null && command.context != Selection.activeObject;
        }
    }
}
