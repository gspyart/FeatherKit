using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
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
