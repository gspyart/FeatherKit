using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// «Глазами»: камера окна сцены встаёт туда, откуда смотрит персонаж, — сразу видно, как поза
    /// выглядит в кадре от первого лица. Ещё раз — камера возвращается, где была.
    ///
    /// Точка взгляда не ходит за головой в анимации: камера игры от первого лица крепится
    /// к персонажу, а не к его голове. Своей точки нет — между глаз чуть впереди лица,
    /// по исходной позе. Своя — встал камерой сцены как в игре и нажал «Запомнить вид».
    /// </summary>
    internal static class HumanoidEyesView
    {
        // Точка по умолчанию — от кости головы, в долях её высоты над корнем.
        private const float EyesAboveHeadBone = 0.065f;
        private const float EyesBeforeHeadBone = 0.1f;

        private static SceneView lookingView;
        private static SavedSceneCamera cameraBeforeEyes;


        // ── Смотреть глазами ──────────────────────────────

        /// <summary>Смотрит ли сейчас окно сцены глазами персонажа.</summary>
        public static bool IsLookingThroughEyes => lookingView != null;


        /// <summary>Поставить камеру сцены в глаза персонажа. Камера уже там — вернуть, где была.</summary>
        public static void Toggle(HumanoidAnimationRig rig)
        {
            if (IsLookingThroughEyes)
            {
                ReturnCamera();
                return;
            }

            var view = SceneView.lastActiveSceneView;

            if (view == null)
                return;

            lookingView = view;
            cameraBeforeEyes = new SavedSceneCamera(view);

            FindEyes(rig, out var position, out var rotation, out var fieldOfView);
            PlaceCamera(view, position, rotation, fieldOfView);
        }


        private static void ReturnCamera()
        {
            if (lookingView != null)
                cameraBeforeEyes.Restore(lookingView);

            lookingView = null;
        }


        // Окно сцены встаёт ровно в точку и поворот объекта — для этого временный объект.
        private static void PlaceCamera(SceneView view, Vector3 position, Quaternion rotation, float fieldOfView)
        {
            var eyes = new GameObject("Eyes View") { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                eyes.transform.SetPositionAndRotation(position, rotation);
                view.orthographic = false;
                view.cameraSettings.fieldOfView = fieldOfView;
                view.AlignViewToObject(eyes.transform);
            }
            finally
            {
                Object.DestroyImmediate(eyes);
            }
        }


        // ── Точка взгляда ──────────────────────────────

        /// <summary>Запомнить, откуда сейчас смотрит камера сцены, как глаза персонажа.</summary>
        public static void RememberView(HumanoidAnimationRig rig)
        {
            var view = SceneView.lastActiveSceneView;

            if (view == null)
                return;

            var cameraTransform = view.camera.transform;
            var root = rig.transform;

            Undo.RecordObject(rig, "Запомнить вид глазами");
            rig.SetEyesView(root.InverseTransformPoint(cameraTransform.position), Quaternion.Inverse(root.rotation) * cameraTransform.rotation, view.cameraSettings.fieldOfView);
            EditorUtility.SetDirty(rig);
        }


        /// <summary>Забыть свою точку — снова между глаз по исходной позе.</summary>
        public static void ForgetView(HumanoidAnimationRig rig)
        {
            Undo.RecordObject(rig, "Забыть вид глазами");
            rig.ForgetEyesView();
            EditorUtility.SetDirty(rig);
        }


        private static void FindEyes(HumanoidAnimationRig rig, out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            var root = rig.transform;
            fieldOfView = rig.EyesViewFieldOfView;

            if (rig.HasEyesView)
            {
                position = root.TransformPoint(rig.EyesViewPosition);
                rotation = root.rotation * rig.EyesViewRotation;
                return;
            }

            var headInRig = FindRestHeadPosition(rig);
            var headHeight = Mathf.Max(0.1f, headInRig.y);
            var eyesInRig = headInRig + Vector3.up * (headHeight * EyesAboveHeadBone) + Vector3.forward * (headHeight * EyesBeforeHeadBone);

            position = root.TransformPoint(eyesInRig);
            rotation = root.rotation;
        }


        // Голова в исходной позе, в пространстве рига: складываем исходные положения ручек от таза
        // до головы. Так анимация, открытая сейчас в предпросмотре, точку не сдвигает.
        private static Vector3 FindRestHeadPosition(HumanoidAnimationRig rig)
        {
            var head = rig.FindControl(HumanBodyBones.Head, HumanoidRigControlType.BoneRotation)
                ?? rig.FindControl(HumanBodyBones.Neck, HumanoidRigControlType.BoneRotation);

            if (head == null || rig.ControlsRoot == null)
                return rig.transform.InverseTransformPoint(rig.Animator.GetBoneTransform(HumanBodyBones.Head).position);

            var restMatrix = Matrix4x4.identity;

            for (var current = head.Handle; current != null && current != rig.ControlsRoot; current = current.parent)
            {
                var control = rig.FindControlByHandle(current);
                var localPosition = control != null ? control.RestLocalPosition : current.localPosition;
                var localRotation = control != null ? control.RestLocalRotation : current.localRotation;

                restMatrix = Matrix4x4.TRS(localPosition, localRotation, current.localScale) * restMatrix;
            }

            var controlsRootInRig = rig.transform.worldToLocalMatrix * rig.ControlsRoot.localToWorldMatrix;
            return (controlsRootInRig * restMatrix).MultiplyPoint3x4(Vector3.zero);
        }


        /// <summary>Как стояла камера сцены до «Глазами», чтобы вернуть её назад.</summary>
        private readonly struct SavedSceneCamera
        {
            private readonly Vector3 pivot;
            private readonly Quaternion rotation;
            private readonly float size;
            private readonly bool isOrthographic;
            private readonly float fieldOfView;

            public SavedSceneCamera(SceneView view)
            {
                pivot = view.pivot;
                rotation = view.rotation;
                size = view.size;
                isOrthographic = view.orthographic;
                fieldOfView = view.cameraSettings.fieldOfView;
            }

            public void Restore(SceneView view)
            {
                view.cameraSettings.fieldOfView = fieldOfView;
                view.orthographic = isOrthographic;
                view.LookAt(pivot, rotation, size);
            }
        }
    }
}
