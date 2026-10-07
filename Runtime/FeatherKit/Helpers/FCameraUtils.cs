using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Видно ли объект камере. Нужно тем, кто рисует поверх мира: полоска здоровья, метка,
    /// иконка — их незачем считать и показывать для того, кто за кадром.
    ///
    /// Два способа спросить, и они отвечают одно и то же, но стоят по-разному:
    ///
    /// - <see cref="IsOnScreen"/> — по экранным координатам, с запасом в пикселях. Одно
    ///   преобразование, годится на каждый кадр и помногу.
    /// - <see cref="IsInView"/> — по пирамиде видимости, с запасом в метрах. Знает про
    ///   размер объекта и про дальность отсечения, но считает шесть плоскостей на вызов.
    ///
    /// Обе честно отсекают то, что за спиной: точка позади камеры даёт координаты, которые
    /// запросто попадают в кадр, и наивная проверка «в пределах экрана» на этом ломается.
    /// </summary>
    public static class FCameraUtils
    {
        // Один буфер на все вызовы: плоскости считаются каждый раз заново, а новый массив
        // на каждый вызов — мусор в каждом кадре.
        private static readonly Plane[] Planes = new Plane[6];


        /// <summary>
        /// Попадает ли точка на экран, с запасом в пикселях. Дешёвый способ: одно
        /// преобразование матрицей и четыре сравнения.
        ///
        /// Запас нужен почти всегда: у самой границы объект качается на ходу, и без запаса
        /// ответ мигал бы по нескольку раз в секунду.
        ///
        /// Про размер объекта не знает: дерево, торчащее в кадр кроной, но стоящее центром
        /// за краем, посчитается невидимым. Нужен размер — есть <see cref="IsInView"/>.
        /// </summary>
        public static bool IsOnScreen(this Camera camera, Vector3 worldPoint, float marginPixels = 0f)
        {
            if (camera == null)
                return false;

            var screen = camera.WorldToScreenPoint(worldPoint);

            // Точка за спиной даёт координаты, которые запросто попадают в кадр: по x и y
            // «в центре экрана», хотя объект сзади. Глубина — единственное, что их отличает.
            if (screen.z <= 0f)
                return false;

            return screen.x >= -marginPixels && screen.x <= Screen.width + marginPixels
                && screen.y >= -marginPixels && screen.y <= Screen.height + marginPixels;
        }


        /// <summary>
        /// Видна ли точка. <paramref name="radius"/> — запас вокруг неё: объект радиусом
        /// в метр считается видимым, ещё не выехав центром в кадр.
        ///
        /// Отрицательный запас сужает кадр — так спрашивают «уже хорошо видно», а не
        /// «краешек показался».
        /// </summary>
        public static bool IsInView(this Camera camera, Vector3 point, float radius = 0f)
        {
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, Planes);

            for (var i = 0; i < Planes.Length; i++)
            {
                if (Planes[i].GetDistanceToPoint(point) < -radius)
                    return false;
            }

            return true;
        }

        /// <summary>Виден ли объём. Для того, у чего размер важнее точки, — здания, области.</summary>
        public static bool IsInView(this Camera camera, Bounds bounds)
        {
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, Planes);

            return GeometryUtility.TestPlanesAABB(Planes, bounds);
        }

        /// <summary>
        /// Виден ли рисуемый объект. Берёт его настоящие границы, поэтому длинный меч
        /// считается видимым, пока в кадре хотя бы кончик.
        /// </summary>
        public static bool IsInView(this Camera camera, Renderer renderer)
        {
            return renderer != null && camera.IsInView(renderer.bounds);
        }
    }
}
