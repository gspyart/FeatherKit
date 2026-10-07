using UnityEngine;

namespace FeatherKit.Helpers
{
    // Vector3 — структура, null-проверки тут не нужны.
    public static class FVector3Utils
    {
        public static Vector3 ReplaceX(this Vector3 vector, float x) => new Vector3(x, vector.y, vector.z);
        public static Vector3 ReplaceY(this Vector3 vector, float y) => new Vector3(vector.x, y, vector.z);
        public static Vector3 ReplaceZ(this Vector3 vector, float z) => new Vector3(vector.x, vector.y, z);
    }
}
