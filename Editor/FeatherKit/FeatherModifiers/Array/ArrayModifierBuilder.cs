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
    /// «Apply» превращает копии в обычные объекты, и это уже один шаг Ctrl+Z.
    /// </summary>
    public static class ArrayModifierBuilder
    {
        // Скрыты в иерархии и инспекторе, не сохраняются ни в сцену, ни в сборку.
        private const HideFlags PreviewFlags = HideFlags.HideAndDontSave;


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

                // Модификатор — добавленный поверх префаба компонент, в правки он не входит: у копий его нет.
                PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(original));
            }
            else
            {
                copy = Object.Instantiate(original, parent);

                if (parent == null)
                    SceneManager.MoveGameObjectToScene(copy, original.scene);

                Object.DestroyImmediate(copy.GetComponent<ArrayModifier>());
            }

            var marker = copy.AddComponent<ArrayPreviewCopy>();
            marker.Owner = array;
            SetFlagsInHierarchy(copy, PreviewFlags);

            array.PreviewCopies.Add(copy);
            return copy;
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
                    Object.DestroyImmediate(copies[index]);

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

        // Флаги — на сам объект и всех его детей: иначе дети скрытой копии сохранились бы в сцену.
        private static void SetFlagsInHierarchy(GameObject root, HideFlags flags)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags = flags;
        }
    }
}
