using System;
using FeatherKit.Logging;
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
        private static bool hasWarnedNotImplemented;

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
            WarnNotImplementedOnce();
            return false; // TODO этап 2, рантайм: углы, кадр камеры, дистанция, Clear View.
        }

        // Заглушка до рантайма этапа 2: молча «не смотрит» — и уровень ломается непонятно почему.
        private static void WarnNotImplementedOnce()
        {
            if (hasWarnedNotImplemented)
                return;

            hasWarnedNotImplemented = true;
            FLog.Warning(("Looks At is not implemented yet — it always answers “not looking”", FLogColor.Bad));
        }


        // Флаг статический: без перезагрузки домена предупреждение не повторилось бы в следующей игре.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            hasWarnedNotImplemented = false;
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
