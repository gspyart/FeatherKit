using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Смотрит ли кто-то куда-то — общая часть условия Looks At и события Looked At.
    /// - смотрящий и цель: точка, направление в мире или ось объекта
    /// - угол: конус, раздельно по горизонтали и вертикали или «в кадре камеры»; предельная дистанция
    /// - «Clear View»: луч до цели ничем не перекрыт
    /// </summary>
    [Serializable]
    public class ScenarioGazeCheck
    {
        // Ближе к цели, чем это, луч упирается в то, на чём она стоит, а не в преграду.
        private const float NearTargetDistance = 0.3f;

        // Общие на все проверки: взгляд опрашивается часто, а выделять память в опросе нельзя.
        private static readonly Plane[] FrustumPlanes = new Plane[6];

        [Tooltip("Who looks: player eyes, a monster, a searchlight")]
        [ScenarioLabel("Looker")]
        [SerializeField] private ScenarioPoint looker = new ScenarioPoint(ScenarioPointKind.PlayerEyes);

        [ScenarioLabel("Target Mode")]
        [SerializeField] private ScenarioLookTargetMode targetMode;

        [Tooltip("What to look at for “Point”; the object whose axis to look along for “Object Axis”")]
        [ScenarioLabel("Target")]
        [SerializeField] private ScenarioPoint target = new ScenarioPoint(ScenarioPointKind.ThisObject);

        [Tooltip("The direction to look along for “World Direction”")]
        [ScenarioLabel("Direction")]
        [SerializeField] private Vector3 worldDirection = Vector3.forward;

        [Tooltip("Which axis of the target object to look along for “Object Axis”")]
        [ScenarioLabel("Axis")]
        [SerializeField] private ScenarioObjectAxis objectAxis;

        [ScenarioLabel("Angle Mode")]
        [SerializeField] private ScenarioLookAngleMode angleMode;

        [Tooltip("Degrees from the looker's forward to the target, for “Cone”")]
        [Range(0f, 180f)]
        [ScenarioLabel("Max Angle")]
        [SerializeField] private float maxAngle = 30f;

        [Tooltip("Degrees left or right, in the looker's axes, for “Horizontal & Vertical”")]
        [Range(0f, 180f)]
        [ScenarioLabel("Max Yaw")]
        [SerializeField] private float maxYaw = 30f;

        [Tooltip("Degrees up or down, in the looker's axes, for “Horizontal & Vertical”")]
        [Range(0f, 90f)]
        [ScenarioLabel("Max Pitch")]
        [SerializeField] private float maxPitch = 20f;

        [Tooltip("For “In Camera Frame”: how far inside the frame edge the target must be, in % of the frame")]
        [Range(0f, 50f)]
        [ScenarioLabel("Frame Margin, %")]
        [SerializeField] private float frameMarginPercent;

        [Tooltip("The camera whose frame counts for “In Camera Frame”. Empty — a Camera on the looker, else the main camera "
                 + "(the player's camera driven by Cinemachine: the eyes object itself has no Camera)")]
        [ScenarioLabel("Frame Camera")]
        [SerializeField] private ScenarioValue<Camera> frameCamera = new ScenarioValue<Camera>();

        [Tooltip("Meters. 0 — any distance")]
        [Min(0f)]
        [ScenarioLabel("Max Distance, m")]
        [SerializeField] private float maxDistance;

        [Tooltip("Nothing may block the line from the looker to the target")]
        [ScenarioLabel("Clear View")]
        [SerializeField] private bool requiresClearView;

        [Tooltip("What blocks the view for “Clear View”")]
        [ScenarioLabel("Blocking Layers")]
        [SerializeField] private LayerMask clearViewLayers = ~0;


        public ScenarioPoint Looker => looker;

        public ScenarioLookTargetMode TargetMode
        {
            get => targetMode;
            set => targetMode = value;
        }

        public ScenarioPoint Target => target;

        public Vector3 WorldDirection
        {
            get => worldDirection;
            set => worldDirection = value;
        }

        public ScenarioObjectAxis ObjectAxis
        {
            get => objectAxis;
            set => objectAxis = value;
        }

        public ScenarioLookAngleMode AngleMode
        {
            get => angleMode;
            set => angleMode = value;
        }

        public float MaxAngle
        {
            get => maxAngle;
            set => maxAngle = Mathf.Clamp(value, 0f, 180f);
        }

        public float MaxYaw
        {
            get => maxYaw;
            set => maxYaw = Mathf.Clamp(value, 0f, 180f);
        }

        public float MaxPitch
        {
            get => maxPitch;
            set => maxPitch = Mathf.Clamp(value, 0f, 90f);
        }

        public float FrameMarginPercent
        {
            get => frameMarginPercent;
            set => frameMarginPercent = Mathf.Clamp(value, 0f, 50f);
        }

        public ScenarioValue<Camera> FrameCamera => frameCamera;

        public float MaxDistance
        {
            get => maxDistance;
            set => maxDistance = Mathf.Max(0f, value);
        }

        public bool RequiresClearView
        {
            get => requiresClearView;
            set => requiresClearView = value;
        }

        public LayerMask ClearViewLayers
        {
            get => clearViewLayers;
            set => clearViewLayers = value;
        }

        /// <summary>Подпись для ноды: «player eyes → Silhouette · cone 30° · clear».</summary>
        public string Summary
        {
            get
            {
                var targetText = targetMode switch
                {
                    ScenarioLookTargetMode.Point => target.Summary,
                    ScenarioLookTargetMode.WorldDirection => $"direction {worldDirection}",
                    _ => $"{target.Summary} {objectAxis.ToString().ToLowerInvariant()}"
                };

                var angleText = angleMode switch
                {
                    ScenarioLookAngleMode.Cone => $"cone {maxAngle:0.#}°",
                    ScenarioLookAngleMode.HorizontalAndVertical => $"±{maxYaw:0.#}° × ±{maxPitch:0.#}°",
                    _ => "in frame"
                };

                return $"{looker.Summary} → {targetText} · {angleText}" + (requiresClearView ? " · clear" : "");
            }
        }


        /// <summary>Смотрит ли смотрящий на цель прямо сейчас. Смотрящего или цели нет — не смотрит.</summary>
        public bool IsLooking(ScenarioRun run)
        {
            var lookerTransform = looker.ResolveTransform(run);

            if (lookerTransform == null || !looker.TryGetPosition(run, out var eyePosition))
                return false;

            switch (targetMode)
            {
                case ScenarioLookTargetMode.Point:
                    return IsLookingAtPoint(run, lookerTransform, eyePosition);

                case ScenarioLookTargetMode.WorldDirection:
                    return IsWithinAngle(lookerTransform, worldDirection);

                default:
                    var axisObject = target.ResolveTransform(run);
                    return axisObject != null && IsWithinAngle(lookerTransform, AxisOf(axisObject));
            }
        }

        private bool IsLookingAtPoint(ScenarioRun run, Transform lookerTransform, Vector3 eyePosition)
        {
            if (!target.TryGetPosition(run, out var targetPosition))
                return false;

            var toTarget = targetPosition - eyePosition;

            if (maxDistance > 0f && toTarget.sqrMagnitude > maxDistance * maxDistance)
                return false;

            var targetTransform = target.ResolveTransform(run);
            var isInView = angleMode == ScenarioLookAngleMode.InCameraFrame
                ? IsInCameraFrame(run, lookerTransform, targetPosition, targetTransform)
                : IsWithinAngle(lookerTransform, toTarget);

            return isInView && (!requiresClearView || IsViewClear(eyePosition, targetPosition, targetTransform, lookerTransform));
        }


        // «В кадре» для направления смысла не имеет — там считаем конусом.
        private bool IsWithinAngle(Transform lookerTransform, Vector3 direction)
        {
            if (angleMode != ScenarioLookAngleMode.HorizontalAndVertical)
                return Vector3.Angle(lookerTransform.forward, direction) <= maxAngle;

            var local = lookerTransform.InverseTransformDirection(direction);
            var yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            var pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;

            return Mathf.Abs(yaw) <= maxYaw && Mathf.Abs(pitch) <= maxPitch;
        }

        private Vector3 AxisOf(Transform axisObject)
        {
            return objectAxis switch
            {
                ScenarioObjectAxis.Forward => axisObject.forward,
                ScenarioObjectAxis.Back => -axisObject.forward,
                ScenarioObjectAxis.Up => axisObject.up,
                ScenarioObjectAxis.Down => -axisObject.up,
                ScenarioObjectAxis.Right => axisObject.right,
                _ => -axisObject.right
            };
        }


        // По габаритам рендереров, если цель — их центр: силуэт виден и краем. Иначе — сама точка.
        private bool IsInCameraFrame(ScenarioRun run, Transform lookerTransform, Vector3 targetPosition, Transform targetTransform)
        {
            var camera = FrameCameraOf(run, lookerTransform);

            if (camera == null)
                return false;

            if (target.UsesRenderersCenter && targetTransform != null
                                           && ScenarioPoint.TryGetRenderersBounds(targetTransform, out var bounds))
            {
                GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);

                return GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds)
                       && (frameMarginPercent <= 0f || IsInsideFrameMargin(camera, bounds.center));
            }

            return IsInsideFrameMargin(camera, targetPosition);
        }

        private Camera FrameCameraOf(ScenarioRun run, Transform lookerTransform)
        {
            var chosen = frameCamera.Get(run);

            if (chosen != null)
                return chosen;

            return lookerTransform.TryGetComponent<Camera>(out var lookerCamera) ? lookerCamera : Camera.main;
        }

        private bool IsInsideFrameMargin(Camera camera, Vector3 position)
        {
            var viewport = camera.WorldToViewportPoint(position);
            var margin = frameMarginPercent / 100f;

            return viewport.z > 0f
                   && viewport.x >= margin && viewport.x <= 1f - margin
                   && viewport.y >= margin && viewport.y <= 1f - margin;
        }


        // Упор у самой цели или в саму цель — не помеха: точка на полу или у стены иначе была бы закрыта всегда.
        private bool IsViewClear(Vector3 eyePosition, Vector3 targetPosition, Transform targetTransform, Transform lookerTransform)
        {
            if (!Physics.Linecast(eyePosition, targetPosition, out var hit, clearViewLayers, QueryTriggerInteraction.Ignore))
                return true;

            return (targetTransform != null && hit.transform.IsChildOf(targetTransform))
                   || hit.transform.IsChildOf(lookerTransform)
                   || (targetPosition - hit.point).sqrMagnitude < NearTargetDistance * NearTargetDistance;
        }
    }


    /// <summary>Куда смотреть. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioLookTargetMode
    {
        [InspectorName("Point")] Point,
        [InspectorName("World Direction")] WorldDirection,
        [InspectorName("Object Axis")] ObjectAxis
    }


    /// <summary>Как мерить «смотрит». Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioLookAngleMode
    {
        [InspectorName("Cone")] Cone,
        [InspectorName("Horizontal & Vertical")] HorizontalAndVertical,
        [InspectorName("In Camera Frame")] InCameraFrame
    }


    /// <summary>Ось объекта для «Object Axis». Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioObjectAxis
    {
        [InspectorName("Forward")] Forward,
        [InspectorName("Back")] Back,
        [InspectorName("Up")] Up,
        [InspectorName("Down")] Down,
        [InspectorName("Right")] Right,
        [InspectorName("Left")] Left
    }
}
