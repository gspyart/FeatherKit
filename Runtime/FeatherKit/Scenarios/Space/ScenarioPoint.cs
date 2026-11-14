using System;
using System.Collections.Generic;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «точка в мире»: объект, вызвавший, игрок, его глаза, переменная графа или координата.
    /// - позиция — у объекта со смещением в его осях, по желанию — центр его рендереров
    /// - глаза игрока задаёт игра через PlayerEyesFinder; не задано — берётся сам игрок
    /// </summary>
    [Serializable]
    public class ScenarioPoint
    {
        // Общий на все точки: центр рендереров считается редко, и звать его из двух мест разом некому.
        private static readonly List<Renderer> RenderersBuffer = new List<Renderer>();

        private static bool hasWarnedEyesFinderMissing;

        [SerializeField] private ScenarioPointKind kind;

        [Tooltip("The object for the “Object” kind")]
        [SerializeField] private Transform targetTransform;

        [Tooltip("The graph variable for the “Variable” kind: an Object or a Vector3")]
        [SerializeField] private string variableId;

        [Tooltip("The point in world space for the “World Point” kind")]
        [SerializeField] private Vector3 worldPosition;

        [Tooltip("Shift from the point. Along the object's own axes; for a Vector3 variable and a world point — in world axes")]
        [SerializeField] private Vector3 offset;

        [Tooltip("Aim at the middle of the object's renderers instead of its pivot")]
        [SerializeField] private bool usesRenderersCenter;


        public ScenarioPoint()
        {
        }

        public ScenarioPoint(ScenarioPointKind kind)
        {
            this.kind = kind;
        }


        /// <summary>Глаза игрока той сцены, где стоит сценарий. Задаёт игра; не задано — точка «Player Eyes» — сам игрок.</summary>
        public static Func<Component, Transform> PlayerEyesFinder { get; set; }

        public ScenarioPointKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public Transform TargetTransform
        {
            get => targetTransform;
            set => targetTransform = value;
        }

        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }

        public Vector3 WorldPosition
        {
            get => worldPosition;
            set => worldPosition = value;
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        public bool UsesRenderersCenter
        {
            get => usesRenderersCenter;
            set => usesRenderersCenter = value;
        }

        public string Summary => kind switch
        {
            ScenarioPointKind.ThisObject => "this object",
            ScenarioPointKind.Caller => "caller",
            ScenarioPointKind.Player => "player",
            ScenarioPointKind.PlayerEyes => "player eyes",
            ScenarioPointKind.Variable => "graph variable",
            ScenarioPointKind.WorldPoint => $"point {worldPosition}",
            _ => targetTransform != null ? targetTransform.name : "no object selected"
        };


        public static ScenarioPoint FromVariable(string graphVariableId)
        {
            return new ScenarioPoint(ScenarioPointKind.Variable) { variableId = graphVariableId };
        }


        // ── Объект и позиция ─────────────────────────────────────────────

        /// <summary>Объект точки или null: точка задана координатой, вызвал никто, игрока нет, объект не выбран.</summary>
        public Transform ResolveTransform(ScenarioRun run)
        {
            return kind switch
            {
                ScenarioPointKind.ThisObject => run.Owner.transform,
                ScenarioPointKind.Caller => run.Instigator != null ? run.Instigator.transform : null,
                ScenarioPointKind.Player => PlayerTransformOf(run),
                ScenarioPointKind.PlayerEyes => PlayerEyesOf(run),
                ScenarioPointKind.Object => targetTransform,
                ScenarioPointKind.Variable => VariableTransformOf(run),
                _ => null
            };
        }

        private static Transform PlayerTransformOf(ScenarioRun run)
        {
            var player = ScenarioTarget.PlayerFinder?.Invoke(run.Scenario);

            return player != null ? player.transform : null;
        }

        private static Transform PlayerEyesOf(ScenarioRun run)
        {
            if (PlayerEyesFinder == null)
            {
                WarnEyesFinderMissingOnce();
                return PlayerTransformOf(run);
            }

            var eyes = PlayerEyesFinder(run.Scenario);

            return eyes != null ? eyes : PlayerTransformOf(run);
        }

        // Глаза — забота игры: без неё взгляд меряется от ног игрока, и это стоит заметить сразу.
        private static void WarnEyesFinderMissingOnce()
        {
            if (hasWarnedEyesFinderMissing)
                return;

            hasWarnedEyesFinderMissing = true;
            FLog.Warning(("ScenarioPoint.PlayerEyesFinder is not set — the player's pivot is used", FLogColor.Bad));
        }


        // Флаг статический: без перезагрузки домена предупреждение не повторилось бы в следующей игре.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            hasWarnedEyesFinderMissing = false;
        }

        // Переменная Vector3 объекта не даёт — у неё только позиция.
        private Transform VariableTransformOf(ScenarioRun run)
        {
            if (IsVectorVariable(run))
                return null;

            return run.Scenario.GraphVariables.Get<Transform>(variableId);
        }


        /// <summary>Позиция точки со смещением. False — точки нет: объект не найден или не задан.</summary>
        public bool TryGetPosition(ScenarioRun run, out Vector3 position)
        {
            if (kind == ScenarioPointKind.WorldPoint)
            {
                position = worldPosition + offset;
                return true;
            }

            if (kind == ScenarioPointKind.Variable && IsVectorVariable(run))
            {
                position = run.Scenario.GraphVariables.GetVector(variableId) + offset;
                return true;
            }

            var target = ResolveTransform(run);

            if (target == null)
            {
                position = default;
                return false;
            }

            var origin = usesRenderersCenter ? RenderersCenterOf(target) : target.position;
            position = origin + target.rotation * offset;

            return true;
        }

        // Без рендереров — опорная точка объекта: пустой объект тоже бывает целью.
        private static Vector3 RenderersCenterOf(Transform target)
        {
            target.GetComponentsInChildren(RenderersBuffer);

            if (RenderersBuffer.Count == 0)
                return target.position;

            var bounds = RenderersBuffer[0].bounds;

            for (var index = 1; index < RenderersBuffer.Count; index++)
                bounds.Encapsulate(RenderersBuffer[index].bounds);

            RenderersBuffer.Clear();

            return bounds.center;
        }


        private bool IsVectorVariable(ScenarioRun run)
        {
            return run.Scenario.GraphVariables.TryGetDefinition(variableId, out var definition)
                   && definition.Kind == ScenarioVariableKind.Vector3;
        }
    }


    /// <summary>Чем задана точка. Новые виды — только в конец: в данных хранится число.</summary>
    public enum ScenarioPointKind
    {
        [InspectorName("This Object")] ThisObject,
        [InspectorName("Caller")] Caller,
        [InspectorName("Player")] Player,
        [InspectorName("Player Eyes")] PlayerEyes,
        [InspectorName("Object")] Object,
        [InspectorName("Variable")] Variable,
        [InspectorName("World Point")] WorldPoint
    }
}
