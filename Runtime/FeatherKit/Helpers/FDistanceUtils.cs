using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Расстояния и направления по плоскости XZ, с поправкой на размер объектов.
    ///
    /// Высоту выбрасываем, потому что в изометрии она врёт: враг на ступеньке не стал дальше.
    /// Радиусы вычитаем, потому что позиции лежат в центрах, а касаются объекты боками —
    /// без этого толстый танк «дотягивается» с той же дистанции, что и тощий бегун.
    ///
    /// Обычные методы, а не расширения: у «расстояния между A и B» нет хозяина, и главное —
    /// радиус обязан стоять вплотную к своей точке. Перепутанные местами радиусы компилятор
    /// не поймает, и ошибка будет тихой.
    ///
    /// У перегрузок с трансформами договор один на все: объекта нет — считаем, что он
    /// бесконечно далеко. Расстояние тогда float.MaxValue, «достаёт ли» — нет, направление —
    /// нулевое. Так проверка по мёртвой цели честно отвечает «не достаёт», а не падает.
    /// </summary>
    public static class FDistanceUtils
    {
        // ── Расстояние ────────────────────────────────────────────────────

        /// <summary>Расстояние между центрами по плоскости.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(FlatSqrDistance(a, b));
        }

        public static float FlatDistance(Transform a, Transform b)
        {
            return a == null || b == null ? float.MaxValue : FlatDistance(a.position, b.position);
        }


        /// <summary>То же без корня — для сравнений и сортировок.</summary>
        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            var x = b.x - a.x;
            var z = b.z - a.z;

            return x * x + z * z;
        }

        public static float FlatSqrDistance(Transform a, Transform b)
        {
            return a == null || b == null ? float.MaxValue : FlatSqrDistance(a.position, b.position);
        }


        // ── Зазор между боками ────────────────────────────────────────────

        /// <summary>
        /// Зазор между боками объектов. Отрицательный — они уже пересеклись,
        /// это и есть глубина проникновения.
        /// </summary>
        public static float FlatGap(Vector3 a, float radiusA, Vector3 b, float radiusB)
        {
            return FlatDistance(a, b) - radiusA - radiusB;
        }

        public static float FlatGap(Transform a, float radiusA, Transform b, float radiusB)
        {
            return a == null || b == null
                ? float.MaxValue
                : FlatGap(a.position, radiusA, b.position, radiusB);
        }


        /// <summary>
        /// Достают ли объекты друг до друга на дистанции <paramref name="range"/>. Корень
        /// не считает — это самая частая проверка в бою, её зовут каждый кадр на каждого.
        /// </summary>
        public static bool IsWithinFlatRange(Vector3 a, float radiusA, Vector3 b, float radiusB, float range)
        {
            var reach = range + radiusA + radiusB;

            if (reach < 0f)
                return false;

            return FlatSqrDistance(a, b) <= reach * reach;
        }

        public static bool IsWithinFlatRange(Transform a, float radiusA, Transform b, float radiusB, float range)
        {
            return a != null && b != null
                   && IsWithinFlatRange(a.position, radiusA, b.position, radiusB, range);
        }


        /// <summary>То же для точек без размера — метка, курсор, центр области.</summary>
        public static bool IsWithinFlatRange(Vector3 a, Vector3 b, float range)
        {
            return IsWithinFlatRange(a, 0f, b, 0f, range);
        }

        public static bool IsWithinFlatRange(Transform a, Transform b, float range)
        {
            return a != null && b != null && IsWithinFlatRange(a.position, b.position, range);
        }


        // ── Направление ───────────────────────────────────────────────────

        /// <summary>Направление по плоскости, нормализованное. Нулевой вектор, если точки совпали.</summary>
        public static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            var direction = new Vector3(to.x - from.x, 0f, to.z - from.z);

            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.zero;
        }

        public static Vector3 FlatDirection(Transform from, Transform to)
        {
            return from == null || to == null
                ? Vector3.zero
                : FlatDirection(from.position, to.position);
        }
    }
}
