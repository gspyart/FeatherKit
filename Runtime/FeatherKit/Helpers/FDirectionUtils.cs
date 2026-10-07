using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Перевод направления из одной системы координат в другую: из локальной в мировую
    /// и обратно, а также из «как видит камера» в мировую.
    ///
    /// Все методы работают с НАПРАВЛЕНИЕМ, а не с точкой: сдвиг объекта в мире на результат
    /// не влияет, поворот — влияет. Для точек это были бы TransformPoint и обратный ему.
    ///
    /// Имя читается как «куда переводим»: ToWorldDirection отдаёт мировое направление,
    /// ToLocalDirection — направление в координатах того, относительно кого спрашивают.
    ///
    /// У Vector2 договор один на весь класс: x — вбок, y — вперёд по плоскости пола.
    /// Так приходит джойстик, и в таком же виде направление ждёт аниматор.
    /// </summary>
    public static class FDirectionUtils
    {
        // ── Плоское в объёмное ────────────────────────────────────────────

        /// <summary>Без смены системы координат: x → X, y → Z, высота нулевая.</summary>
        public static Vector3 ToFlatDirection(this Vector2 input) => new Vector3(input.x, 0f, input.y);


        // ── В мировые ─────────────────────────────────────────────────────

        /// <summary>
        /// Направление задано в координатах <paramref name="space"/> — вернуть мировое.
        /// «Шаг вперёд для персонажа» превращается в «туда-то по миру».
        /// </summary>
        public static Vector3 ToWorldDirection(this Vector2 input, Transform space)
        {
            if (space == null)
                return input.ToFlatDirection();

            return space.TransformDirection(input.ToFlatDirection());
        }

        /// <summary>То же для готового объёмного направления: поворачивается целиком, с высотой.</summary>
        public static Vector3 ToWorldDirection(this Vector3 input, Transform space)
        {
            if (space == null)
                return input;

            return space.TransformDirection(input);
        }


        // ── Из камеры в мировые ───────────────────────────────────────────

        /// <summary>Оси камеры как есть. Камера наклонена — направление уйдёт вверх или вниз.</summary>
        public static Vector3 ToWorldDirectionFromCamera(this Vector2 input, Camera camera)
        {
            if (camera == null)
                return input.ToFlatDirection();

            var view = camera.transform;

            return view.right * input.x + view.forward * input.y;
        }

        public static Vector3 ToWorldDirectionFromCamera(this Vector3 input, Camera camera)
        {
            if (camera == null)
                return input;

            var view = camera.transform;

            return view.right * input.x + view.up * input.y + view.forward * input.z;
        }

        /// <summary>
        /// Оси камеры, положенные на пол: результат всегда параллелен земле и не зависит
        /// от наклона. Обычный выбор для ходьбы — «вверх по экрану» значит «от игрока
        /// вглубь», а не «в небо».
        /// </summary>
        public static Vector3 ToWorldDirectionFromCameraFlat(this Vector2 input, Camera camera)
        {
            if (camera == null)
                return input.ToFlatDirection();

            return Flatten(camera.transform.right) * input.x
                 + Flatten(camera.transform.forward) * input.y;
        }

        /// <summary>То же, но высота идёт напрямую в мировой верх, минуя наклон камеры.</summary>
        public static Vector3 ToWorldDirectionFromCameraFlat(this Vector3 input, Camera camera)
        {
            if (camera == null)
                return input;

            return Flatten(camera.transform.right) * input.x
                 + Vector3.up * input.y
                 + Flatten(camera.transform.forward) * input.z;
        }


        // ── В локальные ───────────────────────────────────────────────────

        /// <summary>
        /// Мировое направление — в координаты <paramref name="space"/>. Обратное к ToWorldDirectionDirection
        /// и ответ на вопрос «а это относительно меня куда»: вперёд, назад или вбок.
        /// </summary>
        public static Vector3 ToLocalDirection(this Vector3 world, Transform space)
        {
            if (space == null)
                return world;

            return space.InverseTransformDirection(world);
        }

        /// <summary>
        /// То же, но плоско и сразу парой: x — вбок, y — вперёд-назад. В таком виде
        /// направление ждут дерево анимаций и всё, что делится на «спереди» и «сбоку».
        /// </summary>
        public static Vector2 ToLocalDirectionFlat(this Vector3 world, Transform space)
        {
            var local = world.ToLocalDirection(space);

            return new Vector2(local.x, local.z);
        }


        // Направление почти вертикально — камера смотрит прямо вниз, и «вперёд» у неё
        // выродилось. Берём условное вперёд, иначе управление станет случайным.
        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }
}
