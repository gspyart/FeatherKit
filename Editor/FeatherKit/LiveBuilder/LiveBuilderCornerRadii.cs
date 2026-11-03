using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Скругление углов контура, как в tsitski: радиус у каждой точки стены по линии или здания, м. Хранится в самом сплайне
    /// (его данные по точкам, ключ <see cref="Key"/>): вставил или убрал точку — радиусы остальных съезжают вместе с ними.
    /// Скругляется только угол — точка между двумя отрезками; дугу строит путь стены (<see cref="SplineWallPath"/>), из скольких кусков — у стены или здания.
    /// </summary>
    public static class LiveBuilderCornerRadii
    {
        public const string Key = "CornerRadius";


        // Радиус угла у точки, м; нет — 0.
        public static float RadiusOf(Spline spline, int knot)
        {
            if (!spline.TryGetFloatData(Key, out var data))
                return 0f;

            foreach (var point in data)
                if (Mathf.Abs(point.Index - knot) < 0.01f)
                    return Mathf.Max(0f, point.Value);

            return 0f;
        }


        // Радиус угла у точки; 0 — угол острый, запись убирается. Undo — на стороне вызывающего (контейнер сплайна).
        public static void SetRadius(Spline spline, int knot, float radius)
        {
            var data = spline.GetOrCreateFloatData(Key);
            data.PathIndexUnit = PathIndexUnit.Knot;

            for (var index = data.Count - 1; index >= 0; index--)
                if (Mathf.Abs(data[index].Index - knot) < 0.01f)
                    data.RemoveAt(index);

            if (radius > 0.0001f)
                data.Add(knot, radius);
        }


        // Для подписи постройки: радиусы всех точек одним хешем.
        public static int HashOf(Spline spline)
        {
            if (!spline.TryGetFloatData(Key, out var data))
                return 0;

            var hash = new System.HashCode();

            foreach (var point in data)
            {
                hash.Add(point.Index);
                hash.Add(point.Value);
            }

            return hash.ToHashCode();
        }
    }
}
