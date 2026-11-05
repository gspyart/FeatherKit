using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что за постройка Live Builder выделена и какого она размера — одно место для рамки с размерами в окне сцены (<see cref="LiveBuilderSelectionBounds"/>)
    /// и строки о выделенном под палитрой окна. Размер — в осях самого объекта: у коробки, комнаты, лестницы и выреза — их собственные числа,
    /// у стены, здания и объекта из коробок — габарит собранного меша вместе со всем, что внутри.
    /// </summary>
    public readonly struct LiveBuilderSelectionInfo
    {
        // Габарит мешей последней спрошенной стены, здания или объекта — до правки сцены: рамку и строку рисуют каждый кадр,
        // а обойти все меши большого здания — дорого.
        private static GameObject cachedMeshesTarget;
        private static int cachedMeshesSceneVersion = -1;
        private static Bounds cachedMeshesBounds;
        private static bool cachedHasMeshesBounds;

        // Что это: «Коробка», «Здание», «Проём».
        public readonly string KindName;

        public readonly GameObject Target;

        // Оси, в которых считан размер: объекта или его объекта из коробок → мир.
        public readonly Matrix4x4 Frame;

        // Габарит в осях Frame.
        public readonly Bounds LocalBounds;

        // У комнаты размер — внутренний, без стен.
        public readonly bool IsInnerSize;


        private LiveBuilderSelectionInfo(string kindName, GameObject target, Matrix4x4 frame, Bounds localBounds, bool isInnerSize = false)
        {
            KindName = kindName;
            Target = target;
            Frame = frame;
            LocalBounds = localBounds;
            IsInnerSize = isInnerSize;
        }


        // Размер в метрах мира: ширина по X, высота по Y, глубина по Z осей объекта.
        public Vector3 Size => Vector3.Scale(LocalBounds.size, Frame.lossyScale);

        // «Ш 4 · Г 3 · В 2,5 м» — ширина, глубина, высота.
        public string SizeText
        {
            get
            {
                var size = Size;
                return "Ш " + LiveBuilderSceneText.FormatMeters(size.x) + " · Г " + LiveBuilderSceneText.FormatMeters(size.z) +
                       " · В " + LiveBuilderSceneText.FormatMeters(size.y) + " м" + (IsInnerSize ? " внутри" : "");
            }
        }


        // ── Что выделено ─────────────────────────────────────────────────

        // Выделенная постройка Live Builder или её часть; часть здания — как само здание. Не постройка или размера нет (пустая группа) — false.
        public static bool TryDescribe(GameObject selected, out LiveBuilderSelectionInfo info)
        {
            info = default;

            if (selected == null)
                return false;

            if (selected.TryGetComponent<BoxModelPart>(out var part))
                return TryDescribeBox(part.Mode == BoxModelPartMode.Add ? "Коробка" : "Вырезающая коробка", part.transform, part.Size, false, out info);

            if (selected.TryGetComponent<BoxModelRoom>(out var room))
                return TryDescribeBox("Комната", room.transform, room.Size, true, out info);

            if (selected.TryGetComponent<BoxModelStairs>(out var stairs))
                return TryDescribeBox("Лестница", stairs.transform, stairs.Size, false, out info);

            if (selected.TryGetComponent<GeometryCutter>(out var cutter))
            {
                var center = cutter.ShapeOffset + Vector3.up * (cutter.Height / 2f);
                info = new LiveBuilderSelectionInfo("Проём", selected, cutter.transform.localToWorldMatrix, new Bounds(center, new Vector3(cutter.Width, cutter.Height, cutter.Depth)));
                return true;
            }

            if (selected.TryGetComponent<SplineBuildingPart>(out var buildingPart) && buildingPart.GetComponentInParent<SplineBuilding>() is { } partBuilding)
                selected = partBuilding.gameObject;

            if (selected.TryGetComponent<SplineBuilding>(out var building))
                return TryDescribeMeshes(LiveBuilderRooms.IsContourRoom(building) ? "Комната" : "Здание, этажей: " + building.StoreyCount, selected, out info);

            if (selected.TryGetComponent<SplineWall>(out var wall))
                return TryDescribeMeshes(LiveBuilderRooms.IsContourRoom(wall) ? "Комната" : "Стена", selected, out info);

            if (selected.TryGetComponent<BoxModel>(out var model))
                return TryDescribeMeshes(model.transform.parent != null && model.transform.parent.GetComponentInParent<BoxModel>() != null ? "Группа" : "Объект из коробок",
                    selected, out info);

            return false;
        }

        // Коробка, комната или лестница: опора — угол с наименьшими координатами в осях её объекта из коробок, размер — свой.
        private static bool TryDescribeBox(string kindName, Transform node, Vector3 size, bool isInnerSize, out LiveBuilderSelectionInfo info)
        {
            info = default;
            var model = node.GetComponentInParent<BoxModel>();

            if (model == null)
                return false;

            var min = BoxModelShapeCollector.LocalMinOf(model, node);
            info = new LiveBuilderSelectionInfo(kindName, node.gameObject, model.transform.localToWorldMatrix, new Bounds(min + size / 2f, size), isInnerSize);
            return true;
        }

        // Стена, здание, объект из коробок: габарит всех собранных мешей — своего и тех, что внутри, — в осях самого объекта.
        private static bool TryDescribeMeshes(string kindName, GameObject target, out LiveBuilderSelectionInfo info)
        {
            info = default;

            if (target != cachedMeshesTarget || BoxModelRebuilder.SceneVersion != cachedMeshesSceneVersion)
            {
                cachedMeshesTarget = target;
                cachedMeshesSceneVersion = BoxModelRebuilder.SceneVersion;
                cachedHasMeshesBounds = TryMeasureMeshes(target, out cachedMeshesBounds);
            }

            if (cachedHasMeshesBounds)
                info = new LiveBuilderSelectionInfo(kindName, target, target.transform.localToWorldMatrix, cachedMeshesBounds);

            return cachedHasMeshesBounds;
        }

        // Габарит всех мешей объекта и того, что внутри, в его осях. Мешей нет — false.
        private static bool TryMeasureMeshes(GameObject target, out Bounds bounds)
        {
            var worldToTarget = target.transform.worldToLocalMatrix;
            bounds = new Bounds();
            var hasBounds = false;

            foreach (var filter in target.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;

                if (mesh == null || mesh.vertexCount == 0)
                    continue;

                var meshToTarget = worldToTarget * filter.transform.localToWorldMatrix;
                var meshBounds = mesh.bounds;

                for (var corner = 0; corner < 8; corner++)
                {
                    var point = meshToTarget.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, CornerSigns(corner)));

                    if (hasBounds)
                        bounds.Encapsulate(point);
                    else
                        bounds = new Bounds(point, Vector3.zero);

                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static Vector3 CornerSigns(int corner)
        {
            return new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
        }
    }
}
