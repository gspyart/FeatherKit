using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Повернуть объект лицом к точке — сразу или плавно за заданное время. «Horizontal Only» — только вокруг вертикали.</summary>
    [Serializable]
    [ScenarioBlock("Transform/Rotate To", Description = "Turn an object to face a point, at once or smoothly")]
    public class RotateToAction : ScenarioLongAction
    {
        [ScenarioLabel("What to Rotate")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [ScenarioLabel("Face Point")]
        [SerializeField] private ScenarioPoint facePoint = new ScenarioPoint(ScenarioPointKind.Player);

        [Tooltip("Turn only around the vertical axis: the object stays upright")]
        [ScenarioLabel("Horizontal Only")]
        [SerializeField] private bool isHorizontalOnly = true;

        [Tooltip("How many seconds to turn. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration;


        public ScenarioTarget Target => target;

        public ScenarioPoint FacePoint => facePoint;

        public override string Summary => $"{target.Summary} → {facePoint.Summary}" + (duration > 0f ? $" · {duration:0.##} s" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            var rotated = target.ResolveRequired(run).transform;

            if (!facePoint.TryGetPosition(run, out var pointPosition))
                throw new InvalidOperationException($"Point “{facePoint.Summary}” not found");

            var direction = pointPosition - rotated.position;

            if (isHorizontalOnly)
                direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                yield break;

            var startRotation = rotated.rotation;
            var endRotation = Quaternion.LookRotation(direction);

            for (var elapsed = 0f; elapsed < duration && rotated != null; elapsed += Time.deltaTime)
            {
                rotated.rotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
                yield return null;
            }

            if (rotated != null)
                rotated.rotation = endRotation;
        }
    }
}
