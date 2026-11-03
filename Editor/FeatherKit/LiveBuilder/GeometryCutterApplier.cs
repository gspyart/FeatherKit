using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вычитает вырезы (<see cref="GeometryCutter"/>) из готового меша постройки — объекта из коробок, стены по линии, части здания, —
    /// каждую копию вверх отдельно. Само вычитание — <see cref="MeshSubtraction"/>: только треугольники рядом с вырезом.
    /// Стенки дыры — грани выреза внутри постройки, материал — свой у выреза или самый частый рядом.
    /// </summary>
    public static class GeometryCutterApplier
    {
        // Режет меш всеми вырезами по очереди. materials — материалы частей меша; вырез со своим материалом добавляет часть.
        // Возвращает материалы для рендерера.
        public static Material[] Cut(Mesh mesh, Transform owner, IReadOnlyList<GeometryCutter> cutters, Material[] materials)
        {
            if (cutters.Count == 0 || mesh.vertexCount == 0)
                return materials;

            var materialList = new List<Material>(materials);

            while (materialList.Count < mesh.subMeshCount)
                materialList.Add(materialList.Count > 0 ? materialList[0] : null);

            var polygons = MeshSubtraction.Read(mesh);

            foreach (var cutter in cutters)
                for (var copy = 0; copy < cutter.CopiesUp; copy++)
                {
                    var cutterToMesh = owner.worldToLocalMatrix * GeometryCutterOutline.CopyToWorld(cutter, copy);
                    polygons = MeshSubtraction.SubtractNear(polygons, GeometryCutterOutline.BoundsIn(cutter, cutterToMesh),
                        near => GeometryCutterOutline.PrismPolygons(cutter, cutterToMesh, RevealSubmeshOf(cutter, near, materialList)));
                }

            MeshSubtraction.Write(mesh, polygons, materialList.Count);
            return materialList.ToArray();
        }

        // Свой материал выреза — отдельная часть меша; без него — та часть, которой больше всего рядом: стенки дыры как стена.
        private static int RevealSubmeshOf(GeometryCutter cutter, List<MeshBooleanPolygon> near, List<Material> materials)
        {
            if (cutter.RevealMaterial == null)
                return MeshSubtraction.MostCommonSubmesh(near);

            var index = materials.IndexOf(cutter.RevealMaterial);

            if (index >= 0)
                return index;

            materials.Add(cutter.RevealMaterial);
            return materials.Count - 1;
        }
    }
}
