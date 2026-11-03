using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Собирает скрытые копии <see cref="ArrayModifier"/> рядом с самим объектом, в том же родителе, и снимает модификатор («Apply», «Remove»).
    /// Копия объекта-экземпляра префаба — экземпляр того же префаба с теми же правками; копия обычного объекта — его клон без модификатора.
    /// Копии скрытые (<see cref="ArrayPreviewCopy"/>): видны в сцене, но их нет в иерархии, в файле сцены и в шаге Ctrl+Z —
    /// это след настроек, его всегда можно собрать заново. Поменялись настройки или объект сдвинули — копии переставляются, сами те же;
    /// объект поправили — копии собираются заново, чтобы повторить правку (<see cref="ArrayModifierWatcher"/>).
    /// Меши, которые не лежат файлом, а собираются компонентом (ProBuilder), у копии — свои: скрытой копии ProBuilder сам их не собирает.
    /// Модификатор из самого префаба копии не достаётся: иначе копии строили бы свои копии без конца.
    /// «Apply» превращает копии в обычные объекты, и это уже один шаг Ctrl+Z.
    /// </summary>
    public static class ArrayModifierBuilder
    {
        // Скрыты в иерархии и инспекторе, не сохраняются ни в сцену, ни в сборку.
        private const HideFlags PreviewFlags = HideFlags.HideAndDontSave;

        private const string ProBuilderMeshTypeName = "UnityEngine.ProBuilder.ProBuilderMesh";


        // Копии появились, переехали или ушли: то, что от них зависит (вырезы копий режут стены), пора сверить.
        public static event Action PreviewCopiesChanged;


        // ── Сборка ───────────────────────────────────────────────────────

        // Собрано по нынешним настройкам и месту объекта: та же подпись, все копии на месте.
        // includesContent — ещё и объект изнутри тот же, что при сборке: тогда и собирать копии заново незачем.
        public static bool IsUpToDate(ArrayModifier array, bool includesContent = false)
        {
            return array.BuiltSignature == SignatureOf(array) && array.PreviewCopies.TrueForAll(copy => copy != null)
                   && (!includesContent || array.BuiltContent == ContentOf(array));
        }


        // Копии — на места по настройкам. recreatesCopies — собрать копии заново: объект поправили, и они должны это повторить.
        public static void Rebuild(ArrayModifier array, bool recreatesCopies = false)
        {
            // Модификатор внутри чужой копии (он в префабе, а копия — экземпляр префаба) не строит своих: иначе копии копий без конца.
            if (IsInsidePreviewCopy(array))
                return;

            var copies = array.PreviewCopies;
            copies.RemoveAll(copy => copy == null);

            if (recreatesCopies || ProblemOf(array) != null)
                RemoveCopies(array, 0);

            if (ProblemOf(array) == null)
            {
                var poses = ArrayModifierPlacement.CopyPosesOf(array, ArrayModifierPlacement.CopySizeOf(array.gameObject));

                for (var index = 0; index < poses.Count; index++)
                {
                    var copy = index < copies.Count ? copies[index] : CreateCopy(array);
                    Place(array, copy, poses[index]);
                }

                RemoveCopies(array, poses.Count);
            }

            array.BuiltSignature = SignatureOf(array);
            array.BuiltContent = ContentOf(array);
            PreviewCopiesChanged?.Invoke();
        }

        // Новая скрытая копия — соседом объекта: экземпляр его префаба с его правками, без префаба — клон без модификатора.
        private static GameObject CreateCopy(ArrayModifier array)
        {
            var original = array.gameObject;
            var parent = original.transform.parent;
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

            // У копии модификатора нет — даже если он в самом префабе.
            foreach (var copyArray in copy.GetComponents<ArrayModifier>())
                Object.DestroyImmediate(copyArray);

            var marker = copy.AddComponent<ArrayPreviewCopy>();
            marker.Owner = array;
            SetFlagsInHierarchy(copy, PreviewFlags);
            DetachSceneMeshes(copy);
            BuildGeneratedMeshes(original, copy);
            CopyMissingMeshes(original, copy);

            array.PreviewCopies.Add(copy);
            return copy;
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
        private static void BuildGeneratedMeshes(GameObject original, GameObject copy)
        {
            var originalMeshes = MeshesOf(original);

            foreach (var component in copy.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != ProBuilderMeshTypeName)
                    continue;

                InvokeWithDefaults(component, "MakeUnique");

                if (component.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null
                                                                           && !EditorUtility.IsPersistent(filter.sharedMesh) && !originalMeshes.Contains(filter.sharedMesh))
                    filter.sharedMesh.hideFlags = PreviewFlags;
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
        private static void CopyMissingMeshes(GameObject original, GameObject copy)
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
                ownMesh.hideFlags = PreviewFlags;
                copyFilters[index].sharedMesh = ownMesh;
            }
        }

        // Место и поворот — от самого объекта, масштаб — его; имя по номеру. Меняется только то, что отличается.
        private static void Place(ArrayModifier array, GameObject copy, ArrayCopyPose pose)
        {
            var original = array.transform;
            var copyTransform = copy.transform;
            ArrayModifierPlacement.WorldPoseOf(original, pose, out var position, out var rotation);

            if (copyTransform.parent != original.parent)
                copyTransform.SetParent(original.parent, true);

            if ((copyTransform.position - position).sqrMagnitude > 1e-10f || Quaternion.Angle(copyTransform.rotation, rotation) > 0.001f
                                                                         || copyTransform.localScale != original.localScale)
            {
                copyTransform.SetPositionAndRotation(position, rotation);
                copyTransform.localScale = original.localScale;
            }

            copy.name = array.gameObject.name + " " + pose.Number;
            copy.GetComponent<ArrayPreviewCopy>().Number = pose.Number;
        }

        // Копии с этого номера и дальше — прочь.
        private static void RemoveCopies(ArrayModifier array, int firstRemoved)
        {
            var copies = array.PreviewCopies;

            for (var index = copies.Count - 1; index >= firstRemoved; index--)
            {
                if (copies[index] != null)
                {
                    DestroyOwnMeshes(copies[index]);
                    Object.DestroyImmediate(copies[index]);
                }

                copies.RemoveAt(index);
            }
        }


        // ── Apply и Remove ───────────────────────────────────────────────

        // Копии становятся обычными объектами сразу за самим объектом в иерархии, модификатор снимается. Один шаг Ctrl+Z.
        public static void Apply(ArrayModifier array)
        {
            Undo.SetCurrentGroupName("Apply Array");

            if (!IsUpToDate(array))
                Rebuild(array);

            var copies = new List<GameObject>(array.PreviewCopies);
            array.PreviewCopies.Clear();
            var siblingIndex = array.transform.GetSiblingIndex();

            foreach (var copy in copies)
            {
                SetFlagsInHierarchy(copy, HideFlags.None);
                KeepOwnMeshes(copy);
                Object.DestroyImmediate(copy.GetComponent<ArrayPreviewCopy>());
                copy.transform.SetSiblingIndex(++siblingIndex);
                Undo.RegisterCreatedObjectUndo(copy, "Apply Array");
            }

            Undo.DestroyObjectImmediate(array);
            PreviewCopiesChanged?.Invoke();
        }


        // Копии — прочь, модификатор — тоже: объект как был. Ctrl+Z возвращает модификатор, копии собираются сами.
        public static void Remove(ArrayModifier array)
        {
            Undo.SetCurrentGroupName("Remove Array");
            RemoveCopies(array, 0);
            Undo.DestroyObjectImmediate(array);
            PreviewCopiesChanged?.Invoke();
        }


        // ── Чужие копии ──────────────────────────────────────────────────

        // Скрытые копии, которые ничей массив не держит: их объект удалили, модификатор сняли, скрипты перезагрузились.
        // Копию, чей массив жив и не помнит её (после перезагрузки скриптов), массив забирает себе по номеру — пересобирать незачем.
        public static void AdoptOrRemoveStrayCopies()
        {
            var isAnyRemoved = false;

            foreach (var marker in Resources.FindObjectsOfTypeAll<ArrayPreviewCopy>())
            {
                if (marker == null || EditorUtility.IsPersistent(marker))
                    continue;

                var owner = marker.Owner;

                if (owner != null && IsInsidePreviewCopy(owner))
                    owner = null;

                if (owner != null && owner.PreviewCopies.Contains(marker.gameObject))
                    continue;

                if (owner != null && !owner.PreviewCopies.Exists(copy => copy != null && copy.name == marker.gameObject.name))
                {
                    owner.PreviewCopies.Add(marker.gameObject);
                    continue;
                }

                Object.DestroyImmediate(marker.gameObject);
                isAnyRemoved = true;
            }

            foreach (var array in Resources.FindObjectsOfTypeAll<ArrayModifier>())
                if (array != null && !EditorUtility.IsPersistent(array))
                    array.PreviewCopies.Sort((first, second) => NumberOf(first).CompareTo(NumberOf(second)));

            if (isAnyRemoved)
                PreviewCopiesChanged?.Invoke();
        }

        private static int NumberOf(GameObject copy)
        {
            return copy != null && copy.TryGetComponent<ArrayPreviewCopy>(out var marker) ? marker.Number : int.MaxValue;
        }


        // ── Проверки ─────────────────────────────────────────────────────

        // Почему копий нет — человеческими словами; всё в порядке — null.
        public static string ProblemOf(ArrayModifier array)
        {
            if (array.transform.parent == null && UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(array.gameObject) != null)
                return "The root of an open prefab has no room for copies next to it. Put the array on an object inside the prefab.";

            var total = ArrayModifierPlacement.TotalCountOf(array);

            if (total > ArrayModifier.MaxCopies)
                return "Too many copies: " + total + ". At most " + ArrayModifier.MaxCopies + " — check the counts.";

            return null;
        }


        // Всё, от чего зависят места копий: настройки, пропуски, где стоит объект и его масштаб, а при шаге «от края» — и его размер.
        private static string SignatureOf(ArrayModifier array)
        {
            var signature = new StringBuilder();
            var culture = CultureInfo.InvariantCulture;
            var original = array.transform;

            signature.Append((int)array.Layout).Append('|').Append(array.Counts).Append('|').Append(array.StepsFromSize ? "e" : "p")
                .Append(array.Step.ToString("F4", culture)).Append(array.Gap.ToString("F4", culture))
                .Append('|').Append(array.CircleCount).Append(',').Append(array.Radius.ToString("F4", culture)).Append(',')
                .Append(array.Arc.ToString("F3", culture)).Append(array.FacesCenter ? "c" : "-")
                .Append('|').Append(string.Join(",", array.SkippedCopies)).Append('|').Append(array.PreviewCopies.Count)
                .Append('|').Append(original.position.ToString("F4", culture)).Append(original.rotation.eulerAngles.ToString("F3", culture))
                .Append(original.localScale.ToString("F4", culture)).Append(original.parent != null ? original.parent.GetEntityId().ToString() : "-")
                .Append('|').Append(array.gameObject.name);

            if (array.StepsFromSize)
                signature.Append('|').Append(ArrayModifierPlacement.CopySizeOf(array.gameObject).ToString("F4", culture));

            return signature.ToString();
        }


        // Объект изнутри одним хешем: имена, включённость, слои и все компоненты его и детей, кроме модификатора и места самого объекта —
        // место в подписи. Копии повторяют именно это.
        private static string ContentOf(ArrayModifier array)
        {
            var content = new Hash128();

            foreach (var node in array.GetComponentsInChildren<Transform>(true))
            {
                content.Append(node.name);
                content.Append(node.gameObject.activeSelf ? 1 : 0);
                content.Append(node.gameObject.layer);

                foreach (var component in node.GetComponents<Component>())
                    if (component != null && component is not ArrayModifier && component != array.transform)
                        content.Append(EditorJsonUtility.ToJson(component));
            }

            return content.ToString();
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Свои меши скрытой копии (CopyMissingMeshes) живут, только пока жива копия.
        private static void DestroyOwnMeshes(GameObject copy)
        {
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == PreviewFlags)
                    Object.DestroyImmediate(filter.sharedMesh);
        }

        // «Apply»: свои меши копии сохраняются в сцену вместе с ней.
        private static void KeepOwnMeshes(GameObject copy)
        {
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.hideFlags == PreviewFlags)
                    filter.sharedMesh.hideFlags = HideFlags.None;
        }

        // Модификатор на скрытой копии другого массива или внутри неё.
        private static bool IsInsidePreviewCopy(ArrayModifier array)
        {
            return array.GetComponentInParent<ArrayPreviewCopy>(true) != null;
        }

        // Флаги — на сам объект и всех его детей: иначе дети скрытой копии сохранились бы в сцену.
        private static void SetFlagsInHierarchy(GameObject root, HideFlags flags)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags = flags;
        }
    }
}
