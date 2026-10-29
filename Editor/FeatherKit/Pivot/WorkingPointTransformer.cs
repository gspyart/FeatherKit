using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Двигает, вращает и растягивает выделенные объекты относительно рабочей точки.
    /// Каждое движение ручки записывается в Ctrl+Z; всё перетаскивание отменяется одним шагом.
    /// </summary>
    public static class WorkingPointTransformer
    {
        // Только верхние из выделенных: дети выделенного и так едут вместе с ним.
        private static Transform[] Targets => Selection.transforms;


        // ── Сдвиг ────────────────────────────────────────

        public static void MoveSelection(Vector3 offset)
        {
            var targets = Targets;
            Undo.RecordObjects(targets, "Сдвиг от своей точки");

            foreach (var target in targets)
                target.position += offset;
        }


        // ── Поворот ──────────────────────────────────────

        public static void RotateSelection(Vector3 point, Quaternion turn)
        {
            var targets = Targets;
            Undo.RecordObjects(targets, "Поворот вокруг своей точки");

            foreach (var target in targets)
            {
                target.position = point + turn * (target.position - point);
                target.rotation = turn * target.rotation;
            }
        }


        // ── Масштаб ──────────────────────────────────────

        // factor — во сколько раз растянуть по каждой оси ручки (axes).
        public static void ScaleSelection(Vector3 point, Quaternion axes, Vector3 factor)
        {
            var targets = Targets;
            Undo.RecordObjects(targets, "Масштаб от своей точки");

            foreach (var target in targets)
            {
                var offsetInAxes = Quaternion.Inverse(axes) * (target.position - point);
                target.position = point + axes * Vector3.Scale(offsetInAxes, factor);
                target.localScale = Vector3.Scale(target.localScale, FactorInOwnAxes(target, axes, factor));
            }
        }

        // У объекта свои оси: каждой берём растяжение той оси ручки, что смотрит почти туда же.
        // У повёрнутого наискосок объекта растяжение по одной оси поэтому приблизительное;
        // по всем осям сразу — точное.
        private static Vector3 FactorInOwnAxes(Transform target, Quaternion axes, Vector3 factor)
        {
            var ownFactor = Vector3.one;

            for (int ownAxis = 0; ownAxis < 3; ownAxis++)
            {
                var ownDirection = target.rotation * AxisDirection(ownAxis);
                int closestAxis = 0;
                float closestAlignment = -1f;

                for (int handleAxis = 0; handleAxis < 3; handleAxis++)
                {
                    float alignment = Mathf.Abs(Vector3.Dot(ownDirection, axes * AxisDirection(handleAxis)));

                    if (alignment > closestAlignment)
                    {
                        closestAlignment = alignment;
                        closestAxis = handleAxis;
                    }
                }

                ownFactor[ownAxis] = factor[closestAxis];
            }

            return ownFactor;
        }

        private static Vector3 AxisDirection(int axis) => axis switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };
    }
}
