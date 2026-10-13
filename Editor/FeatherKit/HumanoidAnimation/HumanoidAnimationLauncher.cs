using System.IO;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// «Анимировать» одной командой — без окон и галок вручную. Персонаж — найти его риг
    /// в сцене или создать. Клип — найти исходник с ручками и риг под него, рига нет —
    /// создать из модели, на которой клип пекли. Риг — открыть его клип или завести новый.
    /// Дальше начинается сеанс: окно Animation, предпросмотр и запись включены.
    ///
    /// Двойной клик по анимации, сделанной тулой, тоже открывает её на риге.
    /// </summary>
    internal static class HumanoidAnimationLauncher
    {
        private const string LastFolderKey = "FeatherKit.HumanoidAnimation.LastFolder";
        private const string DefaultAnimationName = "New Animation";


        // ── Анимировать ──────────────────────────────

        /// <summary>Анимировать персонажа: его риг в сцене или новый.</summary>
        public static void AnimateCharacter(GameObject source)
        {
            var existingRig = source != null ? source.GetComponentInParent<HumanoidAnimationRig>() : null;

            if (existingRig != null)
            {
                AnimateRig(existingRig);
                return;
            }

            if (!HumanoidWorkingCopyCreator.TryFindHumanoidAnimator(source, out var animator, out var problem))
            {
                HumanoidAnimationRigActions.ShowMessage(problem);
                return;
            }

            var rig = FindRigInScene(animator.avatar);

            if (rig == null)
                rig = CreateRig(animator);

            if (rig != null)
                AnimateRig(rig);
        }


        /// <summary>Анимировать риг: клип сеанса, клип из окна Animation или первый клип рига. Клипов нет — завести новый.</summary>
        public static void AnimateRig(HumanoidAnimationRig rig)
        {
            var clip = ChooseClipToOpen(rig);

            if (clip == null)
            {
                CreateNewAnimation(rig);
                return;
            }

            HumanoidAnimationSession.Start(rig, clip);
        }

        private static AnimationClip ChooseClipToOpen(HumanoidAnimationRig rig)
        {
            if (HumanoidAnimationSession.IsActiveOn(rig))
                return HumanoidAnimationSession.ActiveClip;

            var clips = HumanoidRigClipLibrary.CollectClips(rig);
            var openClip = HumanoidAnimationWindowRemote.OpenClip;

            if (openClip != null && clips.Contains(openClip))
                return openClip;

            foreach (var clip in clips)
            {
                if (!HumanoidRigClipLibrary.IsTimelineClip(clip))
                    return clip;
            }

            return null;
        }


        /// <summary>
        /// Анимировать клип: любой клип анимации — найти исходник с ручками и риг под него.
        /// Рига в сцене нет — предложить создать его из модели, на которой клип пекли.
        /// </summary>
        public static void AnimateClip(AnimationClip clip)
        {
            var source = HumanoidAnimationFiles.FindSourceClip(clip);
            var rig = FindRigForClip(source);

            if (rig == null)
            {
                rig = CreateRigForClip(source);

                if (rig == null)
                    return;
            }

            HumanoidAnimationSession.Start(rig, source);
        }


        // Сначала риг, в контроллере которого клип уже есть, потом выделенный, потом риг того
        // же персонажа, на котором клип пекли, потом единственный риг сцены.
        private static HumanoidAnimationRig FindRigForClip(AnimationClip source)
        {
            var rigs = Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude);

            foreach (var rig in rigs)
            {
                var controller = rig.Animator.runtimeAnimatorController;

                if (controller != null && System.Array.IndexOf(controller.animationClips, source) >= 0)
                    return rig;
            }

            var selectedRig = HumanoidAnimationRigActions.FindSelectedRig();

            if (selectedRig != null)
                return selectedRig;

            var bakedOn = HumanoidClipBaker.LoadMarkedAvatar(HumanoidSourceClips.GetHumanoidClipPath(source));

            if (bakedOn != null && FindRigInScene(bakedOn) is HumanoidAnimationRig sameCharacterRig)
                return sameCharacterRig;

            return rigs.Length == 1 ? rigs[0] : null;
        }


        private static HumanoidAnimationRig CreateRigForClip(AnimationClip source)
        {
            var bakedOn = HumanoidClipBaker.LoadMarkedAvatar(HumanoidSourceClips.GetHumanoidClipPath(source));
            var model = bakedOn != null ? AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(bakedOn)) as GameObject : null;

            if (model == null)
            {
                HumanoidAnimationRigActions.ShowMessage($"Не нашлось, на каком персонаже анимировать «{source.name}».\n\nВыделите персонажа и выберите FeatherKit → Анимация гуманоида → Анимировать выделенное, потом откройте клип.");
                return null;
            }

            if (!EditorUtility.DisplayDialog("Анимация гуманоида", $"В сцене нет рига для «{model.name}».\n\nСоздать его и открыть анимацию?", "Создать", "Отмена"))
                return null;

            return HumanoidWorkingCopyCreator.TryFindHumanoidAnimator(model, out var animator, out var problem)
                ? CreateRig(animator)
                : null;
        }


        // ── Новая анимация ──────────────────────────────

        /// <summary>Спросить имя и место, создать файл анимации и сразу начать её анимировать.</summary>
        public static void CreateNewAnimation(HumanoidAnimationRig rig)
        {
            if (!HumanoidAnimationRigActions.CanChangeRigNow())
                return;

            var path = EditorUtility.SaveFilePanelInProject(
                "Новая анимация",
                DefaultAnimationName,
                HumanoidAnimationFiles.FileExtension.TrimStart('.'),
                "Как назвать анимацию? Под этим именем клип увидите в Project и поставите в аниматор игры.",
                ChooseFolder(rig));

            if (string.IsNullOrEmpty(path))
                return;

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                HumanoidAnimationRigActions.ShowMessage($"Файл «{Path.GetFileName(path)}» уже есть. Выберите другое имя — тула не перезаписывает файлы.");
                return;
            }

            EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var source = HumanoidAnimationFiles.Create(path, rig.Animator.avatar);
            HumanoidAnimationSession.Start(rig, source);
        }


        // Куда предложить сохранить: куда сохраняли в прошлый раз, рядом с клипами рига или рядом с моделью.
        private static string ChooseFolder(HumanoidAnimationRig rig)
        {
            var lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);

            if (AssetDatabase.IsValidFolder(lastFolder))
                return lastFolder;

            foreach (var clip in HumanoidRigClipLibrary.CollectClips(rig))
            {
                var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(clip))?.Replace('\\', '/');

                if (AssetDatabase.IsValidFolder(folder))
                    return folder;
            }

            var modelFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(rig.Animator.avatar))?.Replace('\\', '/');
            return AssetDatabase.IsValidFolder(modelFolder) ? modelFolder : "Assets";
        }


        // ── Двойной клик ──────────────────────────────

        // Анимация, сделанная тулой, открывается на риге, а не пустым окном Animation.
#if UNITY_6000_2_OR_NEWER
        [OnOpenAsset]
        private static bool OpenOwnAnimation(EntityId entityId, int line) =>
            TryOpenOwnAnimation(EditorUtility.EntityIdToObject(entityId));
#else
        [OnOpenAsset]
        private static bool OpenOwnAnimation(int instanceId, int line) =>
            TryOpenOwnAnimation(EditorUtility.InstanceIDToObject(instanceId));
#endif

        private static bool TryOpenOwnAnimation(Object asset)
        {
            if (!(asset is AnimationClip clip) || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (!HumanoidClipBaker.IsOwnFile(AssetDatabase.GetAssetPath(clip)))
                return false;

            AnimateClip(clip);
            return true;
        }


        // ── Риг ──────────────────────────────

        /// <summary>Риг этого персонажа в открытых сценах. Нет — null.</summary>
        public static HumanoidAnimationRig FindRigInScene(Avatar avatar)
        {
            foreach (var rig in Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude))
            {
                if (rig.Animator.avatar == avatar)
                    return rig;
            }

            return null;
        }


        /// <summary>Создать рабочую копию персонажа с ручками и выделить её. Нельзя сейчас — null.</summary>
        public static HumanoidAnimationRig CreateRig(Animator source)
        {
            // В предпросмотре кости стоят в чужой позе — копия запомнила бы её как исходную.
            if (!HumanoidAnimationRigActions.CanChangeRigNow() || !HumanoidAnimationRigActions.TryLeavePreview("создавать риг"))
                return null;

            var copy = HumanoidWorkingCopyCreator.Create(source);
            var rig = HumanoidRigControlsBuilder.Build(copy, withFingers: false);

            Undo.RegisterCreatedObjectUndo(copy, "Создать риг для анимации");

            Selection.activeGameObject = copy;
            SceneView.lastActiveSceneView?.FrameSelected();

            return rig;
        }
    }
}
