using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Перенести объект к точке — сразу или плавно за заданное время, по желанию с её поворотом.
    /// Для игрока — «Player/Teleport Player»: перемещение игрока само обновляет физику и камеру.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Transform/Move to Point")]
    public class MoveToPointAction : ScenarioLongAction
    {
        [Tooltip("Which object to move")]
        [ScenarioLabel("What to Move")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [Tooltip("Where to: the position and, if checked, the rotation of this point")]
        [ScenarioLabel("Point")]
        [SerializeField] private Transform point;

        [Tooltip("Rotate the object the same way as the point")]
        [ScenarioLabel("Match Point Rotation")]
        [SerializeField] private bool copiesRotation = true;

        [Tooltip("How many seconds to get there. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration;

        [Tooltip("How it speeds up and slows down: horizontal — time, vertical — share of the way covered")]
        [ScenarioLabel("Easing")]
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


        public override string Summary => $"{target.Summary} → {(point != null ? point.name : "no point selected")}"
                                          + (duration > 0f ? $" · {duration:0.##} s" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (point == null)
                throw new InvalidOperationException("No point selected");

            yield return Move(target.ResolveRequired(run).transform);
        }

        private IEnumerator Move(Transform moved)
        {
            var startPosition = moved.position;
            var startRotation = moved.rotation;

            for (var elapsed = 0f; elapsed < duration && moved != null && point != null; elapsed += Time.deltaTime)
            {
                var progress = easing.Evaluate(elapsed / duration);

                moved.position = Vector3.LerpUnclamped(startPosition, point.position, progress);

                if (copiesRotation)
                    moved.rotation = Quaternion.SlerpUnclamped(startRotation, point.rotation, progress);

                yield return null;
            }

            if (moved == null || point == null)
                yield break;

            moved.position = point.position;

            if (copiesRotation)
                moved.rotation = point.rotation;
        }
    }
}
