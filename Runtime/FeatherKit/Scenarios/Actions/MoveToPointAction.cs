using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Перенести объект к точке — сразу или плавно за заданное время, по желанию с поворотом её объекта.
    /// Старое поле «point» переносится в новое при загрузке. Для игрока — «Player/Teleport Player».
    /// </summary>
    [Serializable]
    [ScenarioBlock("Transform/Move to Point", Description = "Move an object to a point, at once or smoothly")]
    public class MoveToPointAction : ScenarioLongAction, ISerializationCallbackReceiver
    {
        [Tooltip("Which object to move")]
        [ScenarioLabel("What to Move")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [SerializeField, HideInInspector] private Transform point;

        [Tooltip("Where to: the position and, if checked, the rotation of the point's object")]
        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint destination = new ScenarioPoint(ScenarioPointKind.Object);

        [Tooltip("Rotate the object the same way as the point's object")]
        [ScenarioLabel("Match Point Rotation")]
        [SerializeField] private bool copiesRotation = true;

        [Tooltip("How many seconds to get there. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration;

        [Tooltip("How it speeds up and slows down: horizontal — time, vertical — share of the way covered")]
        [ScenarioLabel("Easing")]
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


        public ScenarioTarget Target => target;

        public ScenarioPoint Destination => destination;

        public override string Summary => $"{target.Summary} → {destination.Summary}" + (duration > 0f ? $" · {duration:0.##} s" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            var moved = target.ResolveRequired(run).transform;

            if (!destination.TryGetPosition(run, out var endPosition))
                throw new InvalidOperationException($"Point “{destination.Summary}” not found");

            var pointTransform = copiesRotation ? destination.ResolveTransform(run) : null;
            var endRotation = pointTransform != null ? pointTransform.rotation : moved.rotation;

            yield return Move(moved, endPosition, endRotation);
        }

        private IEnumerator Move(Transform moved, Vector3 endPosition, Quaternion endRotation)
        {
            var startPosition = moved.position;
            var startRotation = moved.rotation;

            for (var elapsed = 0f; elapsed < duration && moved != null; elapsed += Time.deltaTime)
            {
                var progress = easing.Evaluate(elapsed / duration);

                moved.SetPositionAndRotation(Vector3.LerpUnclamped(startPosition, endPosition, progress),
                    Quaternion.SlerpUnclamped(startRotation, endRotation, progress));

                yield return null;
            }

            if (moved != null)
                moved.SetPositionAndRotation(endPosition, endRotation);
        }


        // ISerializationCallbackReceiver
        public void OnBeforeSerialize()
        {
        }

        // ISerializationCallbackReceiver
        public void OnAfterDeserialize()
        {
            if (ScenarioFieldMigration.IsAssigned(point))
                destination = new ScenarioPoint(ScenarioPointKind.Object) { TargetTransform = point };

            point = null;
        }
    }
}
