using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Создаёт модули на стене с Undo: один экземпляр префаба или ряд — контейнер «‹Префаб› Row» с повтором вдоль стены (<see cref="LiveBuilderArray"/>).
    /// - ряд: Along = Wall, Fill во всю сторону, шаг и отступ — из настроек; в здании с «И этажи выше» — до верха; модуль — вариант A
    /// - габарит модуля в осях префаба — для рамки и места на стене
    /// </summary>
    public static class LiveBuilderModuleFactory
    {
        private const string RowSuffix = " Row";


        // ── Модуль ───────────────────────────────────────────────────────

        public static GameObject CreateModule(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var module = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            module.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(module, "Place Module");
            return module;
        }


        // ── Ряд ──────────────────────────────────────────────────────────

        // Ряд встаёт сразу на первое место стороны: оригинал — первая копия, а не спрятанный в стороне.
        public static GameObject CreateRow(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            var row = new GameObject(prefab.name + RowSuffix);
            row.transform.SetParent(parent, false);
            row.transform.SetPositionAndRotation(position, rotation);
            row.layer = parent != null ? parent.gameObject.layer : 0;

            var module = (GameObject)PrefabUtility.InstantiatePrefab(prefab, row.transform);
            module.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var array = row.AddComponent<LiveBuilderArray>();
            array.Along = LiveBuilderArrayAlong.Wall;
            array.Mode = LiveBuilderArrayMode.Fill;
            array.Length = 0f;
            array.Margin = LiveBuilderModuleSettings.RowMargin;
            array.StepX = LiveBuilderModuleSettings.RowStep;
            array.CountY = 1;
            array.Variants.Add(module.transform);

            var building = SplineBuildingStoreys.BuildingOf(row.transform);

            if (building != null && LiveBuilderBuildingContext.IsOnAllStoreys)
            {
                array.CountYToBuildingTop = true;
                array.StepYIsStoreyHeight = true;
            }

            LiveBuilderArrayWallPath.MoveToFirstPlace(array);
            Undo.RegisterCreatedObjectUndo(row, "Place Module Row");
            return row;
        }


        // ── Габарит ──────────────────────────────────────────────────────

        // Меши и вырезы префаба в осях его корня; пусто — метровый куб над опорой.
        public static Bounds BoundsOf(GameObject prefab)
        {
            var root = prefab.transform;
            var toRoot = root.worldToLocalMatrix;
            var bounds = new Bounds();
            var isEmpty = true;

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    Encapsulate(ref bounds, ref isEmpty, toRoot * filter.transform.localToWorldMatrix, BoxCorners(filter.sharedMesh.bounds));

            foreach (var cutter in prefab.GetComponentsInChildren<GeometryCutter>(true))
                Encapsulate(ref bounds, ref isEmpty, toRoot * GeometryCutterOutline.ShapeToWorld(cutter), GeometryCutterOutline.BoxCorners(cutter));

            return isEmpty ? new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one) : bounds;
        }

        private static Vector3[] BoxCorners(Bounds local)
        {
            var corners = new Vector3[8];

            for (var index = 0; index < 8; index++)
                corners[index] = local.center + Vector3.Scale(local.extents, new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f));

            return corners;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool isEmpty, Matrix4x4 toRoot, Vector3[] corners)
        {
            foreach (var corner in corners)
            {
                var point = toRoot.MultiplyPoint3x4(corner);

                if (isEmpty)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);

                isEmpty = false;
            }
        }
    }
}
