using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вычитает вырезы (<see cref="GeometryCutter"/>) из готового меша постройки — объекта из коробок, стены по линии, части здания, —
    /// каждую копию (<see cref="GeometryCutterCopy"/>) отдельно. Само вычитание — <see cref="MeshSubtraction"/>: только треугольники рядом с вырезом.
    /// Стенки дыры — грани выреза внутри постройки, материал — своей стороны выреза, общий у выреза или того, что режем
    /// (у перекрытий здания на новом правиле — торцы здания, у остального — самый частый рядом); развёртка стенок — <see cref="LiveBuilderFaceUnwrapper"/>.
    /// </summary>
    public static class GeometryCutterApplier
    {
        // Режет меш всеми вырезами по очереди. materials — материалы частей меша; вырез со своим материалом добавляет часть.
        // Возвращает материалы для рендерера.
        public static Material[] Cut(Mesh mesh, Transform owner, IReadOnlyList<GeometryCutter> cutters, Material[] materials)
        {
            if (cutters.Count == 0 || mesh.vertexCount == 0)
                return materials;

            var materialList = MaterialListOf(mesh, materials);
            var polygons = new MeshSubtractionPolygons(MeshSubtraction.Read(mesh));
            CutPolygons(polygons, owner, cutters, materialList);
            MeshSubtraction.Write(mesh, polygons.InOrder(), materialList.Count);
            return materialList.ToArray();
        }


        // Материал на каждую часть меша: недостающие — как первая.
        public static List<Material> MaterialListOf(Mesh mesh, Material[] materials)
        {
            var materialList = new List<Material>(materials);

            while (materialList.Count < mesh.subMeshCount)
                materialList.Add(materialList.Count > 0 ? materialList[0] : null);

            return materialList;
        }


        // Вычитает вырезы, каждую копию, из уже прочитанных многоугольников меша; материалы стенок дыр дописываются в materials.
        public static void CutPolygons(MeshSubtractionPolygons polygons, Transform owner, IReadOnlyList<GeometryCutter> cutters, List<Material> materials)
        {
            var ownerRevealSubmesh = OwnerRevealSubmeshOf(owner);

            foreach (var cutter in cutters)
                foreach (var copy in GeometryCutterCopy.AllOf(cutter))
                {
                    var cutterToMesh = owner.worldToLocalMatrix * copy.ToWorld;
                    polygons.Unwrapper = LiveBuilderFaceUnwrapper.ForCutter(owner, cutterToMesh, cutter, materials);
                    polygons.SubtractNear(GeometryCutterOutline.BoundsIn(cutter, cutterToMesh),
                        near => PrismPolygons(cutter, cutterToMesh, RevealSubmeshOf(cutter, near, materials, ownerRevealSubmesh), materials));
                }
        }

        // Перекрытия здания на новом правиле: стенки дыры без своего материала — как торцы здания, так выглядит дыра от вырезающей коробки.
        // Иначе своего нет (-1) — самый частый материал рядом.
        private static int OwnerRevealSubmeshOf(Transform owner)
        {
            if (!owner.TryGetComponent<SplineBuildingPart>(out var part) || part.Kind == SplineBuildingPartKind.Walls)
                return -1;

            var model = SplineWallRebuilder.OuterModelOf(part);
            return model != null && !LiveBuilderCutRules.IsLegacy(model) ? SplineBuildingMeshBuilder.OuterWallSubmesh : -1;
        }

        // Общий материал выреза — отдельная часть меша; без него — материал того, что режем.
        private static int RevealSubmeshOf(GeometryCutter cutter, List<MeshBooleanPolygon> near, List<Material> materials, int ownerRevealSubmesh)
        {
            if (cutter.RevealMaterial != null)
                return SubmeshOf(cutter.RevealMaterial, materials);

            return ownerRevealSubmesh >= 0 ? ownerRevealSubmesh : MeshSubtraction.MostCommonSubmesh(near);
        }


        // Тело выреза; сторона со своим материалом (<see cref="GeometryCutterSideMaterials"/>) — в свою часть меша.
        private static List<MeshBooleanPolygon> PrismPolygons(GeometryCutter cutter, Matrix4x4 cutterToMesh, int commonSubmesh, List<Material> materials)
        {
            var prism = GeometryCutterOutline.PrismPolygons(cutter, cutterToMesh, commonSubmesh);

            if (!cutter.SideMaterials.HasAny)
                return prism;

            // Нормаль из осей меша в оси выреза — транспонированной матрицей.
            var meshToCutterNormal = cutterToMesh.transpose;

            for (var index = 0; index < prism.Count; index++)
            {
                var polygon = prism[index];
                var side = SideOf(meshToCutterNormal.MultiplyVector(polygon.Plane.Normal));
                var material = GeometryCutterSolverBoxes.OwnMaterialOf(cutter, side);

                if (material != null)
                    prism[index] = new MeshBooleanPolygon(polygon.Vertices, SubmeshOf(material, materials), polygon.IsFromCutter, polygon.Plane);
            }

            return prism;
        }

        // Сторона выреза (BoxFaces), куда смотрит нормаль его тела: верх тела — потолок дыры, низ — пол.
        private static int SideOf(Vector3 normalInCutter)
        {
            var absolute = new Vector3(Mathf.Abs(normalInCutter.x), Mathf.Abs(normalInCutter.y), Mathf.Abs(normalInCutter.z));
            var axis = absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2;
            return BoxFaces.Index(axis, normalInCutter[axis] > 0f);
        }


        private static int SubmeshOf(Material material, List<Material> materials)
        {
            var index = materials.IndexOf(material);

            if (index >= 0)
                return index;

            materials.Add(material);
            return materials.Count - 1;
        }
    }
}
