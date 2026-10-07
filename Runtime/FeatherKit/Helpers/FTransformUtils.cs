using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Мгновенная перестановка объекта — с оглядкой на физику.
    ///
    /// Если на объекте есть Rigidbody, позой владеет он, а не трансформ: при включённом
    /// сглаживании физика перезапишет правку в том же кадре, и объект останется на месте.
    /// Поэтому тело переставляется через тело, а всё остальное — как обычно.
    ///
    /// Смотрим только сам объект: тело на родителе — это его тело, и двигать надо было бы
    /// родителя целиком.
    ///
    /// Null-трансформ — просто ничего не делает, исключения не кидает.
    /// </summary>
    public static class FTransformUtils
    {
        // ── Перестановка ──────────────────────────────────────────────────

        /// <summary>Перенести объект в точку. Скорость и разворот остаются как были.</summary>
        public static void Teleport(this Transform transform, Vector3 position)
        {
            if (transform == null)
                return;

            if (transform.TryGetComponent<Rigidbody>(out var body))
                Teleport(body, position, body.rotation);
            else
                transform.position = position;
        }

        /// <summary>То же, но сразу с разворотом: физике их дешевле отдать одной правкой.</summary>
        public static void Teleport(this Transform transform, Vector3 position, Quaternion rotation)
        {
            if (transform == null)
                return;

            if (transform.TryGetComponent<Rigidbody>(out var body))
                Teleport(body, position, rotation);
            else
                transform.SetPositionAndRotation(position, rotation);
        }

        // Сглаживание на время прыжка снимаем: оно считает позу по двум прошлым шагам
        // физики и иначе растянет прыжок в плавный полёт через всю карту.
        private static void Teleport(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            var smoothing = body.interpolation;

            body.interpolation = RigidbodyInterpolation.None;

            body.position = position;
            body.rotation = rotation;

            // Трансформ физика подтянула бы только на следующем шаге, а прочитать его
            // могут прямо сейчас — следующей строкой.
            body.PublishTransform();

            body.interpolation = smoothing;
        }


        // ── Одна ось ──────────────────────────────────────────────────────

        public static void SetX(this Transform transform, float x)
        {
            if (transform == null)
                return;

            var position = transform.position;
            position.x = x;

            transform.Teleport(position);
        }

        public static void SetY(this Transform transform, float y)
        {
            if (transform == null)
                return;

            var position = transform.position;
            position.y = y;

            transform.Teleport(position);
        }

        public static void SetZ(this Transform transform, float z)
        {
            if (transform == null)
                return;

            var position = transform.position;
            position.z = z;

            transform.Teleport(position);
        }
    }
}
