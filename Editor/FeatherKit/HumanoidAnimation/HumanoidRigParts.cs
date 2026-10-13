using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Мастерская одной сборки рига: создаёт ручки, ограничения и фигуры ручек на рабочей
    /// копии и копит список созданных ручек. Что и в каком порядке строить, решают
    /// <see cref="HumanoidRigControlsBuilder"/> и <see cref="HumanoidRigLimbBuilder"/>.
    ///
    /// При пересборке помнит веса старых ограничений по именам: режим IK/FK конечностей
    /// переживает пересборку.
    /// </summary>
    internal sealed class HumanoidRigParts
    {
        private const string ShapesFolder = "Packages/com.unity.animation.rigging/Editor/Shapes/";
        private const string RingShape = "CircleEffector";
        private const string SquareShape = "SquareEffector";
        private const string BoxShape = "BoxEffector";
        private const string BallShape = "BallEffector";

        public static readonly Color CenterColor = new Color(1f, 0.78f, 0.2f, 0.75f);
        public static readonly Color LeftColor = new Color(0.25f, 0.62f, 1f, 0.75f);
        public static readonly Color RightColor = new Color(1f, 0.36f, 0.36f, 0.75f);

        // Кольца и квадраты из пакета лежат в плоскости XZ — смотрят вдоль Y.
        private static readonly Vector3 FlatShapeNormal = Vector3.up;

        private readonly RigBuilder rigBuilder;
        private readonly List<HumanoidRigControl> controls = new List<HumanoidRigControl>();
        private readonly IReadOnlyDictionary<string, float> preservedWeights;

        public HumanoidRigParts(Animator animator, IReadOnlyDictionary<string, float> preservedWeights)
        {
            Animator = animator;
            this.preservedWeights = preservedWeights;

            MeasureBody(animator, out var up, out var forward, out var height);
            Up = up;
            Forward = forward;
            BodyHeight = height;

            ControlsRoot = CreateChild(animator.transform, HumanoidAnimationRig.ControlsRootName).transform;

            var constraintsObject = CreateChild(animator.transform, HumanoidAnimationRig.ConstraintsRootName);
            ConstraintsRoot = constraintsObject.transform;

            var rig = constraintsObject.AddComponent<Rig>();
            rig.weight = 1f;

            rigBuilder = animator.gameObject.GetComponent<RigBuilder>();

            if (rigBuilder == null)
                rigBuilder = animator.gameObject.AddComponent<RigBuilder>();

            // Слои других ригов на этом объекте не трогаем — убираем только опустевшие.
            rigBuilder.layers.RemoveAll(layer => layer.rig == null);
            rigBuilder.layers.Add(new RigLayer(rig));
        }


        // ── Устройство ──────────────────────────────

        public Animator Animator { get; }

        public Transform ControlsRoot { get; }

        public Transform ConstraintsRoot { get; }

        public List<HumanoidRigControl> Controls => controls;

        /// <summary>Рост персонажа. Размеры ручек считаются в его долях.</summary>
        public float BodyHeight { get; }

        /// <summary>«Вверх» по телу: от таза к голове. Не по корню — корень модели бывает повёрнут.</summary>
        public Vector3 Up { get; }

        /// <summary>Куда смотрит грудь персонажа.</summary>
        public Vector3 Forward { get; }


        // ── Ручки ──────────────────────────────

        /// <summary>
        /// Ручка поворота кости с кольцом и ограничением. Кость в аватаре необязательная
        /// (верх груди, шея, пальцы)? Нет кости — нет ручки, вернёт null.
        /// Позу ручки можно задать своей — так руки на старте сгибаются в локте.
        /// </summary>
        public Transform AddBoneRotation(HumanBodyBones bone, Transform parent, Color color, float sizeOfHeight, Vector3? position = null, Quaternion? rotation = null)
        {
            var boneTransform = Animator.GetBoneTransform(bone);

            if (boneTransform == null)
                return null;

            var handle = CreateHandle(ControlName(bone, HumanoidRigControlType.BoneRotation), parent, position ?? boneTransform.position, rotation ?? boneTransform.rotation);

            var constraint = CreateConstraint<MultiRotationConstraint>(handle.name, 1f);
            constraint.data.constrainedObject = boneTransform;
            constraint.data.sourceObjects = SingleSource(handle);
            constraint.data.maintainOffset = false;
            constraint.data.constrainedXAxis = true;
            constraint.data.constrainedYAxis = true;
            constraint.data.constrainedZAxis = true;

            // Кольцо надето на кость: смотрит туда, куда кость растёт.
            AddRing(handle, color, sizeOfHeight, BoneDirection(bone, boneTransform));
            Register(bone, HumanoidRigControlType.BoneRotation, handle);

            return handle;
        }


        public Transform CreateHandle(string handleName, Transform parent, Vector3 position, Quaternion rotation)
        {
            var handle = CreateChild(parent, handleName).transform;
            handle.SetPositionAndRotation(position, rotation);
            return handle;
        }


        public void Register(HumanBodyBones bone, HumanoidRigControlType type, Transform handle) =>
            controls.Add(new HumanoidRigControl(bone, type, handle));


        public Transform FindHandle(HumanBodyBones bone, HumanoidRigControlType type)
        {
            foreach (var control in controls)
            {
                if (control.Bone == bone && control.Type == type)
                    return control.Handle;
            }

            return null;
        }


        /// <summary>Имя ручки. Постоянное: на него ссылаются кривые клипов.</summary>
        public static string ControlName(HumanBodyBones bone, HumanoidRigControlType type)
        {
            var boneName = ObjectNames.NicifyVariableName(bone.ToString());

            switch (type)
            {
                case HumanoidRigControlType.LimbTarget:
                    return boneName + " IK";

                case HumanoidRigControlType.LimbBendHint:
                    return boneName.Replace("Lower Arm", "Elbow").Replace("Lower Leg", "Knee") + " Hint";

                default:
                    return boneName;
            }
        }


        // ── Ограничения ──────────────────────────────

        /// <summary>
        /// Ограничение в отдельном объекте под корнем рига. Порядок создания — порядок
        /// расчёта. Вес берётся из старого рига с тем же именем, если он был.
        /// </summary>
        public T CreateConstraint<T>(string constraintName, float defaultWeight) where T : Component, IRigConstraint
        {
            var objectName = constraintName + " Constraint";
            var constraint = CreateChild(ConstraintsRoot, objectName).AddComponent<T>();

            constraint.weight = preservedWeights != null && preservedWeights.TryGetValue(objectName, out var weight) ? weight : defaultWeight;

            return constraint;
        }


        public static WeightedTransformArray SingleSource(Transform source)
        {
            var sources = new WeightedTransformArray(0);
            sources.Add(new WeightedTransform(source, 1f));
            return sources;
        }


        // ── Фигуры ручек ──────────────────────────────

        public void AddRing(Transform handle, Color color, float sizeOfHeight, Vector3 worldDirection) =>
            AddEffector(handle, RingShape, color, sizeOfHeight, FaceAlong(handle, worldDirection));

        public void AddSquare(Transform handle, Color color, float sizeOfHeight, Vector3 worldDirection) =>
            AddEffector(handle, SquareShape, color, sizeOfHeight, FaceAlong(handle, worldDirection));

        public void AddBox(Transform handle, Color color, float sizeOfHeight) =>
            AddEffector(handle, BoxShape, color, sizeOfHeight, Vector3.zero);

        public void AddBall(Transform handle, Color color, float sizeOfHeight) =>
            AddEffector(handle, BallShape, color, sizeOfHeight, Vector3.zero);


        private void AddEffector(Transform handle, string shapeName, Color color, float sizeOfHeight, Vector3 rotationOffset)
        {
            var style = new RigEffectorData.Style
            {
                shape = AssetDatabase.LoadAssetAtPath<Mesh>(ShapesFolder + shapeName + ".asset"),
                color = color,
                size = sizeOfHeight * BodyHeight,
                position = Vector3.zero,
                rotation = rotationOffset,
            };

            rigBuilder.AddEffector(handle, style);
        }


        // Поворот плоской фигуры так, чтобы она смотрела вдоль направления в мире.
        private static Vector3 FaceAlong(Transform handle, Vector3 worldDirection)
        {
            var localDirection = handle.InverseTransformDirection(worldDirection);
            return Quaternion.FromToRotation(FlatShapeNormal, localDirection).eulerAngles;
        }


        // ── Кости ──────────────────────────────

        // Куда растёт кость: на следующую по цепочке. У головы и кончиков пальцев следующей нет —
        // голова смотрит вверх, кончик продолжает предыдущую фалангу.
        private Vector3 BoneDirection(HumanBodyBones bone, Transform boneTransform)
        {
            var next = FindNextBone(bone);

            if (next != null && (next.position - boneTransform.position).sqrMagnitude > 1e-8f)
                return (next.position - boneTransform.position).normalized;

            if (boneTransform.parent != null && (boneTransform.position - boneTransform.parent.position).sqrMagnitude > 1e-8f && bone != HumanBodyBones.Head)
                return (boneTransform.position - boneTransform.parent.position).normalized;

            return Up;
        }

        private Transform FindNextBone(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Spine: return FirstExisting(HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head);
                case HumanBodyBones.Chest: return FirstExisting(HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head);
                case HumanBodyBones.UpperChest: return FirstExisting(HumanBodyBones.Neck, HumanBodyBones.Head);
                case HumanBodyBones.Neck: return FirstExisting(HumanBodyBones.Head);
                case HumanBodyBones.LeftShoulder: return FirstExisting(HumanBodyBones.LeftUpperArm);
                case HumanBodyBones.RightShoulder: return FirstExisting(HumanBodyBones.RightUpperArm);
                case HumanBodyBones.LeftUpperArm: return FirstExisting(HumanBodyBones.LeftLowerArm);
                case HumanBodyBones.RightUpperArm: return FirstExisting(HumanBodyBones.RightLowerArm);
                case HumanBodyBones.LeftLowerArm: return FirstExisting(HumanBodyBones.LeftHand);
                case HumanBodyBones.RightLowerArm: return FirstExisting(HumanBodyBones.RightHand);
                case HumanBodyBones.LeftUpperLeg: return FirstExisting(HumanBodyBones.LeftLowerLeg);
                case HumanBodyBones.RightUpperLeg: return FirstExisting(HumanBodyBones.RightLowerLeg);
                case HumanBodyBones.LeftLowerLeg: return FirstExisting(HumanBodyBones.LeftFoot);
                case HumanBodyBones.RightLowerLeg: return FirstExisting(HumanBodyBones.RightFoot);
                case HumanBodyBones.LeftFoot: return FirstExisting(HumanBodyBones.LeftToes);
                case HumanBodyBones.RightFoot: return FirstExisting(HumanBodyBones.RightToes);
            }

            var isFingerBone = bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;
            var isLastPhalanx = isFingerBone && (bone - HumanBodyBones.LeftThumbProximal) % 3 == 2;

            return isFingerBone && !isLastPhalanx ? Animator.GetBoneTransform(bone + 1) : null;
        }

        private Transform FirstExisting(params HumanBodyBones[] bones)
        {
            foreach (var bone in bones)
            {
                var boneTransform = Animator.GetBoneTransform(bone);

                if (boneTransform != null)
                    return boneTransform;
            }

            return null;
        }


        // ── Общие помощники ──────────────────────────────

        private static GameObject CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child;
        }


        // Оси и рост по костям, а не по корню: у моделей, повёрнутых при импорте (Z вверх),
        // корень смотрит не туда, и ручки вышли бы крошечными, а локти гнулись бы вбок.
        private static void MeasureBody(Animator animator, out Vector3 up, out Vector3 forward, out float height)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            up = head != null ? (head.position - hips.position).normalized : animator.transform.up;

            var right = (rightLeg.position - leftLeg.position).normalized;
            forward = Vector3.Cross(right, up);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : animator.transform.forward;

            var span = head != null && foot != null ? Vector3.Dot(head.position - foot.position, up) * 1.1f : animator.humanScale * 1.8f;
            height = Mathf.Max(0.1f, span);
        }
    }
}
