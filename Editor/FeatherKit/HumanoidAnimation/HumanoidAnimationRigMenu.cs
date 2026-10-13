using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Пункты меню тулы: создать риг из выделенного персонажа (из Hierarchy, Project
    /// или главного меню FeatherKit) и запечь все клипы ригов сцены.
    /// </summary>
    internal static class HumanoidAnimationRigMenu
    {
        private const string HierarchyMenuPath = "GameObject/FeatherKit/Риг для анимации гуманоида";
        private const string ProjectMenuPath = "Assets/FeatherKit/Риг для анимации гуманоида";
        private const string CreateMenuPath = "FeatherKit/Анимация гуманоида/Создать риг из выделенного";
        private const string BakeAllMenuPath = "FeatherKit/Анимация гуманоида/Запечь клипы всех ригов сцены";


        // ── Создание ──────────────────────────────

        [MenuItem(HierarchyMenuPath, false, 20)]
        private static void CreateFromHierarchy(MenuCommand command)
        {
            // Из контекстного меню пункт зовётся по разу на каждый выделенный объект.
            CreateRig(command.context as GameObject ?? Selection.activeGameObject);
        }


        [MenuItem(ProjectMenuPath, false, 20)]
        [MenuItem(CreateMenuPath, false, 0)]
        private static void CreateFromSelection() => CreateRig(Selection.activeGameObject);

        [MenuItem(ProjectMenuPath, true)]
        [MenuItem(CreateMenuPath, true)]
        private static bool CanCreateFromSelection() => Selection.activeGameObject != null;


        private static void CreateRig(GameObject source)
        {
            if (!HumanoidWorkingCopyCreator.TryFindHumanoidAnimator(source, out var animator, out var problem))
            {
                HumanoidAnimationRigActions.ShowMessage(problem);
                return;
            }

            // В предпросмотре кости стоят в чужой позе — копия запомнила бы её как исходную.
            if (!HumanoidAnimationRigActions.CanChangeRigNow() || !HumanoidAnimationRigActions.TryLeavePreview("создавать риг"))
                return;

            var copy = HumanoidWorkingCopyCreator.Create(animator);
            HumanoidRigControlsBuilder.Build(copy, withFingers: false);

            Undo.RegisterCreatedObjectUndo(copy, "Создать риг для анимации");

            Selection.activeGameObject = copy;
            SceneView.lastActiveSceneView?.FrameSelected();
        }


        // ── Запекание ──────────────────────────────

        [MenuItem(BakeAllMenuPath, false, 20)]
        private static void BakeAllRigs()
        {
            var rigs = Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude);

            if (rigs.Length == 0)
            {
                HumanoidAnimationRigActions.ShowMessage("В открытых сценах нет ригов для анимации.\n\nВыделите персонажа и выберите FeatherKit → Анимация гуманоида → Создать риг из выделенного.");
                return;
            }

            var baked = 0;

            foreach (var rig in rigs)
                baked += HumanoidAnimationRigActions.BakeAll(rig);

            Debug.Log($"Humanoid-клипов запечено: {baked}");
        }
    }
}
