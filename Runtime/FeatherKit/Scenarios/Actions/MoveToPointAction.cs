using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Перенести объект к точке — сразу или плавно за заданное время, по желанию с её поворотом.
    /// Для игрока — «Игрок/Телепорт игрока»: перемещение игрока само обновляет физику и камеру.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Трансформ/Переместить к точке")]
    public class MoveToPointAction : ScenarioAction
    {
        [Tooltip("Что двигать")]
        [ScenarioLabel("Что двигать")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [Tooltip("Куда: позиция и, если отмечено, поворот этой точки")]
        [ScenarioLabel("Точка")]
        [SerializeField] private Transform point;

        [Tooltip("Повернуть объект так же, как повёрнута точка")]
        [ScenarioLabel("С поворотом точки")]
        [SerializeField] private bool copiesRotation = true;

        [Tooltip("За сколько секунд доехать. 0 — сразу")]
        [Min(0f)]
        [ScenarioLabel("Секунд")]
        [SerializeField] private float duration;

        [Tooltip("Как разгоняется и тормозит: по горизонтали — время, по вертикали — пройденная доля пути")]
        [ScenarioLabel("Разгон и торможение")]
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Следующий шаг начнётся, когда объект доедет")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd = true;


        public override string Summary => $"{target.Summary} → {(point != null ? point.name : "точка не выбрана")}"
                                          + (duration > 0f ? $" · {duration:0.##} с" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (point == null)
                throw new InvalidOperationException("Точка не выбрана");

            var moved = target.ResolveRequired(run).transform;

            if (waitsForEnd)
                yield return Move(moved);
            else
                RunAside(Move(moved));
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
