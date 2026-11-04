using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Хеши для подписей построек и сверок: одинаковые на любом запуске Unity и на любой машине.
    /// System.HashCode каждый запуск свой, а число в строке пишется с запятой или точкой по языку системы — с ними сцена
    /// считалась бы изменённой после каждой компиляции. Здесь — Hash128 по самим байтам чисел.
    /// </summary>
    public static class LiveBuilderStableHash
    {
        // Всё о линии, от чего зависит постройка: замкнута ли, точки с усами и поворотом, вид точки, скругление угла.
        public static void AppendSplines(ref Hash128 hash, SplineContainer container)
        {
            hash.Append(container.Splines.Count);

            foreach (var spline in container.Splines)
                AppendSpline(ref hash, spline);
        }


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


        public static void AppendMatrix(ref Hash128 hash, Matrix4x4 matrix)
        {
            hash.Append(ref matrix);
        }


        // Хеш числом для полей, где ноль значит «ещё не сверяли»: ноль не выдаёт.
        public static int ToNonZeroInt(Hash128 hash)
        {
            var value = System.Convert.ToInt32(hash.ToString().Substring(0, 8), 16);
            return value != 0 ? value : 1;
        }
    }
}
