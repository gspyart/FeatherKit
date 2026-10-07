using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Где держать иконку. Скрывает разницу между сценой и интерфейсом: точка может ездить
    /// за трансформом, а может быть снята один раз. Наружу всегда отдаёт экранные пиксели,
    /// поэтому работают любые сочетания — объект в мире, место на земле, угол экрана.
    ///
    /// Одна структура с фабричными конструкторами, а не набор классов: способов всего
    /// несколько, и читается это на месте вызова лучше всякой иерархии —
    /// <c>ScreenPoint.Scene(target, up)</c> говорит само за себя.
    ///
    /// Про игру не знает ничего, поэтому живёт в ядре: точка на экране нужна и полоске
    /// здоровья, и цифре урона, и перелёту иконки в слот сумки.
    /// </summary>
    public readonly struct ScreenPoint
    {
        /// <summary>Задан, если точка едет за объектом. Тогда позиция берётся у него каждый кадр.</summary>
        private readonly Transform follow;

        /// <summary>Снятая позиция. Нет её — значит точка держится за трансформ.</summary>
        private readonly Vector3? fixedPosition;

        /// <summary>Координаты уже экранные, переводить их не нужно.</summary>
        private readonly bool isScreenSpace;

        /// <summary>Смещение в мире, м. Им иконку поднимают над головой.</summary>
        private readonly Vector3 offset;


        private ScreenPoint(Transform follow, Vector3? fixedPosition, bool isScreenSpace, Vector3 offset)
        {
            this.follow = follow;
            this.fixedPosition = fixedPosition;
            this.isScreenSpace = isScreenSpace;
            this.offset = offset;
        }


        /// <summary>Трансформ, если точка к нему привязана. Нужен, например, чтобы толкнуть цель.</summary>
        public Transform Transform => follow;

        /// <summary>
        /// Точку ещё можно опрашивать. Ложь, если она должна держаться за трансформ, а его нет —
        /// не передали или уже уничтожили. Снятые позиции живы всегда.
        /// </summary>
        public bool IsValid => fixedPosition.HasValue || follow != null;

        /// <summary>Точка задана прямо в пикселях: камера ей не нужна и за край она не уходит.</summary>
        public bool IsScreenSpace => isScreenSpace;


        // ── Откуда берётся ────────────────────────────────────────────────

        /// <summary>Объект на сцене. Иконка держится за него, пока он жив.</summary>
        public static ScreenPoint Scene(Transform target, Vector3 offset = default)
        {
            return new ScreenPoint(target, null, false, offset);
        }

        /// <summary>
        /// Место на сцене, снятое один раз. Так показывают то, у чего объекта нет вовсе:
        /// точку эвакуации, место высадки, отметку задания.
        /// </summary>
        public static ScreenPoint Scene(Vector3 worldPosition, Vector3 offset = default)
        {
            return new ScreenPoint(null, worldPosition, false, offset);
        }

        /// <summary>Элемент интерфейса. Его позиция уже в экранных пикселях.</summary>
        public static ScreenPoint UI(Transform uiTransform)
        {
            return new ScreenPoint(uiTransform, null, true, Vector3.zero);
        }

        /// <summary>Готовая точка экрана в пикселях.</summary>
        public static ScreenPoint Screen(Vector2 screenPosition)
        {
            return new ScreenPoint(null, screenPosition, true, Vector3.zero);
        }


        // ── Куда это на экране ────────────────────────────────────────────

        /// <summary>
        /// Позиция в экранных пикселях. Точки сцены проецируются камерой, точки интерфейса
        /// отдаются как есть.
        ///
        /// Точка за спиной камеры даёт зеркальные координаты — тут же и разворачиваем,
        /// иначе иконка прыгнула бы в противоположный угол. Такую точку видно по
        /// <paramref name="behind"/>: на экране её быть не должно.
        /// </summary>
        public Vector3 Resolve(Camera camera, out bool behind)
        {
            behind = false;

            if (!IsValid)
                return Vector3.zero;

            var position = (fixedPosition ?? follow.position) + offset;

            if (isScreenSpace)
                return position;

            var screen = RectTransformUtility.WorldToScreenPoint(camera, position);

            // WorldToScreenPoint у RectTransformUtility отдаёт Vector2 — глубину спрашиваем
            // у камеры отдельно, без неё точку за спиной не отличить.
            behind = camera != null && camera.transform.InverseTransformPoint(position).z < 0f;

            if (!behind)
                return screen;

            return new Vector3(UnityEngine.Screen.width - screen.x, UnityEngine.Screen.height - screen.y, 0f);
        }
    }
}
