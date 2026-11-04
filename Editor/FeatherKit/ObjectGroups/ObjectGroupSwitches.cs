using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Переключатели группы <see cref="ObjectGroup"/> для окна групп, как у коллекций Blender:
    /// включена ли (и в игре), всё ли внутри статичное, видна ли в окне сцены, выделяется ли кликом, Default Parent ли она.
    /// Вкл/выкл и Static пишутся в сцену и отменяются Ctrl+Z; глаз и рука — личный вид редактора, в файл сцены не попадают.
    /// </summary>
    public static class ObjectGroupSwitches
    {
        private const string ActiveUndoName = "Toggle Group";
        private const string StaticUndoName = "Toggle Group Static";


        // ── Вкл / выкл ───────────────────────────────────

        public static bool IsActive(ObjectGroup group) => group.gameObject.activeSelf;


        public static void SetActive(ObjectGroup group, bool isActive)
        {
            Undo.RecordObject(group.gameObject, ActiveUndoName);
            group.gameObject.SetActive(isActive);
        }


        // ── Static ───────────────────────────────────────

        public static bool IsStatic(ObjectGroup group) => group.KeepsContentsStatic;


        // Включил — группа помнит и ставит флаги всему внутри, в том числе новому. Выключил — флаги снимаются со всего внутри.
        public static void SetStatic(ObjectGroup group, bool isStatic)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(StaticUndoName);
            var undoGroup = Undo.GetCurrentGroup();

            var serializedGroup = new SerializedObject(group);
            serializedGroup.FindProperty(ObjectGroup.KeepsContentsStaticField).boolValue = isStatic;
            serializedGroup.ApplyModifiedProperties();

            var flags = isStatic ? ObjectGroupStaticUpdater.AllStaticFlags : 0;

            foreach (var child in group.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(child.gameObject) == flags)
                    continue;

                Undo.RecordObject(child.gameObject, StaticUndoName);
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }


        // ── Глаз: видна ли в окне сцены ──────────────────

        public static bool IsHiddenInScene(ObjectGroup group) => SceneVisibilityManager.instance.IsHidden(group.gameObject);


        public static void SetHiddenInScene(ObjectGroup group, bool isHidden)
        {
            if (isHidden)
                SceneVisibilityManager.instance.Hide(group.gameObject, true);
            else
                SceneVisibilityManager.instance.Show(group.gameObject, true);
        }


        // ── Рука: выделяется ли кликом в сцене ───────────

        public static bool IsPickingDisabled(ObjectGroup group) => SceneVisibilityManager.instance.IsPickingDisabled(group.gameObject);


        public static void SetPickingDisabled(ObjectGroup group, bool isDisabled)
        {
            if (isDisabled)
                SceneVisibilityManager.instance.DisablePicking(group.gameObject, true);
            else
                SceneVisibilityManager.instance.EnablePicking(group.gameObject, true);
        }


        // ── Default Parent ───────────────────────────────

        public static bool IsDefaultParent(ObjectGroup group) =>
            SceneDefaultParent.CurrentIn(group.gameObject.scene) == group.transform;


        public static void SetDefaultParent(ObjectGroup group, bool isDefaultParent)
        {
            if (isDefaultParent)
                SceneDefaultParent.Set(group.transform);
            else
                SceneDefaultParent.Clear(group.gameObject.scene);
        }
    }
}
