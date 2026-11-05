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
    /// в группу не заходят, её вырезы режут только внутри (правило вырезов — <see cref="GeometryCutterLookup"/>, коробок — <see cref="SplineWallRebuilder.OuterModelOf"/>).
    /// Здесь — в какой группе лежит объект и две команды с Undo: «Новая группа» внутри выделенного и «В группу» для выделенного.
    /// В здании группа встаёт в его «Interior» и привязывается к этажу: этаж — выбранный в окне, «И все выше» — из окна.
    /// Пункты меню — GameObject → FeatherKit, в окне — карточка «Группа» (<see cref="LiveBuilderGroupCard"/>).
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

        // Куда встаёт новая группа: в выделенный объект из коробок или пустой объект-папку; рядом — у коробки, стены, выреза и объекта с мешем;
        // здание и его собранные части — в «Interior» здания.
        private static Transform NewGroupParentFor(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid())
                return null;

            var building = selected.GetComponentInParent<SplineBuilding>(true);

            if (building != null && (selected == building.gameObject || IsGeneratedBuildingPart(selected.transform)))
                return BoxModelFactory.InteriorOf(building);

            return CanHoldGroup(selected) ? selected.transform : selected.transform.parent;
        }

        // Группу кладут внутрь объекта из коробок или пустого объекта-папки. Коробка, стена, вырез, повтор и объект с мешем — не папки.
        private static bool CanHoldGroup(GameObject selected)
        {
            if (selected.GetComponent<BoxModel>() != null)
                return true;

            var isPiece = selected.GetComponent<BoxModelPart>() != null || selected.GetComponent<BoxModelRoom>() != null
                || selected.GetComponent<BoxModelStairs>() != null || selected.GetComponent<BoxModelRepeat>() != null
                || selected.GetComponent<GeometryCutter>() != null || selected.GetComponent<SplineWall>() != null;

            return !isPiece && selected.GetComponent<Renderer>() == null;
        }

        // Группа в здании — на пол этажа, выбранного в окне (у здания такого нет — ближайшего), и привязана к нему целиком:
        // с «И все выше» — её коробки на всех этажах выше, одним мешем.
        private static void AttachToWindowStorey(BoxModel group, SplineBuilding building)
        {
            var storey = Mathf.Clamp(LiveBuilderStoreys.CurrentStorey - SplineBuildingLevelAlignment.BaseStoreyOf(building), 0, building.StoreyCount - 1);
            var parent = group.transform.parent;
            var floorPoint = parent.position;
            floorPoint.y = building.FirstFloorWorldHeight + storey * building.StoreyHeight;

            var localFloor = parent.InverseTransformPoint(floorPoint);
            group.transform.localPosition = RoundToMillimeters(new Vector3(0f, localFloor.y, 0f));
            SplineBuildingLinkSync.Attach(group.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys);
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
            var parent = isBuildingItself ? BoxModelFactory.InteriorOf(building) : commonParent;
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

            // Свои привязки внутри (коробка «до верха») привязка группы погасила бы — тогда группа без неё.
            if (building != null && !BoxModelShapeCollector.HasStoreyLinksInside(group.transform))
                SplineBuildingLinkSync.Attach(group.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = group.gameObject;
            LiveBuilderRebuildQueue.RequestCheck();
            return group;
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
                reason = "ничего не выделено";
                return false;
            }

            foreach (var selected in objects)
            {
                reason = ReasonNotMovable(selected);

                if (reason != null)
                    return false;

                if (selected.scene != objects[0].scene)
                {
                    reason = "объекты из разных сцен";
                    return false;
                }
            }

            var items = TopmostInHierarchyOrder(objects);
            var commonParent = CommonParentOf(items);

            foreach (var item in items)
            {
                if (IsModelPiece(item.gameObject) && !IsInsideOwnModel(item, commonParent))
                {
                    reason = "коробки — только внутри своего объекта из коробок: выдели их вместе с тем, что лежит в том же объекте";
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
                return "выделены не объекты сцены";

            if (IsGeneratedBuildingPart(selected.transform))
                return "собранные части здания строит само здание";

            if (IsBuildingInterior(selected.transform))
                return "группу «Interior» здания не переносят";

            if (PrefabUtility.IsPartOfPrefabInstance(selected) && !PrefabUtility.IsOutermostPrefabInstanceRoot(selected))
                return "часть экземпляра префаба — переносится только весь экземпляр";

            var prefabStage = PrefabStageUtility.GetPrefabStage(selected);

            if (prefabStage != null && prefabStage.prefabContentsRoot == selected)
                return "корень префаба";

            if (selected.TryGetComponent<GeometryCutter>(out _) && selected.transform.parent != null
                && (selected.transform.parent.GetComponent<SplineWall>() != null || selected.transform.parent.GetComponent<SplineBuilding>() != null))
                return "проём стены или здания: в группе он перестал бы их резать";

            return null;
        }

        // Коробка, комната, лестница или повтор — часть объекта из коробок.
        private static bool IsModelPiece(GameObject selected)
        {
            return selected.GetComponent<BoxModel>() == null && (selected.GetComponent<BoxModelPart>() != null || selected.GetComponent<BoxModelRoom>() != null
                || selected.GetComponent<BoxModelStairs>() != null || selected.GetComponent<BoxModelRepeat>() != null);
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

        // Группа «Interior» самого здания.
        private static bool IsBuildingInterior(Transform node)
        {
            return node.parent != null && node.parent.TryGetComponent<SplineBuilding>(out var building)
                && BoxModelFactory.HasInterior(building) && BoxModelFactory.InteriorOf(building) == node;
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
                Debug.LogWarning("Live Builder: в группу нельзя — " + reason + ".");
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
