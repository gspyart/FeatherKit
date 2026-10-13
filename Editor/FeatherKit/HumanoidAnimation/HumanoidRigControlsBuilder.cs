using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Строит на рабочей копии гуманоида ручки и ограничения Animation Rigging.
    /// Кости ищет по аватару, поэтому работает с любым правильно настроенным Humanoid.
    ///
    /// Какие ручки:
    /// таз двигает всё тело; позвоночник, шея, голова, ключицы и пальцы — повороты;
    /// руки и ноги — в двух режимах, FK и IK (см. <see cref="HumanoidRigLimbBuilder"/>).
    ///
    /// Имена ручек и ограничений постоянные: по ним клипы находят свои кривые. Пересборка
    /// ставит те же имена и сохраняет режимы конечностей — сделанные анимации не ломаются.
    /// </summary>
    internal static class HumanoidRigControlsBuilder
    {
        private const float BodyRootSize = 0.16f;
        private const float TorsoRotationSize = 0.13f;
        private const float HeadRotationSize = 0.1f;
        private const float ShoulderRotationSize = 0.06f;
        private const float FingerRotationSize = 0.018f;

        private const string FingersSpaceSuffix = " Fingers Space";

        private static readonly HumanBodyBones[] TorsoChain =
        {
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
        };


        // ── Точки входа ──────────────────────────────

        /// <summary>Построить риг на свежей рабочей копии.</summary>
        public static HumanoidAnimationRig Build(GameObject workingCopy, bool withFingers)
        {
            var rig = workingCopy.AddComponent<HumanoidAnimationRig>();

            BuildInto(rig, withFingers, null);

            return rig;
        }


        /// <summary>
        /// Снести ручки и ограничения и построить заново. Анимации остаются рабочими:
        /// имена те же, режимы IK/FK конечностей сохраняются. Поза ручек возвращается к исходной.
        /// </summary>
        public static void Rebuild(HumanoidAnimationRig rig, bool withFingers)
        {
            Undo.RegisterFullObjectHierarchyUndo(rig.gameObject, "Пересоздать ручки");

            var preservedWeights = CollectConstraintWeights(rig);

            RemoveBuiltParts(rig);
            BuildInto(rig, withFingers, preservedWeights);

            Undo.RegisterCreatedObjectUndo(rig.ControlsRoot.gameObject, "Пересоздать ручки");
            Undo.RegisterCreatedObjectUndo(rig.ConstraintsRoot.gameObject, "Пересоздать ручки");
        }


        private static Dictionary<string, float> CollectConstraintWeights(HumanoidAnimationRig rig)
        {
            var weights = new Dictionary<string, float>();

            if (rig.ConstraintsRoot == null)
                return weights;

            foreach (var constraint in rig.ConstraintsRoot.GetComponentsInChildren<IRigConstraint>(true))
            {
                if (constraint is Component component)
                    weights[component.gameObject.name] = constraint.weight;
            }

            return weights;
        }


        // Фигуры ручек снимаем до удаления самих ручек: потом их уже не найти по объекту.
        // Только свои — под корнем ручек или уже потерявшие объект.
        private static void RemoveBuiltParts(HumanoidAnimationRig rig)
        {
            var rigBuilder = rig.GetComponent<RigBuilder>();

            if (rigBuilder != null)
            {
                var effectorTransforms = new List<Transform>();

                foreach (var effector in rigBuilder.effectors)
                {
                    if (effector.transform == null || rig.ControlsRoot != null && effector.transform.IsChildOf(rig.ControlsRoot))
                        effectorTransforms.Add(effector.transform);
                }

                foreach (var effectorTransform in effectorTransforms)
                    rigBuilder.RemoveEffector(effectorTransform);
            }

            if (rig.ControlsRoot != null)
                Undo.DestroyObjectImmediate(rig.ControlsRoot.gameObject);

            if (rig.ConstraintsRoot != null)
                Undo.DestroyObjectImmediate(rig.ConstraintsRoot.gameObject);
        }


        // Порядок важен: ограничения считаются сверху вниз. Сначала таз и позвоночник,
        // потом ключицы, руки и ноги, в конце пальцы — каждая кость опирается на уже готовые.
        private static void BuildInto(HumanoidAnimationRig rig, bool withFingers, IReadOnlyDictionary<string, float> preservedWeights)
        {
            var parts = new HumanoidRigParts(rig.Animator, preservedWeights);
            var limbs = new List<HumanoidRigLimb>();

            var hips = AddBodyRoot(parts);
            var torsoTop = AddTorsoChain(parts, hips);

            var leftShoulder = parts.AddBoneRotation(HumanBodyBones.LeftShoulder, torsoTop, HumanoidRigParts.LeftColor, ShoulderRotationSize);
            var rightShoulder = parts.AddBoneRotation(HumanBodyBones.RightShoulder, torsoTop, HumanoidRigParts.RightColor, ShoulderRotationSize);

            limbs.Add(HumanoidRigLimbBuilder.BuildArm(parts, true, leftShoulder != null ? leftShoulder : torsoTop, torsoTop));
            limbs.Add(HumanoidRigLimbBuilder.BuildArm(parts, false, rightShoulder != null ? rightShoulder : torsoTop, torsoTop));
            limbs.Add(HumanoidRigLimbBuilder.BuildLeg(parts, true, hips));
            limbs.Add(HumanoidRigLimbBuilder.BuildLeg(parts, false, hips));

            if (withFingers)
            {
                AddHandFingers(parts, HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanoidRigParts.LeftColor);
                AddHandFingers(parts, HumanBodyBones.RightHand, HumanBodyBones.RightThumbProximal, HumanoidRigParts.RightColor);
            }

            rig.AssignBuiltRig(parts.ControlsRoot, parts.ConstraintsRoot, parts.Controls, limbs, withFingers);
            EditorUtility.SetDirty(rig);
        }


        // ── Корпус ──────────────────────────────

        private static Transform AddBodyRoot(HumanoidRigParts parts)
        {
            var hipsBone = parts.Animator.GetBoneTransform(HumanBodyBones.Hips);
            var handle = parts.CreateHandle(HumanoidRigParts.ControlName(HumanBodyBones.Hips, HumanoidRigControlType.BodyRoot), parts.ControlsRoot, hipsBone.position, hipsBone.rotation);

            var constraint = parts.CreateConstraint<MultiParentConstraint>(handle.name, 1f);
            constraint.data.constrainedObject = hipsBone;
            constraint.data.sourceObjects = HumanoidRigParts.SingleSource(handle);
            constraint.data.maintainPositionOffset = false;
            constraint.data.maintainRotationOffset = false;
            constraint.data.constrainedPositionXAxis = true;
            constraint.data.constrainedPositionYAxis = true;
            constraint.data.constrainedPositionZAxis = true;
            constraint.data.constrainedRotationXAxis = true;
            constraint.data.constrainedRotationYAxis = true;
            constraint.data.constrainedRotationZAxis = true;

            // Квадрат лежит горизонтально, как бы ни была повёрнута кость таза.
            parts.AddSquare(handle, HumanoidRigParts.CenterColor, BodyRootSize, parts.Up);
            parts.Register(HumanBodyBones.Hips, HumanoidRigControlType.BodyRoot, handle);

            return handle;
        }


        // Позвоночник до головы — цепочка: ручка каждой кости вложена в ручку предыдущей,
        // поэтому наклон спины уносит за собой грудь и голову. Возвращает верх груди.
        private static Transform AddTorsoChain(HumanoidRigParts parts, Transform hipsHandle)
        {
            var parent = hipsHandle;
            var torsoTop = hipsHandle;

            foreach (var bone in TorsoChain)
            {
                var size = bone == HumanBodyBones.Head ? HeadRotationSize : TorsoRotationSize;
                var handle = parts.AddBoneRotation(bone, parent, HumanoidRigParts.CenterColor, size);

                if (handle == null)
                    continue;

                parent = handle;

                if (bone != HumanBodyBones.Neck && bone != HumanBodyBones.Head)
                    torsoTop = handle;
            }

            return torsoTop;
        }


        // ── Пальцы ──────────────────────────────

        // Пальцы висят на «пространстве кисти» — пустышке, которую ограничение ставит ровно
        // в кость кисти. Так кольца пальцев идут за кистью и в FK, и в IK.
        // Фаланги одного пальца — цепочка: согнул основание, кончик пошёл за ним.
        private static void AddHandFingers(HumanoidRigParts parts, HumanBodyBones hand, HumanBodyBones firstFingerBone, Color color)
        {
            const int FingersPerHand = 5;
            const int BonesPerFinger = 3;

            var handBone = parts.Animator.GetBoneTransform(hand);
            var handName = ObjectNames.NicifyVariableName(hand.ToString()).Replace(" Hand", string.Empty);
            var space = parts.CreateHandle(handName + FingersSpaceSuffix, parts.ControlsRoot, handBone.position, handBone.rotation);

            var spaceConstraint = parts.CreateConstraint<MultiParentConstraint>(space.name, 1f);
            spaceConstraint.data.constrainedObject = space;
            spaceConstraint.data.sourceObjects = HumanoidRigParts.SingleSource(handBone);
            spaceConstraint.data.maintainPositionOffset = false;
            spaceConstraint.data.maintainRotationOffset = false;
            spaceConstraint.data.constrainedPositionXAxis = true;
            spaceConstraint.data.constrainedPositionYAxis = true;
            spaceConstraint.data.constrainedPositionZAxis = true;
            spaceConstraint.data.constrainedRotationXAxis = true;
            spaceConstraint.data.constrainedRotationYAxis = true;
            spaceConstraint.data.constrainedRotationZAxis = true;

            for (var finger = 0; finger < FingersPerHand; finger++)
            {
                var parent = space;

                for (var phalanx = 0; phalanx < BonesPerFinger; phalanx++)
                {
                    var bone = firstFingerBone + finger * BonesPerFinger + phalanx;
                    var handle = parts.AddBoneRotation(bone, parent, color, FingerRotationSize);

                    if (handle == null)
                        break;

                    parent = handle;
                }
            }
        }
    }
}
