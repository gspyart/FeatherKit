using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Коробка по осям в целых миллиметрах. Целые числа — чтобы стыки коробок совпадали точно, без щелей от дробей.
    /// </summary>
    public readonly struct MillimeterBox
    {
        public const float MillimetersPerMeter = 1000f;


        public MillimeterBox(Vector3Int min, Vector3Int max)
        {
            Min = Vector3Int.Min(min, max);
            Max = Vector3Int.Max(min, max);
        }


        public Vector3Int Min { get; }
        public Vector3Int Max { get; }

        // Углы в метрах.
        public Vector3 MinInMeters => (Vector3)Min / MillimetersPerMeter;
        public Vector3 MaxInMeters => (Vector3)Max / MillimetersPerMeter;

        public bool IsEmpty => Max.x <= Min.x || Max.y <= Min.y || Max.z <= Min.z;


        // ── Пересчёт из метров ───────────────────────────────────────────

        public static MillimeterBox FromMeters(Vector3 min, Vector3 size)
        {
            return new MillimeterBox(ToMillimeters(min), ToMillimeters(min + size));
        }


        public static Vector3Int ToMillimeters(Vector3 meters)
        {
            return Vector3Int.RoundToInt(meters * MillimetersPerMeter);
        }


        public static float ToMeters(float millimeters)
        {
            return millimeters / MillimetersPerMeter;
        }


        // ── Пересечение ──────────────────────────────────────────────────

        // Пересекаются объёмом; просто касаться гранями — не считается.
        public bool Overlaps(MillimeterBox other)
        {
            return Min.x < other.Max.x && other.Min.x < Max.x
                && Min.y < other.Max.y && other.Min.y < Max.y
                && Min.z < other.Max.z && other.Min.z < Max.z;
        }
    }
}
