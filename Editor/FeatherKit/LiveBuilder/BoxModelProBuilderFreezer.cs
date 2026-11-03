#if FEATHERKIT_PROBUILDER
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Заморозить в ProBuilder»: копия объекта из коробок обычным мешем ProBuilder — дальше режешь и тянешь вершины руками.
    /// Коробки на копии больше не работают; сам объект выключается, а не удаляется — к нему можно вернуться.
    /// </summary>
    public static class BoxModelProBuilderFreezer
    {
        public static GameObject Freeze(BoxModel model)
        {
            var source = model.GetComponent<MeshFilter>().sharedMesh;

            if (source == null || source.vertexCount == 0)
                return null;

            var frozenObject = new GameObject(model.name + " (ProBuilder)");
            Undo.RegisterCreatedObjectUndo(frozenObject, "Freeze Box Model");

            var frozenTransform = frozenObject.transform;
            frozenTransform.SetParent(model.transform.parent, false);
            frozenTransform.SetSiblingIndex(model.transform.GetSiblingIndex() + 1);
            frozenTransform.SetPositionAndRotation(model.transform.position, model.transform.rotation);
            frozenTransform.localScale = model.transform.localScale;
            GameObjectUtility.SetStaticEditorFlags(frozenObject, GameObjectUtility.GetStaticEditorFlags(model.gameObject));

            var proBuilderMesh = frozenObject.AddComponent<ProBuilderMesh>();
            var importer = new MeshImporter(source, model.GetComponent<MeshRenderer>().sharedMaterials, proBuilderMesh);
            importer.Import(new MeshImportSettings { quads = true, smoothing = false });
            proBuilderMesh.ToMesh();
            proBuilderMesh.Refresh();

            if (model.HasCollider)
                frozenObject.AddComponent<MeshCollider>().sharedMesh = frozenObject.GetComponent<MeshFilter>().sharedMesh;

            Undo.RecordObject(model.gameObject, "Freeze Box Model");
            model.gameObject.SetActive(false);
            Selection.activeGameObject = frozenObject;
            return frozenObject;
        }
    }
}
#endif
