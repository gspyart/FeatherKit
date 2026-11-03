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


        public static float ToMeters(int millimeters)
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


    /// <summary>
    /// Грани коробки по номерам: 0 — +X, 1 — −X, 2 — +Y, 3 — −Y, 4 — +Z, 5 — −Z.
    /// У каждой грани две оси на её плоскости — по ним же идёт развёртка текстуры: у X-граней (Z, Y), у Y-граней (X, Z), у Z-граней (X, Y).
    /// </summary>
    public static class BoxFaces
    {
        public const int Count = 6;


        public static int Axis(int face)
        {
            return face / 2;
        }


        public static bool IsPositive(int face)
        {
            return face % 2 == 0;
        }


        public static int Index(int axis, bool positive)
        {
            return axis * 2 + (positive ? 0 : 1);
        }


        public static int Opposite(int face)
        {
            return face ^ 1;
        }


        // Первая ось на плоскости грани — горизонталь развёртки.
        public static int SurfaceAxisU(int face)
        {
            return Axis(face) == 0 ? 2 : 0;
        }


        // Вторая ось на плоскости грани — вертикаль развёртки.
        public static int SurfaceAxisV(int face)
        {
            return Axis(face) == 1 ? 2 : 1;
        }


        public static Vector3 Normal(int face)
        {
            var normal = Vector3.zero;
            normal[Axis(face)] = IsPositive(face) ? 1f : -1f;
            return normal;
        }
    }
}
