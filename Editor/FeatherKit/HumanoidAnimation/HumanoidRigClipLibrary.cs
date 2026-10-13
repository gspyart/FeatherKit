using System.Collections.Generic;
using System.IO;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Клипы рига в окне Animation. Окно показывает клипы из контроллера на Animator, и связь
    /// с ним легко потерять: Ctrl+Z за шаг Create, пересоздание рига. Поэтому:
    /// риг помнит свой контроллер и получает его обратно, когда его выделяют или открывают клип
    /// (не на каждой перерисовке и не внутри Ctrl+Z — иначе ломалась бы история отмен);
    /// любой клип можно подключить к ригу — он сам попадёт в контроллер; клип в контроллере
    /// можно заменить другим, когда исходник переехал в другой файл.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidRigClipLibrary
    {
        private const string ControllerExtension = ".controller";

        static HumanoidRigClipLibrary()
        {
            Selection.selectionChanged += RestoreControllerOfSelectedRig;
        }


        // ── Контроллер ──────────────────────────────

        private static void RestoreControllerOfSelectedRig()
        {
            var rig = HumanoidAnimationRigActions.FindSelectedRig();

            if (rig != null)
                RestoreController(rig);
        }


        /// <summary>Запомнить подключённый контроллер. Ничего не возвращает и не трогает Animator.</summary>
        public static void RememberController(HumanoidAnimationRig rig)
        {
            var current = rig.Animator.runtimeAnimatorController;

            if (current == null || rig.AnimationController == current)
                return;

            rig.AnimationController = current;
            EditorUtility.SetDirty(rig);
        }


        /// <summary>Animator потерял контроллер — вернуть запомненный. Есть другой — запомнить его.</summary>
        public static void RestoreController(HumanoidAnimationRig rig)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RememberController(rig);

            var animator = rig.Animator;

            if (animator.runtimeAnimatorController != null || rig.AnimationController == null)
                return;

            animator.runtimeAnimatorController = rig.AnimationController;
            EditorUtility.SetDirty(animator);
            Debug.Log($"«{rig.name}»: контроллер с клипами был потерян — подключён обратно.", rig);
        }


        // ── Список клипов ──────────────────────────────

        /// <summary>
        /// Клипы рига: все из его контроллера, даже пустые, и записанные в таймлайнах сцены.
        /// Готовые Humanoid-версии в список не попадают.
        /// </summary>
        public static List<AnimationClip> CollectClips(HumanoidAnimationRig rig)
        {
            var clips = new List<AnimationClip>();
            var controller = rig.Animator.runtimeAnimatorController;

            if (controller != null)
            {
                foreach (var clip in controller.animationClips)
                {
                    if (clip != null && !clip.humanMotion && !clips.Contains(clip))
                        clips.Add(clip);
                }
            }

            foreach (var clip in HumanoidSourceClips.CollectFor(rig))
            {
                if (!clips.Contains(clip))
                    clips.Add(clip);
            }

            return clips;
        }


        /// <summary>Клип записан в таймлайн: открывать его надо в окне Timeline.</summary>
        public static bool IsTimelineClip(AnimationClip clip) =>
            HumanoidSourceClips.IsNestedClip(clip) && AssetDatabase.GetAssetPath(clip).EndsWith(".playable");


        // ── Подключить клип ──────────────────────────────

        /// <summary>
        /// Подключить клип к ригу, чтобы окно Animation могло его показать. Клипа нет
        /// в контроллере — добавить, контроллера нет совсем — завести рядом с клипом.
        /// Клип править нельзя — объяснить почему и вернуть false.
        /// </summary>
        public static bool TryAddToRig(HumanoidAnimationRig rig, AnimationClip clip)
        {
            if (clip.humanMotion)
            {
                HumanoidAnimationRigActions.ShowMessage($"«{clip.name}» — готовый Humanoid-клип, сделанный не этой тулой. Ручками его не поправить: у него нет ключей ручек, только «мышцы».");
                return false;
            }

            if (IsTimelineClip(clip))
            {
                HumanoidAnimationRigActions.ShowMessage($"«{clip.name}» записан в Timeline. Откройте таймлайн и правьте его там.");
                return false;
            }

            var path = AssetDatabase.GetAssetPath(clip);
            var isReadOnly = HumanoidSourceClips.IsNestedClip(clip) && !HumanoidAnimationFiles.IsEmbeddedSource(clip);

            if (string.IsNullOrEmpty(path) || isReadOnly || !path.StartsWith("Assets/"))
            {
                HumanoidAnimationRigActions.ShowMessage($"«{clip.name}» только для чтения: он лежит внутри модели или пакета.\n\nВыделите его в Project и нажмите Ctrl+D — получится копия, которую можно править.");
                return false;
            }

            return TryAddToController(rig, clip);
        }


        private static bool TryAddToController(HumanoidAnimationRig rig, AnimationClip clip)
        {
            RestoreController(rig);

            var animator = rig.Animator;

            if (animator.runtimeAnimatorController == null)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath(FolderOf(clip) + "/" + rig.name + ControllerExtension);
                var created = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);

                Undo.RecordObject(animator, "Подключить контроллер");
                animator.runtimeAnimatorController = created;
                RememberController(rig);
                return true;
            }

            if (ArrayUtility.Contains(animator.runtimeAnimatorController.animationClips, clip))
                return true;

            if (!(animator.runtimeAnimatorController is AnimatorController controller))
            {
                HumanoidAnimationRigActions.ShowMessage("К ригу подключён Override-контроллер — в него клип не добавить. Подключите обычный Animator Controller или очистите поле Controller.");
                return false;
            }

            Undo.RecordObject(controller, "Добавить клип в контроллер");
            controller.AddMotion(clip);
            return true;
        }


        /// <summary>Заменить клип в контроллере рига другим: состояния, которые его играли, играют новый.</summary>
        public static void ReplaceClip(HumanoidAnimationRig rig, AnimationClip oldClip, AnimationClip newClip)
        {
            RestoreController(rig);

            if (!(rig.Animator.runtimeAnimatorController is AnimatorController controller))
                return;

            foreach (var layer in controller.layers)
                ReplaceInStateMachine(layer.stateMachine, oldClip, newClip);

            EditorUtility.SetDirty(controller);
        }

        private static void ReplaceInStateMachine(AnimatorStateMachine stateMachine, AnimationClip oldClip, AnimationClip newClip)
        {
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.motion == oldClip)
                    childState.state.motion = newClip;
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
                ReplaceInStateMachine(childStateMachine.stateMachine, oldClip, newClip);
        }


        private static string FolderOf(Object asset)
        {
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset))?.Replace('\\', '/');
            return string.IsNullOrEmpty(folder) ? "Assets" : folder;
        }
    }
}
