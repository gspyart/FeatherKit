using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Копия объекта для повторов: экземпляр того же префаба с теми же правками, а без префаба — клон.
    /// - меши, собранные для оригинала (ProBuilder и прочие не из файла), у копии — свои: иначе её сборка писала бы в меш оригинала
    /// - общая для <see cref="ArrayModifierBuilder"/> и повтора Live Builder; флаги своих мешей решает тот, кто копирует
    /// </summary>
    public static class ObjectCopyFactory
    {
        private const string ProBuilderMeshTypeName = "UnityEngine.ProBuilder.ProBuilderMesh";


        // ── Копия ────────────────────────────────────────────────────────

        // Экземпляр префаба оригинала с его правками или клон — в parent (нет — в корень сцены оригинала).
        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            GameObject copy;

            if (PrefabUtility.IsOutermostPrefabInstanceRoot(original))
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(original);
                copy = parent != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : (GameObject)PrefabUtility.InstantiatePrefab(prefab, original.scene);

                PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(original));
            }
            else
            {
                copy = Object.Instantiate(original, parent);

                if (parent == null)
                    SceneManager.MoveGameObjectToScene(copy, original.scene);
            }

            return copy;
        }


        // Свои меши копии вместо мешей оригинала, что собраны не из файла; meshFlags — их флаги: скрытые и несохраняемые или сохраняемые со сценой.
        public static void GiveOwnMeshes(GameObject original, GameObject copy, HideFlags meshFlags)
        {
            DetachSceneMeshes(copy);
            BuildGeneratedMeshes(original, copy, meshFlags);
            CopyMissingMeshes(original, copy, meshFlags);
        }

        // Меш не из файла — собранный для оригинала (ProBuilder): копии нужен свой, иначе сборка копии писала бы в меш оригинала.
        private static void DetachSceneMeshes(GameObject copy)
        {
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && !EditorUtility.IsPersistent(filter.sharedMesh))
                    filter.sharedMesh = null;
        }

        // Меш, которого нет в файле, а его собирает компонент, — ProBuilder: у скрытой копии он сам его не собирает.
        // Просим собрать копии свой меш (MakeUnique): клон помнит меш оригинала, и обычная сборка писала бы в него.
        // ProBuilder зовём по имени: FeatherKit от него не зависит. Собранный меш — копии, живёт и уходит вместе с ней.
        private static void BuildGeneratedMeshes(GameObject original, GameObject copy, HideFlags meshFlags)
        {
            var originalMeshes = MeshesOf(original);

            foreach (var component in copy.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != ProBuilderMeshTypeName)
                    continue;

                InvokeWithDefaults(component, "MakeUnique");

                if (component.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null
                                                                           && !EditorUtility.IsPersistent(filter.sharedMesh) && !originalMeshes.Contains(filter.sharedMesh))
                    filter.sharedMesh.hideFlags = meshFlags;
            }
        }

        private static HashSet<Mesh> MeshesOf(GameObject root)
        {
            var meshes = new HashSet<Mesh>();

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    meshes.Add(filter.sharedMesh);

            return meshes;
        }

        private static void InvokeWithDefaults(Component component, string methodName)
        {
            foreach (var method in component.GetType().GetMethods())
            {
                if (method.Name != methodName)
                    continue;

                var parameters = method.GetParameters();

                if (!System.Array.TrueForAll(parameters, parameter => parameter.HasDefaultValue))
                    continue;

                method.Invoke(component, System.Array.ConvertAll(parameters, parameter => parameter.DefaultValue));
                return;
            }
        }

        // Остальные пустые меши (собирает не ProBuilder) — своя копия меша оригинала. Части совпадают по порядку в иерархии.
        private static void CopyMissingMeshes(GameObject original, GameObject copy, HideFlags meshFlags)
        {
            var originalFilters = original.GetComponentsInChildren<MeshFilter>(true);
            var copyFilters = copy.GetComponentsInChildren<MeshFilter>(true);

            for (var index = 0; index < copyFilters.Length && index < originalFilters.Length; index++)
            {
                var originalMesh = originalFilters[index].sharedMesh;

                if (copyFilters[index].sharedMesh != null || originalMesh == null)
                    continue;

                var ownMesh = Object.Instantiate(originalMesh);
                ownMesh.name = originalMesh.name;
                ownMesh.hideFlags = meshFlags;
                copyFilters[index].sharedMesh = ownMesh;
            }
        }
    }
}
