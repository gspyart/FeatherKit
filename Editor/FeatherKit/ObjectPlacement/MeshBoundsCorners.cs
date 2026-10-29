using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Углы рамок мешей объекта и его детей в мировых координатах — чтобы понять,
    /// где у группы низ, середина и края. Частицы, линии и следы не в счёт: их рамка — не форма объекта.
    /// </summary>
    public static class MeshBoundsCorners
    {
        public static List<Vector3> CollectInWorld(Transform root)
        {
            var corners = new List<Vector3>();

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not (MeshRenderer or SkinnedMeshRenderer))
                    continue;

                var bounds = renderer.localBounds;
                var toWorld = renderer.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                    corners.Add(toWorld.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, CornerDirection(corner))));
            }

            return corners;
        }

        // Восемь углов коробки: каждый бит номера — плюс или минус по своей оси.
        private static Vector3 CornerDirection(int corner) => new(
            (corner & 1) == 0 ? -1f : 1f,
            (corner & 2) == 0 ? -1f : 1f,
            (corner & 4) == 0 ? -1f : 1f);
    }
}
