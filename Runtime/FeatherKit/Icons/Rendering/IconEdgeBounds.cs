using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Докуда можно прижимать иконку, ушедшую за край экрана: прямоугольник в пикселях.
    /// - внешняя граница — рамка на холсте: едет вместе с интерфейсом и вырезом телефона;
    /// - видимые элементы у края (миникарта, кнопки) поле обходит, спрятанные — нет;
    /// - рамки нет — весь экран.
    /// </summary>
    [System.Serializable]
    public class IconEdgeBounds
    {
        [Tooltip("Рамка свободного места на холсте: в неё прижимаются иконки у края. " +
                 "Пусто — весь экран")]
        [SerializeField] private RectTransform frame;

        [Tooltip("Элементы интерфейса у края: пока элемент включён и заходит в рамку, поле " +
                 "отступает от него — с той стороны, где теряет меньше места. Выключенный " +
                 "не мешает, и поле растёт обратно")]
        [SerializeField] private List<RectTransform> obstacles = new List<RectTransform>();

        [Tooltip("Отступ внутрь от границы поля, пикселей. Ноль — иконка встанет серединой " +
                 "на саму границу и наполовину вылезет за неё")]
        [SerializeField] private float padding = 64f;

        // Массив под углы просит сама Unity, и заводить его каждый кадр незачем.
        private readonly Vector3[] corners = new Vector3[4];


        // ── Границы ───────────────────────────────────────────────────────

        /// <summary>
        /// Поле в пикселях экрана, уже с отступом внутрь. Камера нужна холсту на камере,
        /// Overlay передаёт null.
        /// </summary>
        public Rect Resolve(Camera uiCamera)
        {
            var area = frame != null
                ? ScreenRectOf(frame, uiCamera)
                : new Rect(0f, 0f, Screen.width, Screen.height);

            for (var i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];

                if (obstacle != null && obstacle.gameObject.activeInHierarchy)
                    area = CutAround(area, ScreenRectOf(obstacle, uiCamera));
            }

            return Shrink(area, padding);
        }

        // Угловая миникарта на высоком экране срезает верх, кнопка посреди бока — только бок.
        // Элемент во всё поле не срезает ничего: иначе иконкам не осталось бы места вовсе.
        private static Rect CutAround(Rect area, Rect obstacle)
        {
            if (!area.Overlaps(obstacle))
                return area;

            var best = area;
            var bestSize = -1f;

            TryCut(Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, obstacle.yMin), ref best, ref bestSize);
            TryCut(Rect.MinMaxRect(area.xMin, obstacle.yMax, area.xMax, area.yMax), ref best, ref bestSize);
            TryCut(Rect.MinMaxRect(obstacle.xMax, area.yMin, area.xMax, area.yMax), ref best, ref bestSize);
            TryCut(Rect.MinMaxRect(area.xMin, area.yMin, obstacle.xMin, area.yMax), ref best, ref bestSize);

            return bestSize > 0f ? best : area;
        }

        private static void TryCut(Rect remaining, ref Rect best, ref float bestSize)
        {
            if (remaining.width <= 0f || remaining.height <= 0f)
                return;

            var size = remaining.width * remaining.height;

            if (size <= bestSize)
                return;

            best = remaining;
            bestSize = size;
        }

        // Отступ не больше половины стороны: узкая рамка иначе вывернулась бы, и все
        // иконки собрались бы в одном углу — на вид ровно поломка.
        private static Rect Shrink(Rect area, float amount)
        {
            var horizontal = Mathf.Min(amount, area.width * 0.5f);
            var vertical = Mathf.Min(amount, area.height * 0.5f);

            return Rect.MinMaxRect(area.xMin + horizontal, area.yMin + vertical,
                                   area.xMax - horizontal, area.yMax - vertical);
        }

        // Через углы, а не через rect: тот живёт в координатах холста, а нужны пиксели.
        // Берём крайние из всех четырёх — повёрнутый элемент иначе вывернулся бы наизнанку.
        private Rect ScreenRectOf(RectTransform rect, Camera uiCamera)
        {
            rect.GetWorldCorners(corners);

            var min = (Vector2)RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            var max = min;

            for (var i = 1; i < corners.Length; i++)
            {
                var point = (Vector2)RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);

                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }


        /// <summary>Вернуть точку экрана внутрь поля.</summary>
        public Vector2 Clamp(Vector2 screen, Camera uiCamera)
        {
            var area = Resolve(uiCamera);

            return new Vector2(Mathf.Clamp(screen.x, area.xMin, area.xMax),
                               Mathf.Clamp(screen.y, area.yMin, area.yMax));
        }
    }
}
