using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Переключает руки и ноги между FK и IK. Перед переключением ставит ручки нового
    /// режима ровно в текущую позу, чтобы конечность не прыгнула. Всё через Undo — при
    /// включённой записи окно Animation ставит ключи и ручкам, и весу IK.
    ///
    /// Заодно показывает в сцене только ручки текущего режима, чтобы не путаться.
    /// </summary>
    internal static class HumanoidLimbModeSwitcher
    {

        // ── Переключение ──────────────────────────────

        /// <summary>
        /// Совместить ручки нового режима с текущей позой и переключиться. Без предпросмотра
        /// кости стоят в исходной позе, совмещать не с чем — только переключаемся.
        /// </summary>
        public static void Switch(HumanoidAnimationRig rig, HumanoidRigLimb limb, bool toIK)
        {
            if (AnimationMode.InAnimationMode())
            {
                if (toIK)
                    MatchIKToPose(rig, limb);
                else
                    MatchFKToPose(rig, limb);
            }

            SetIKWeight(limb, toIK ? 1f : 0f);
            SwapSelectionToMode(rig, limb, toIK);
            SyncVisibility(rig);
        }


        // Кольца FK встают по текущим костям, от плеча к кисти: каждое следующее
        // уже знает, куда повернулся родитель.
        private static void MatchFKToPose(HumanoidAnimationRig rig, HumanoidRigLimb limb)
        {
            var bones = new[] { limb.UpperBone, limb.MiddleBone, limb.EndBone };
            var handles = new List<Object>();

            foreach (var bone in bones)
            {
                var control = rig.FindControl(bone, HumanoidRigControlType.BoneRotation);

                if (control != null)
                    handles.Add(control.Handle);
            }

            Undo.RecordObjects(handles.ToArray(), "Совместить FK с позой");

            foreach (var bone in bones)
            {
                var control = rig.FindControl(bone, HumanoidRigControlType.BoneRotation);
                var boneTransform = rig.Animator.GetBoneTransform(bone);

                if (control != null && boneTransform != null)
                    control.Handle.rotation = boneTransform.rotation;
            }
        }


        // Кубик — в кисть или стопу. Шарик — туда, куда сейчас смотрит сгиб. Конечность
        // прямая и сгиба нет — шарик остаётся, где был.
        private static void MatchIKToPose(HumanoidAnimationRig rig, HumanoidRigLimb limb)
        {
            var target = rig.FindControl(limb.EndBone, HumanoidRigControlType.LimbTarget);
            var hint = rig.FindControl(limb.MiddleBone, HumanoidRigControlType.LimbBendHint);

            if (target == null || hint == null)
                return;

            Undo.RecordObjects(new Object[] { target.Handle, hint.Handle }, "Совместить IK с позой");

            var upper = rig.Animator.GetBoneTransform(limb.UpperBone).position;
            var middle = rig.Animator.GetBoneTransform(limb.MiddleBone).position;
            var end = rig.Animator.GetBoneTransform(limb.EndBone);

            target.Handle.SetPositionAndRotation(end.position, end.rotation);

            var limbLength = Vector3.Distance(upper, middle) + Vector3.Distance(middle, end.position);
            var bendDirection = BendDirection(upper, middle, end.position);

            if (bendDirection.sqrMagnitude > 0f)
                hint.Handle.position = middle + bendDirection * (limbLength * HumanoidRigLimbBuilder.BendHintDistance);
        }

        // Куда выпирает сустав: от линии «плечо — кисть» к локтю.
        private static Vector3 BendDirection(Vector3 upper, Vector3 middle, Vector3 end)
        {
            var line = end - upper;

            if (line.sqrMagnitude < 1e-8f)
                return Vector3.zero;

            var closest = upper + Vector3.Project(middle - upper, line);
            var direction = middle - closest;

            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
        }


        /// <summary>Смесь режимов: 0 — FK, 1 — IK. Без совмещения — для плавных переходов.</summary>
        public static void SetIKWeight(HumanoidRigLimb limb, float weight)
        {
            if (limb.IKConstraint == null)
                return;

            Undo.RecordObject(limb.IKConstraint, "Режим IK/FK");
            limb.IKConstraint.weight = weight;
        }


        // Выделена ручка старого режима — выделяем её пару в новом: кубик ↔ кольцо кисти,
        // шарик ↔ кольцо предплечья. Иначе после переключения в руках осталась бы ненужная ручка.
        private static void SwapSelectionToMode(HumanoidAnimationRig rig, HumanoidRigLimb limb, bool toIK)
        {
            var pairs = new[]
            {
                (fk: rig.FindControl(limb.EndBone, HumanoidRigControlType.BoneRotation), ik: rig.FindControl(limb.EndBone, HumanoidRigControlType.LimbTarget)),
                (fk: rig.FindControl(limb.MiddleBone, HumanoidRigControlType.BoneRotation), ik: rig.FindControl(limb.MiddleBone, HumanoidRigControlType.LimbBendHint)),
            };

            var selection = new List<Object>(Selection.objects);
            var changed = false;

            foreach (var (fk, ik) in pairs)
            {
                if (fk == null || ik == null)
                    continue;

                var from = (toIK ? fk : ik).Handle.gameObject;
                var to = (toIK ? ik : fk).Handle.gameObject;
                var index = selection.IndexOf(from);

                if (index < 0)
                    continue;

                selection[index] = to;
                changed = true;
            }

            if (changed)
                Selection.objects = selection.ToArray();
        }


        // ── Видимость ручек ──────────────────────────────

        /// <summary>
        /// Показать ручки текущего режима, спрятать ручки другого. Всё прячется, если
        /// ручки выключены совсем. Меняет только то, что разошлось.
        /// </summary>
        public static void SyncVisibility(HumanoidAnimationRig rig)
        {
            var rigBuilder = rig.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();

            if (rigBuilder == null)
                return;

            var hidden = CollectInactiveModeHandles(rig);
            var changed = false;

            foreach (var effector in rigBuilder.effectors)
            {
                var shouldBeVisible = rig.ControlsShown && !hidden.Contains(effector.transform);

                if (effector.visible == shouldBeVisible)
                    continue;

                effector.visible = shouldBeVisible;
                changed = true;
            }

            if (changed)
                SceneView.RepaintAll();
        }

        private static HashSet<Transform> CollectInactiveModeHandles(HumanoidAnimationRig rig)
        {
            var hidden = new HashSet<Transform>();

            foreach (var control in rig.Controls)
            {
                var limb = rig.FindLimbOf(control);

                if (limb == null || control.Handle == null)
                    continue;

                var isIKControl = control.Type != HumanoidRigControlType.BoneRotation;

                if (isIKControl != limb.IsInIK)
                    hidden.Add(control.Handle);
            }

            return hidden;
        }
    }
}
