using FeatherKit.HumanoidAnimation;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Строит одну руку или ногу с двумя режимами управления.
    ///
    /// FK — цепочка колец: плечевой сустав → предплечье → кисть (у ноги бедро → голень → стопа).
    /// IK — кубик кисти или стопы и шарик, куда смотрит сгиб. Ограничение IK стоит после FK
    /// и перекрывает его своим весом: 0 — работает FK, 1 — IK.
    ///
    /// Руки на старте чуть согнуты в локте: у прямой руки шарику локтя нечего направлять,
    /// и в IK он бы не работал, пока руку не согнёшь.
    /// </summary>
    internal static class HumanoidRigLimbBuilder
    {
        /// <summary>Как далеко от сустава висит подсказка сгиба, в долях длины конечности.</summary>
        public const float BendHintDistance = 0.5f;

        private const float LimbRotationSize = 0.07f;
        private const float LimbTargetSize = 0.05f;
        private const float LimbBendHintSize = 0.025f;

        private const float ArmRestBendAngle = 15f;

        // Руки по умолчанию в FK — для маха при беге, ноги в IK — чтобы стопы стояли на полу.
        private const float ArmDefaultIKWeight = 0f;
        private const float LegDefaultIKWeight = 1f;


        // ── Руки и ноги ──────────────────────────────

        /// <summary>Режим конечности у нового рига: руки в FK, ноги в IK.</summary>
        public static float DefaultIKWeight(HumanoidLimb limb) =>
            limb == HumanoidLimb.LeftArm || limb == HumanoidLimb.RightArm ? ArmDefaultIKWeight : LegDefaultIKWeight;


        /// <summary>
        /// Рука. FK-цепочка висит на ключице, шарик локтя — сзади на груди: повернул корпус —
        /// локоть повернулся с ним. Кисть в IK — в пространстве всего рига.
        /// </summary>
        public static HumanoidRigLimb BuildArm(HumanoidRigParts parts, bool isLeft, Transform fkParent, Transform torsoTop)
        {
            var limb = new LimbBones(
                isLeft ? HumanoidLimb.LeftArm : HumanoidLimb.RightArm,
                isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

            // Предплечье сгибается вперёд, локоть смотрит назад.
            return Build(parts, limb, isLeft, fkParent, torsoTop, -parts.Forward, parts.Forward, ArmRestBendAngle, ArmDefaultIKWeight);
        }


        /// <summary>
        /// Нога. FK-цепочка висит на тазе. Шарик колена привязан к стопе: повернул стопу —
        /// колено пошло за ней.
        /// </summary>
        public static HumanoidRigLimb BuildLeg(HumanoidRigParts parts, bool isLeft, Transform hipsHandle)
        {
            var limb = new LimbBones(
                isLeft ? HumanoidLimb.LeftLeg : HumanoidLimb.RightLeg,
                isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);

            return Build(parts, limb, isLeft, hipsHandle, null, parts.Forward, -parts.Forward, 0f, LegDefaultIKWeight);
        }


        // ── Сборка ──────────────────────────────

        // hintParent = null — шарик сгиба висит на кубике конечности.
        private static HumanoidRigLimb Build(HumanoidRigParts parts, LimbBones limb, bool isLeft, Transform fkParent, Transform hintParent,
            Vector3 hintDirection, Vector3 restBendDirection, float restBendAngle, float defaultIKWeight)
        {
            var color = isLeft ? HumanoidRigParts.LeftColor : HumanoidRigParts.RightColor;

            var upperBone = parts.Animator.GetBoneTransform(limb.Upper);
            var middleBone = parts.Animator.GetBoneTransform(limb.Middle);
            var endBone = parts.Animator.GetBoneTransform(limb.End);

            var restBend = RestBend(middleBone, endBone, restBendDirection, restBendAngle);
            var endPosition = middleBone.position + restBend * (endBone.position - middleBone.position);
            var endRotation = restBend * endBone.rotation;

            // FK: каждое кольцо вложено в предыдущее — повернул плечо, вся рука пошла за ним.
            var upperHandle = parts.AddBoneRotation(limb.Upper, fkParent, color, LimbRotationSize);
            var middleHandle = parts.AddBoneRotation(limb.Middle, upperHandle, color, LimbRotationSize, rotation: restBend * middleBone.rotation);
            parts.AddBoneRotation(limb.End, middleHandle, color, LimbRotationSize, endPosition, endRotation);

            // IK: после FK, перекрывает его своим весом.
            var limbLength = Vector3.Distance(upperBone.position, middleBone.position) + Vector3.Distance(middleBone.position, endBone.position);

            var target = parts.CreateHandle(HumanoidRigParts.ControlName(limb.End, HumanoidRigControlType.LimbTarget), parts.ControlsRoot, endPosition, endRotation);

            var hintPosition = middleBone.position + hintDirection * (limbLength * BendHintDistance);
            var hint = parts.CreateHandle(HumanoidRigParts.ControlName(limb.Middle, HumanoidRigControlType.LimbBendHint), hintParent != null ? hintParent : target, hintPosition, parts.Animator.transform.rotation);

            var constraint = parts.CreateConstraint<TwoBoneIKConstraint>(target.name, defaultIKWeight);
            constraint.data.root = upperBone;
            constraint.data.mid = middleBone;
            constraint.data.tip = endBone;
            constraint.data.target = target;
            constraint.data.hint = hint;
            constraint.data.targetPositionWeight = 1f;
            constraint.data.targetRotationWeight = 1f;
            constraint.data.hintWeight = 1f;
            constraint.data.maintainTargetPositionOffset = false;
            constraint.data.maintainTargetRotationOffset = false;

            parts.AddBox(target, color, LimbTargetSize);
            parts.AddBall(hint, color, LimbBendHintSize);

            parts.Register(limb.End, HumanoidRigControlType.LimbTarget, target);
            parts.Register(limb.Middle, HumanoidRigControlType.LimbBendHint, hint);

            return new HumanoidRigLimb(limb.Limb, limb.Upper, limb.Middle, limb.End, constraint);
        }


        // Поворот нижнего сегмента в сторону сгиба. Если сегмент уже смотрит туда — сгибать некуда.
        private static Quaternion RestBend(Transform middleBone, Transform endBone, Vector3 bendDirection, float angle)
        {
            if (angle <= 0f)
                return Quaternion.identity;

            var axis = Vector3.Cross((endBone.position - middleBone.position).normalized, bendDirection);

            return axis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(angle, axis.normalized) : Quaternion.identity;
        }


        // Три кости конечности и её название.
        private readonly struct LimbBones
        {
            public readonly HumanoidLimb Limb;
            public readonly HumanBodyBones Upper;
            public readonly HumanBodyBones Middle;
            public readonly HumanBodyBones End;

            public LimbBones(HumanoidLimb limb, HumanBodyBones upper, HumanBodyBones middle, HumanBodyBones end)
            {
                Limb = limb;
                Upper = upper;
                Middle = middle;
                End = end;
            }
        }
    }
}
