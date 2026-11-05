using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Хеши для подписей построек и сверок: одинаковые на любом запуске Unity и на любой машине.
    /// System.HashCode каждый запуск свой, а число в строке пишется с запятой или точкой по языку системы — с ними сцена
    /// считалась бы изменённой после каждой компиляции. Здесь — Hash128 по самим байтам чисел.
    /// Числа, которые получаются расчётом (взаимное место двух объектов, размеры выреза), — с округлением до 0,1 мм:
    /// у произведения матриц последние знаки дрожат, когда двигают общего родителя, и подпись менялась бы на каждом шаге.
    /// Взаимное место считается через общего предка, по местным числам объектов: двигают общего родителя — оно то же до бита.
    /// </summary>
    public static class LiveBuilderStableHash
    {
        // Шаг округления: 0,1 мм у сдвига, у поворота — 0,1 мм на метр.
        private const double QuantizationStep = 1e-4;


        // ── Линия ────────────────────────────────────────────────────────

        // Всё о линии, от чего зависит постройка: замкнута ли, точки с усами и поворотом, вид точки, скругление угла.
        public static void AppendSplines(ref Hash128 hash, SplineContainer container)
        {
            hash.Append(container.Splines.Count);

            foreach (var spline in container.Splines)
                AppendSpline(ref hash, spline);
        }


        // Одна линия: замкнута ли и все её точки.
        public static void AppendSpline(ref Hash128 hash, Spline spline)
        {
            hash.Append(spline.Closed ? 1 : 0);
            hash.Append(spline.Count);

            for (var index = 0; index < spline.Count; index++)
            {
                var knot = spline[index];
                hash.Append(ref knot.Position);
                hash.Append(ref knot.TangentIn);
                hash.Append(ref knot.TangentOut);
                hash.Append(ref knot.Rotation);
                hash.Append((int)spline.GetTangentMode(index));
                hash.Append(LiveBuilderCornerRadii.RadiusOf(spline, index));
            }
        }


        // ── Взаимное место ───────────────────────────────────────────────

        // Где target в осях space — с округлением. Считается через их общего предка по местным числам объектов, а не через мир:
        // далеко от начала координат у мировых чисел дрожат уже десятые доли миллиметра, а общего родителя двигают целиком.
        public static void AppendQuantizedPlacement(ref Hash128 hash, Transform space, Transform target)
        {
            AppendQuantizedMatrix(ref hash, RelativeMatrix(space, target));
        }


        // Матрица из осей target в оси space. Общего предка нет (разные корни сцены) — через мир.
        public static Matrix4x4 RelativeMatrix(Transform space, Transform target)
        {
            var ancestor = CommonAncestorOf(space, target);

            if (ancestor == null)
                return space.worldToLocalMatrix * target.localToWorldMatrix;

            return MatrixToAncestor(space, ancestor).inverse * MatrixToAncestor(target, ancestor);
        }

        // Ближайший объект, внутри которого лежат оба (или один из них сам); нет такого — null.
        private static Transform CommonAncestorOf(Transform first, Transform second)
        {
            for (var candidate = first; candidate != null; candidate = candidate.parent)
                if (second.IsChildOf(candidate))
                    return candidate;

            return null;
        }

        // Из осей node в оси предка — по местным числам каждого объекта между ними.
        private static Matrix4x4 MatrixToAncestor(Transform node, Transform ancestor)
        {
            var matrix = Matrix4x4.identity;

            for (var current = node; current != ancestor; current = current.parent)
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;

            return matrix;
        }


        // ── Числа из расчёта ─────────────────────────────────────────────

        // Матрица взаимного места — каждое число с округлением.
        public static void AppendQuantizedMatrix(ref Hash128 hash, Matrix4x4 matrix)
        {
            for (var index = 0; index < 16; index++)
                AppendQuantized(ref hash, matrix[index]);
        }


        // Число с округлением до 0,1 мм: дрожь последних знаков подпись не меняет.
        public static void AppendQuantized(ref Hash128 hash, float value)
        {
            var steps = (long)System.Math.Round(value / QuantizationStep);
            hash.Append(ref steps);
        }


        // ── Число вместо хеша ────────────────────────────────────────────

        // Хеш числом для полей, где ноль значит «ещё не сверяли»: ноль не выдаёт.
        public static int ToNonZeroInt(Hash128 hash)
        {
            var value = System.Convert.ToInt32(hash.ToString().Substring(0, 8), 16);
            return value != 0 ? value : 1;
        }
    }
}
