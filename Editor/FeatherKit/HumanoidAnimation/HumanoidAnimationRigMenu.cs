using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Пункты меню тулы: «Анимировать» на персонаже (Hierarchy, Project, главное меню FeatherKit)
    /// или на клипе анимации (Project) и запекание всех клипов ригов сцены.
    /// </summary>
    internal static class HumanoidAnimationRigMenu
    {
        private const string HierarchyMenuPath = "GameObject/FeatherKit/Анимировать гуманоида";
        private const string ProjectMenuPath = "Assets/FeatherKit/Анимировать гуманоида";
        private const string AnimateMenuPath = "FeatherKit/Анимация гуманоида/Анимировать выделенное";
        private const string BakeAllMenuPath = "FeatherKit/Анимация гуманоида/Запечь клипы всех ригов сцены";


        // ── Анимировать ──────────────────────────────

        [MenuItem(HierarchyMenuPath, false, 20)]
        private static void AnimateFromHierarchy(MenuCommand command)
        {
            // Из контекстного меню пункт зовётся по разу на каждый выделенный объект — берём только первый.
            var target = command.context as GameObject;

            if (target != null && Selection.gameObjects.Length > 1 && target != Selection.activeGameObject)
                return;

            HumanoidAnimationLauncher.AnimateCharacter(target != null ? target : Selection.activeGameObject);
        }


        [MenuItem(ProjectMenuPath, false, 20)]
        [MenuItem(AnimateMenuPath, false, 0)]
        private static void AnimateSelection()
        {
            if (Selection.activeObject is AnimationClip clip)
                HumanoidAnimationLauncher.AnimateClip(clip);
            else
                HumanoidAnimationLauncher.AnimateCharacter(Selection.activeGameObject);
        }

        [MenuItem(ProjectMenuPath, true)]
        [MenuItem(AnimateMenuPath, true)]
        private static bool CanAnimateSelection() => Selection.activeObject is AnimationClip || Selection.activeGameObject != null;


        // ── Запекание ──────────────────────────────

        [MenuItem(BakeAllMenuPath, false, 20)]
        private static void BakeAllRigs()
        {
            var rigs = Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude);

            if (rigs.Length == 0)
            {
                HumanoidAnimationRigActions.ShowMessage("В открытых сценах нет ригов для анимации.\n\nВыделите персонажа и выберите FeatherKit → Анимация гуманоида → Анимировать выделенное.");
                return;
            }

            var baked = 0;

            foreach (var rig in rigs)
                baked += HumanoidAnimationRigActions.BakeAll(rig);

            Debug.Log($"Humanoid-клипов запечено: {baked}");
        }
    }
}
