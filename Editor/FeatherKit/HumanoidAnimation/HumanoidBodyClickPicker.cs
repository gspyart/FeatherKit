using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Выбор ручки кликом прямо по телу рига в окне сцены: клик по предплечью выделит ручку
    /// предплечья, по кисти — кисть. Рука в IK — кубик кисти, в FK — кольцо сустава, по которому
    /// кликнули. Shift или Ctrl — добавить к выделению. Под мышкой подсвечивается кость,
    /// которую выберет клик.
    ///
    /// Стрелки и кольца инструментов, сами ручки и объекты перед телом важнее: клик по ним
    /// работает как обычно.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidBodyClickPicker
    {
        // Меньше пяти пикселей — забираем клик у обычного выделения рамкой; больше нуля —
        // уступаем стрелкам и кольцам инструментов, когда мышь прямо на них.
        private const float PickDistanceInPixels = 3f;

        private const float HighlightThickness = 5f;
        private const double RigListRefreshInterval = 1.0;

        private static readonly int ControlHint = "FeatherKit.HumanoidBodyClickPicker".GetHashCode();
        private static readonly Color HighlightColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        private static readonly List<HumanoidAnimationRig> Rigs = new List<HumanoidAnimationRig>();

        private static HumanoidAnimationRig hoveredRig;
        private static HumanoidBodyHit hoveredHit;
        private static HumanoidAnimationRig highlightedRig;
        private static HumanBodyBones highlightedBone;
        private static double rigListRefreshTime;

        static HumanoidBodyClickPicker()
        {
            SceneView.duringSceneGui += HandleSceneGUI;
            EditorApplication.hierarchyChanged += () => rigListRefreshTime = 0;
        }


        // ── Окно сцены ──────────────────────────────

        private static void HandleSceneGUI(SceneView sceneView)
        {
            var evt = Event.current;

            if (EditorApplication.isPlaying || Tools.current == Tool.View || evt.alt)
                return;

            var id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    FindHovered(HandleUtility.GUIPointToWorldRay(evt.mousePosition));

                    if (hoveredRig != null)
                        HandleUtility.AddControl(id, PickDistanceInPixels);

                    break;

                case EventType.MouseMove:
                    if (HasHoverChanged())
                        HandleUtility.Repaint();

                    break;

                case EventType.MouseDown:
                    if (evt.button != 0 || HandleUtility.nearestControl != id || hoveredRig == null)
                        break;

                    GUIUtility.hotControl = id;
                    PickAt(evt.mousePosition, evt.shift || EditorGUI.actionKey);
                    evt.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id)
                        break;

                    GUIUtility.hotControl = 0;
                    evt.Use();
                    break;

                case EventType.Repaint:
                    var shouldHighlight = hoveredRig != null && HandleUtility.nearestControl == id && GUIUtility.hotControl == 0;
                    highlightedRig = shouldHighlight ? hoveredRig : null;
                    highlightedBone = hoveredHit.Bone;

                    if (shouldHighlight)
                        DrawHighlight(hoveredHit);

                    break;
            }
        }


        // Перерисовываем, только когда под мышкой другая кость: иначе сцена перерисовывалась бы
        // на каждое движение мыши.
        private static bool HasHoverChanged() =>
            hoveredRig != highlightedRig || hoveredRig != null && hoveredHit.Bone != highlightedBone;


        // Из всех ригов — тот, чьё тело ближе к камере под мышкой.
        private static void FindHovered(Ray ray)
        {
            hoveredRig = null;

            foreach (var rig in CollectRigs())
            {
                if (rig == null || !rig.ControlsShown || !rig.isActiveAndEnabled)
                    continue;

                if (!HumanoidBodyHitTest.TryHit(rig.Animator, ray, rig.HasFingerControls, out var hit))
                    continue;

                if (hoveredRig != null && hit.Depth >= hoveredHit.Depth)
                    continue;

                hoveredRig = rig;
                hoveredHit = hit;
            }
        }


        private static List<HumanoidAnimationRig> CollectRigs()
        {
            if (EditorApplication.timeSinceStartup < rigListRefreshTime)
                return Rigs;

            rigListRefreshTime = EditorApplication.timeSinceStartup + RigListRefreshInterval;
            Rigs.Clear();
            Rigs.AddRange(Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude));
            return Rigs;
        }


        // ── Клик ──────────────────────────────

        // Перед телом что-то стоит — выделяем его, как сделал бы обычный клик. Иначе — ручку кости.
        private static void PickAt(Vector2 mousePosition, bool isAdditive)
        {
            var picked = HandleUtility.PickGameObject(mousePosition, true);

            if (picked != null && !hoveredRig.Contains(picked.transform) && IsInFrontOfBody(picked, mousePosition))
            {
                SelectObjects(new List<Object> { picked }, isAdditive);
                return;
            }

            var handle = FindControlForBone(hoveredRig, hoveredHit.Bone)?.Handle;

            if (handle != null)
                SelectObjects(new List<Object> { handle.gameObject }, isAdditive);
        }


        private static bool IsInFrontOfBody(GameObject picked, Vector2 mousePosition)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            foreach (var renderer in picked.GetComponentsInChildren<Renderer>())
            {
                if (renderer.bounds.IntersectRay(ray, out var distance) && distance < hoveredHit.Depth)
                    return true;
            }

            return false;
        }


        // Shift или Ctrl — добавить к выделению или убрать, если уже выделено.
        private static void SelectObjects(List<Object> objects, bool isAdditive)
        {
            if (!isAdditive)
            {
                Selection.objects = objects.ToArray();
                return;
            }

            var selection = new List<Object>(Selection.objects);

            foreach (var target in objects)
            {
                if (!selection.Remove(target))
                    selection.Add(target);
            }

            Selection.objects = selection.ToArray();
        }


        // ── Кость → ручка ──────────────────────────────

        /// <summary>
        /// Ручка, которой двигают кость. Корпус — своё кольцо, таз — квадрат. Рука или нога в IK:
        /// плечо или бедро — шарик сгиба, ниже — кубик кисти или стопы. В FK — кольцо этой кости.
        /// Пальцы без ручек пальцев — кисть.
        /// </summary>
        public static HumanoidRigControl FindControlForBone(HumanoidAnimationRig rig, HumanBodyBones bone)
        {
            if (bone == HumanBodyBones.Hips)
                return rig.FindControl(bone, HumanoidRigControlType.BodyRoot);

            var isFinger = bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;

            if (isFinger)
            {
                var fingerControl = rig.FindControl(bone, HumanoidRigControlType.BoneRotation);

                if (fingerControl != null)
                    return fingerControl;

                bone = bone <= HumanBodyBones.LeftLittleDistal ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            }

            if (bone == HumanBodyBones.LeftToes)
                bone = HumanBodyBones.LeftFoot;
            else if (bone == HumanBodyBones.RightToes)
                bone = HumanBodyBones.RightFoot;

            var limb = FindLimbOfBone(rig, bone);

            if (limb == null)
                return rig.FindControl(bone, HumanoidRigControlType.BoneRotation) ?? rig.FindControl(bone, HumanoidRigControlType.LimbTarget);

            if (!limb.IsInIK)
                return rig.FindControl(bone, HumanoidRigControlType.BoneRotation);

            return bone == limb.UpperBone
                ? rig.FindControl(limb.MiddleBone, HumanoidRigControlType.LimbBendHint)
                : rig.FindControl(limb.EndBone, HumanoidRigControlType.LimbTarget);
        }

        private static HumanoidRigLimb FindLimbOfBone(HumanoidAnimationRig rig, HumanBodyBones bone)
        {
            foreach (var limb in rig.Limbs)
            {
                if (limb.ContainsBone(bone))
                    return limb;
            }

            return null;
        }


        // ── Подсветка ──────────────────────────────

        private static void DrawHighlight(HumanoidBodyHit hit)
        {
            using (new Handles.DrawingScope(HighlightColor))
            {
                var previousZTest = Handles.zTest;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.DrawLine(hit.From, hit.To, HighlightThickness);
                Handles.zTest = previousZTest;
            }
        }
    }
}
