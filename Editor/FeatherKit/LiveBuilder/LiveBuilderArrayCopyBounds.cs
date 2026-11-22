using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Габарит одной копии повтора (<see cref="LiveBuilderArray"/>): всё под ним — коробки, комнаты, лестницы, колонны, вырезы и меши настоящих частей.
    /// Ширина копии для Fill и для «накрывает ли копия угол стены»; варианты узора считаются вместе — берётся самый широкий.
    /// </summary>
    public static class LiveBuilderArrayCopyBounds
    {
        // Углы всего, что под повтором, в мире. Контейнеры копий и скрытые копии массива FeatherKit не считаются.
        public static List<Vector3> CornersOf(LiveBuilderArray array)
        {
            var corners = new List<Vector3>();
            AddCorners(array.transform, corners);
            return corners;
        }

        private static void AddCorners(Transform node, List<Vector3> corners)
        {
            if (LiveBuilderArrayRealParts.IsServiceObject(node.gameObject) || LiveBuilderArrayRealParts.IsGeneratedContainer(node))
                return;

            AddOwnCorners(node, corners);

            foreach (Transform child in node)
                AddCorners(child, corners);
        }

        // Своя форма узла. Меш постройки (объект из коробок, стена) — уже с копиями, его не берём: его форма — в кусках внутри.
        private static void AddOwnCorners(Transform node, List<Vector3> corners)
        {
            if (node.TryGetComponent<GeometryCutter>(out var cutter))
            {
                AddBoxCorners(GeometryCutterOutline.ShapeToWorld(cutter), GeometryCutterOutline.BoxCorners(cutter), corners);
                return;
            }

            if (node.TryGetComponent<BoxModelPart>(out var part))
                AddSizedBox(node, part.Size, corners);
            else if (node.TryGetComponent<BoxModelRoom>(out var room))
                AddSizedBox(node, room.Size, corners);
            else if (node.TryGetComponent<BoxModelStairs>(out var stairs))
                AddSizedBox(node, stairs.Size, corners);
            else if (node.TryGetComponent<LiveBuilderColumn>(out var column))
                AddSizedBox(node, column.Size, corners);
            else if (LiveBuilderArrayRealParts.IsRealPart(node) || !IsConstruction(node))
                AddMeshCorners(node, corners);
        }

        private static bool IsConstruction(Transform node)
        {
            return node.TryGetComponent<BoxModel>(out _) || node.TryGetComponent<SplineWall>(out _) || node.TryGetComponent<SplineBuilding>(out _)
                   || node.TryGetComponent<SplineBuildingPart>(out _);
        }

        // Кусок с опорой в нижнем углу и размером по своим осям.
        private static void AddSizedBox(Transform node, Vector3 size, List<Vector3> corners)
        {
            var toWorld = Matrix4x4.TRS(node.position, node.rotation, Vector3.one);
            var box = new Vector3[8];

            for (var index = 0; index < 8; index++)
                box[index] = new Vector3((index & 1) == 0 ? 0f : size.x, (index & 2) == 0 ? 0f : size.y, (index & 4) == 0 ? 0f : size.z);

            AddBoxCorners(toWorld, box, corners);
        }

        private static void AddMeshCorners(Transform node, List<Vector3> corners)
        {
            if (!node.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                return;

            var bounds = filter.sharedMesh.bounds;
            var box = new Vector3[8];

            for (var index = 0; index < 8; index++)
                box[index] = bounds.center + Vector3.Scale(bounds.extents, new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f));

            AddBoxCorners(node.localToWorldMatrix, box, corners);
        }

        private static void AddBoxCorners(Matrix4x4 toWorld, Vector3[] box, List<Vector3> corners)
        {
            foreach (var corner in box)
                corners.Add(toWorld.MultiplyPoint3x4(corner));
        }


        // От опоры повтора вдоль направления (в мире): самое далёкое назад и вперёд, м. Пусто — ноль и ноль.
        public static Vector2 ExtentAlong(LiveBuilderArray array, IReadOnlyList<Vector3> corners, Vector3 worldDirection)
        {
            if (corners.Count == 0)
                return Vector2.zero;

            var pivot = array.transform.position;
            var min = float.MaxValue;
            var max = float.MinValue;

            foreach (var corner in corners)
            {
                var along = Vector3.Dot(corner - pivot, worldDirection);
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
            }

            return new Vector2(min, max);
        }
    }
}
