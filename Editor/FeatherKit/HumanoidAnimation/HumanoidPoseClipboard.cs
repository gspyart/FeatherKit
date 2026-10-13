using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Буфер позы: скопировать позу тела и вставить в другой кадр — как есть или зеркально,
    /// левое на место правого. Так делают циклы: шаг левой ногой зеркалится в шаг правой,
    /// а первый кадр вставляется в последний, чтобы цикл сомкнулся.
    ///
    /// Поза хранится по костям, а не по ручкам: вставка работает, даже если у рук сейчас другой
    /// режим FK/IK. Вставка ставит ручки текущего режима так, чтобы кости встали в позу, и при
    /// включённой записи ставит ключ позы. Зеркало считается через мышцы Humanoid: у аватара
    /// левая и правая стороны описаны одинаково, поэтому отражение точное у любой модели.
    /// </summary>
    internal static class HumanoidPoseClipboard
    {
        private static Dictionary<HumanBodyBones, BoneInRigSpace> copiedPose;
        private static int[] mirroredMuscles;


        // ── Буфер ──────────────────────────────

        public static bool HasPose => copiedPose != null;


        /// <summary>Скопировать позу, в которой тело стоит сейчас.</summary>
        public static void Copy(HumanoidAnimationRig rig) => copiedPose = ReadPose(rig.Animator, rig.transform);


        /// <summary>
        /// Поставить ручки так, чтобы тело встало в скопированную позу. Зеркально — левое на место
        /// правого. При включённой записи — ключ позы в текущем кадре.
        /// </summary>
        public static void Paste(HumanoidAnimationRig rig, bool mirrored)
        {
            if (!HasPose)
                return;

            var pose = mirrored ? MirrorPose(rig, copiedPose) : copiedPose;
            PlaceControls(rig, pose, mirrored ? "Вставить позу зеркально" : "Вставить позу");

            var clip = HumanoidAnimationWindowRemote.OpenClip;

            if (HumanoidAnimationWindowRemote.IsRecording && HumanoidAnimationWindowRemote.IsPreviewing(clip))
                HumanoidPoseKeys.KeyPose(rig, clip, HumanoidAnimationWindowRemote.CurrentTime);
        }


        // ── Ручки по позе ──────────────────────────────

        // Ручки ставятся сверху вниз: каждая считает своё место от того, куда встанет родитель,
        // а не от того, где он стоит сейчас. Кольца — только поворот: их место задают кости.
        private static void PlaceControls(HumanoidAnimationRig rig, Dictionary<HumanBodyBones, BoneInRigSpace> pose, string undoName)
        {
            var controls = CollectActiveControlsTopDown(rig);
            var targets = new Dictionary<Transform, Pose>();
            var handles = new List<Object>();

            foreach (var control in controls)
                handles.Add(control.Handle);

            Undo.RecordObjects(handles.ToArray(), undoName);

            foreach (var control in controls)
            {
                if (!TryFindTargetPose(rig, control, pose, out var target))
                    continue;

                targets[control.Handle] = target;

                var parent = FindParentTargetPose(rig, control, pose, targets);
                var handle = control.Handle;

                if (control.Type != HumanoidRigControlType.LimbBendHint)
                    handle.localRotation = Quaternion.Inverse(parent.rotation) * target.rotation;

                if (control.Type != HumanoidRigControlType.BoneRotation)
                    handle.localPosition = Matrix4x4.TRS(parent.position, parent.rotation, handle.parent.lossyScale).inverse.MultiplyPoint3x4(target.position);
            }
        }


        private static List<HumanoidRigControl> CollectActiveControlsTopDown(HumanoidAnimationRig rig)
        {
            var controls = new List<HumanoidRigControl>();

            foreach (var control in rig.Controls)
            {
                if (control.Handle != null && rig.IsInActiveMode(control))
                    controls.Add(control);
            }

            controls.Sort((first, second) => Depth(first.Handle).CompareTo(Depth(second.Handle)));
            return controls;
        }

        private static int Depth(Transform transform)
        {
            var depth = 0;

            for (var current = transform; current != null; current = current.parent)
                depth++;

            return depth;
        }


        // Куда должна встать ручка в мире: таз и кубики — на свою кость, кольцо — повернуться
        // как кость, шарик — туда, куда выпирает сустав.
        private static bool TryFindTargetPose(HumanoidAnimationRig rig, HumanoidRigControl control, Dictionary<HumanBodyBones, BoneInRigSpace> pose, out Pose target)
        {
            target = default;
            var root = rig.transform;

            if (control.Type != HumanoidRigControlType.LimbBendHint)
            {
                if (!pose.TryGetValue(control.Bone, out var bone))
                    return false;

                target = bone.ToWorld(root);
                return true;
            }

            var limb = rig.FindLimbOf(control);

            if (limb == null || !pose.TryGetValue(limb.UpperBone, out var upper) || !pose.TryGetValue(limb.MiddleBone, out var middle) || !pose.TryGetValue(limb.EndBone, out var end))
                return false;

            var hasBend = HumanoidLimbModeSwitcher.TryFindBendHintPosition(
                upper.ToWorld(root).position, middle.ToWorld(root).position, end.ToWorld(root).position, out var hintPosition);

            target = new Pose(hasBend ? hintPosition : control.Handle.position, control.Handle.rotation);
            return true;
        }


        // Родитель уже получил место — считаем от него. Родитель — опора пальцев, которая сама
        // встаёт в кисть, — считаем от кисти из позы. Иначе родитель стоит, где стоит.
        private static Pose FindParentTargetPose(HumanoidAnimationRig rig, HumanoidRigControl control, Dictionary<HumanBodyBones, BoneInRigSpace> pose, Dictionary<Transform, Pose> targets)
        {
            var parent = control.Handle.parent;

            if (targets.TryGetValue(parent, out var parentTarget))
                return parentTarget;

            if (rig.FindControlByHandle(parent) == null && TryFindHandOfFinger(control.Bone, out var hand) && pose.TryGetValue(hand, out var handBone))
                return handBone.ToWorld(rig.transform);

            return new Pose(parent.position, parent.rotation);
        }

        private static bool TryFindHandOfFinger(HumanBodyBones bone, out HumanBodyBones hand)
        {
            hand = bone <= HumanBodyBones.LeftLittleDistal ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            return bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;
        }


        // ── Зеркало ──────────────────────────────

        // Отражение через мышцы: поза ставится на скрытую копию скелета, переводится в мышцы,
        // мышцы левой и правой стороны меняются местами, и копия встаёт в отражённую позу.
        private static Dictionary<HumanBodyBones, BoneInRigSpace> MirrorPose(HumanoidAnimationRig rig, Dictionary<HumanBodyBones, BoneInRigSpace> pose)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            var skeleton = Object.Instantiate(rig.gameObject);

            try
            {
                skeleton.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(skeleton, previewScene);

                var animator = skeleton.GetComponent<Animator>();
                var root = skeleton.transform;

                WritePose(animator, root, pose);

                using (var poseHandler = new HumanPoseHandler(animator.avatar, root))
                {
                    var humanPose = new HumanPose();
                    poseHandler.GetHumanPose(ref humanPose);
                    MirrorHumanPose(ref humanPose);
                    poseHandler.SetHumanPose(ref humanPose);
                }

                return ReadPose(animator, root);
            }
            finally
            {
                Object.DestroyImmediate(skeleton);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }


        // Зеркало — плоскость, в которой стоит корень персонажа: правое ⟷ левое.
        // Мышцы сторон меняются местами, наклоны и повороты корпуса влево-вправо меняют знак.
        private static void MirrorHumanPose(ref HumanPose pose)
        {
            pose.bodyPosition.x = -pose.bodyPosition.x;

            var rotation = pose.bodyRotation;
            pose.bodyRotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);

            var mirrorMap = FindMirroredMuscles();
            var muscles = (float[])pose.muscles.Clone();

            for (var i = 0; i < muscles.Length && i < mirrorMap.Length; i++)
            {
                var source = mirrorMap[i];
                pose.muscles[i] = source >= 0 ? muscles[source] : -muscles[i];
            }
        }


        // Для каждой мышцы — мышца, которую она берёт в зеркале. Левая берёт правую и наоборот;
        // мышца корпуса «влево-вправо» берёт себя же с обратным знаком — помечена -1.
        private static int[] FindMirroredMuscles()
        {
            if (mirroredMuscles != null)
                return mirroredMuscles;

            var names = HumanTrait.MuscleName;
            var indices = new Dictionary<string, int>();

            for (var i = 0; i < names.Length; i++)
                indices[names[i]] = i;

            mirroredMuscles = new int[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (name.StartsWith("Left ") && indices.TryGetValue("Right " + name.Substring(5), out var right))
                    mirroredMuscles[i] = right;
                else if (name.StartsWith("Right ") && indices.TryGetValue("Left " + name.Substring(6), out var left))
                    mirroredMuscles[i] = left;
                else
                    mirroredMuscles[i] = name.Contains("Left-Right") ? -1 : i;
            }

            return mirroredMuscles;
        }


        // ── Кости ──────────────────────────────

        private static Dictionary<HumanBodyBones, BoneInRigSpace> ReadPose(Animator animator, Transform root)
        {
            var pose = new Dictionary<HumanBodyBones, BoneInRigSpace>();

            for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                var boneTransform = animator.GetBoneTransform(bone);

                if (boneTransform != null)
                    pose[bone] = BoneInRigSpace.FromWorld(root, boneTransform.position, boneTransform.rotation);
            }

            return pose;
        }


        // Сверху вниз по скелету: поворот родителя уносит детей, поэтому дети ставятся после.
        // Положение — только у таза: длины костей не меняются, остальные встанут сами.
        private static void WritePose(Animator animator, Transform root, Dictionary<HumanBodyBones, BoneInRigSpace> pose)
        {
            var bones = new List<(Transform transform, BoneInRigSpace pose, int depth)>();

            foreach (var pair in pose)
            {
                var boneTransform = animator.GetBoneTransform(pair.Key);

                if (boneTransform != null)
                    bones.Add((boneTransform, pair.Value, Depth(boneTransform)));
            }

            bones.Sort((first, second) => first.depth.CompareTo(second.depth));

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            foreach (var (boneTransform, bonePose, _) in bones)
            {
                var world = bonePose.ToWorld(root);

                if (boneTransform == hips)
                    boneTransform.SetPositionAndRotation(world.position, world.rotation);
                else
                    boneTransform.rotation = world.rotation;
            }
        }


        /// <summary>Положение и поворот кости в пространстве корня персонажа.</summary>
        private readonly struct BoneInRigSpace
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;

            private BoneInRigSpace(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }

            public static BoneInRigSpace FromWorld(Transform root, Vector3 worldPosition, Quaternion worldRotation) =>
                new BoneInRigSpace(root.InverseTransformPoint(worldPosition), Quaternion.Inverse(root.rotation) * worldRotation);

            public Pose ToWorld(Transform root) => new Pose(root.TransformPoint(position), root.rotation * rotation);
        }
    }
}
