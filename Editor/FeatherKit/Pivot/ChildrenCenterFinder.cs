using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Находит середину детей объекта — куда ставить пивот в режиме «центр детей».
    /// Габариты считаются в осях самого объекта: у повёрнутой группы «низ» — её низ, а не мира.
    /// Сначала по мешам детей; мешей нет — по коллайдерам; нет и их — по точкам опоры детей.
    /// </summary>
    public static class ChildrenCenterFinder
    {
        public static Vector3 FindPoint(Transform root, ChildrenCenterPoint point)
        {
            var bounds = FindChildrenBoundsInRootAxes(root);
            var localPoint = point == ChildrenCenterPoint.BottomCenter
                ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
                : bounds.center;

            return root.TransformPoint(localPoint);
        }

        private static Bounds FindChildrenBoundsInRootAxes(Transform root)
        {
            var corners = MeshBoundsCorners.CollectInWorld(root);

            if (corners.Count == 0)
                corners = CollectColliderCorners(root);

            if (corners.Count == 0)
                corners = root.Cast<Transform>().Select(child => child.position).ToList();

            // Детей нет вовсе — середина там же, где сам объект.
            if (corners.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var toRoot = root.worldToLocalMatrix;
            var bounds = new Bounds(toRoot.MultiplyPoint3x4(corners[0]), Vector3.zero);

            foreach (var corner in corners)
                bounds.Encapsulate(toRoot.MultiplyPoint3x4(corner));

            return bounds;
        }

        // Рамка коллайдера — по осям мира, поэтому у повёрнутой группы чуть шире настоящей.
        private static List<Vector3> CollectColliderCorners(Transform root)
        {
            var corners = new List<Vector3>();

            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                var bounds = collider.bounds;
                corners.Add(bounds.min);
                corners.Add(bounds.max);
            }

            return corners;
        }
    }
}
