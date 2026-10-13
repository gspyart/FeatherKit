using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Ключ на всю позу сам. Окно Animation при записи ставит ключ только той ручке, которую
    /// сдвинули, а эта тула дописывает ключи остальным ручкам в том же кадре. Так между ключами
    /// движется всё тело разом, как в обычной анимации «поза к позе», а не каждая ручка по-своему.
    ///
    /// Ждёт, пока отпустишь мышь: пока тянешь, окно само переписывает ключ сдвинутой ручки.
    /// Дописанные ключи отменяются тем же Ctrl+Z, что и сдвиг. Выключается в настройке рига.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidPoseAutoKeyer
    {
        private static HumanoidAnimationRig pendingRig;
        private static int pendingUndoGroup;

        // Подписываемся при загрузке, раньше окна Animation: оно подписывается, когда включают
        // запись, и забирает себе записанные изменения — после него их было бы не видно.
        static HumanoidPoseAutoKeyer()
        {
            Undo.postprocessModifications += NoteMovedControl;
            EditorApplication.update += KeyPendingPose;
        }


        // ── Сдвиг ручки ──────────────────────────────

        private static UndoPropertyModification[] NoteMovedControl(UndoPropertyModification[] modifications)
        {
            if (pendingRig != null || !HumanoidAnimationWindowRemote.IsRecording)
                return modifications;

            foreach (var modification in modifications)
            {
                var rig = FindRigOfMovedControl(modification.currentValue?.target as Transform);

                if (rig == null || !rig.KeyWholePose)
                    continue;

                pendingRig = rig;
                pendingUndoGroup = Undo.GetCurrentGroup();
                break;
            }

            return modifications;
        }

        private static HumanoidAnimationRig FindRigOfMovedControl(Transform moved)
        {
            if (moved == null)
                return null;

            var rig = moved.GetComponentInParent<HumanoidAnimationRig>();
            return rig != null && rig.FindControlByHandle(moved) != null ? rig : null;
        }


        // ── Ключ позы ──────────────────────────────

        // Мышь отпущена — ставим ключ позы в кадре окна и склеиваем его со сдвигом в одну отмену.
        private static void KeyPendingPose()
        {
            if (pendingRig == null || GUIUtility.hotControl != 0)
                return;

            var rig = pendingRig;
            pendingRig = null;

            var clip = HumanoidAnimationWindowRemote.OpenClip;

            if (rig == null || !HumanoidAnimationWindowRemote.IsRecording || !HumanoidAnimationWindowRemote.IsPreviewing(clip))
                return;

            if (!HumanoidRigClipLibrary.CollectClips(rig).Contains(clip))
                return;

            HumanoidPoseKeys.KeyPose(rig, clip, HumanoidAnimationWindowRemote.CurrentTime);
            Undo.CollapseUndoOperations(pendingUndoGroup);
        }
    }
}
