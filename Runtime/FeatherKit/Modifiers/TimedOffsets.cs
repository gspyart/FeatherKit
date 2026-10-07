using UnityEngine;

namespace FeatherKit.Modifiers
{
    /// <summary>
    /// Сдвиги одной точки или направления: итог — их сумма, база — ноль. Ветер, отдача,
    /// тряска, смещение камеры от нескольких источников разом.
    /// </summary>
    public class TimedOffsets : TimedModifiers<Vector3>
    {
        protected override Vector3 Default => Vector3.zero;

        protected override Vector3 Combine(Vector3 accumulated, Vector3 value) => accumulated + value;
    }
}
