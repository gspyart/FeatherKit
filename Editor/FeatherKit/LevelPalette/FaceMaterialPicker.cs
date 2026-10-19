using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Пипетка: какой материал на выделенной грани — как «взять текстуру с грани» в Hammer.
    /// Выделены грани ProBuilder — материал первой из них; выделен объект — его первый материал.
    /// </summary>
    public static class FaceMaterialPicker
    {
        /// <summary>Материал выделенной грани или объекта. Ничего не выделено — null.</summary>
        public static Material PickFromSelection()
        {
            var gameObject = Selection.activeGameObject;

            if (gameObject == null)
                return null;

            if (gameObject.TryGetComponent<ProBuilderMesh>(out var mesh) && mesh.selectedFaceCount > 0)
                return MaterialOfFace(mesh, mesh.GetSelectedFaces().First());

            return gameObject.TryGetComponent<Renderer>(out var renderer) ? renderer.sharedMaterial : null;
        }

        private static Material MaterialOfFace(ProBuilderMesh mesh, Face face)
        {
            var materials = mesh.GetComponent<MeshRenderer>().sharedMaterials;

            return materials.Length == 0 ? null : materials[Mathf.Clamp(face.submeshIndex, 0, materials.Length - 1)];
        }
    }
}
