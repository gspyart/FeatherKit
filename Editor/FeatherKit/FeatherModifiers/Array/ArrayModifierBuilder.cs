using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Собирает копии <see cref="ArrayModifier"/> рядом с самим объектом, в том же родителе, и снимает модификатор («Apply», «Remove»).
    /// Копия объекта-экземпляра префаба — экземпляр того же префаба с теми же правками; копия обычного объекта — его клон без модификатора.
    /// Поменялись настройки или объект сдвинули — копии переставляются, сами те же. Объект поправили — копии собираются заново,
    /// чтобы повторить правку (<see cref="ArrayModifierWatcher"/>). Каждая сборка — в том же шаге Ctrl+Z, что и правка, которая её вызвала.
    /// </summary>
    public static class ArrayModifierBuilder
    {
        private const string UndoName = "Rebuild Array";

        // Размер копии без мешей — как у кубика в метр: шаг от края не схлопывается в ноль.
        private static readonly Vector3 FallbackCopySize = Vector3.one;


        // ── Сборка ───────────────────────────────────────────────────────

        // Собрано по нынешним настройкам и месту объекта: та же подпись, все копии на месте.
        public static bool IsUpToDate(ArrayModifier array)
        {
            return array.BuiltSignature == SignatureOf(array) && array.Copies.TrueForAll(copy => copy != null);
        }


        // Копии — на места по настройкам. recreatesCopies — собрать копии заново: объект поправили, и они должны это повторить.
        public static void Rebuild(ArrayModifier array, bool recreatesCopies = false)
        {
            Undo.RecordObject(array, UndoName);
            array.Copies.RemoveAll(copy => copy == null);

            if (recreatesCopies || ProblemOf(array) != null)
                RemoveCopies(array, 0);

            if (ProblemOf(array) == null)
            {
                var poses = ArrayModifierPlacement.CopyPosesOf(array, CopySizeOf(array.gameObject));

                for (var index = 0; index < poses.Count; index++)
                {
                    var copy = index < array.Copies.Count ? array.Copies[index] : CreateCopy(array);
                    Place(array, copy, poses[index], index);
                }

                RemoveCopies(array, poses.Count);
            }

            array.BuiltSignature = SignatureOf(array);
            EditorUtility.SetDirty(array);
        }

        // Новая копия — соседом объекта: экземпляр его префаба с его правками, без префаба — клон без модификатора.
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

            Undo.RegisterCreatedObjectUndo(copy, UndoName);
            array.Copies.Add(copy);
            return copy;
        }

        // Место и поворот — от самого объекта, масштаб — его; имя по номеру; в иерархии — сразу за объектом. Меняется только то, что отличается.
        private static void Place(ArrayModifier array, GameObject copy, ArrayCopyPose pose, int index)
        {
            var original = array.transform;
            var copyTransform = copy.transform;
            var position = original.position + original.rotation * pose.Position;
            var rotation = original.rotation * pose.Rotation;
            var name = array.gameObject.name + " " + pose.Number;

            if (copyTransform.parent != original.parent)
                Undo.SetTransformParent(copyTransform, original.parent, UndoName);

            if ((copyTransform.position - position).sqrMagnitude > 1e-10f || Quaternion.Angle(copyTransform.rotation, rotation) > 0.001f
                                                                         || copyTransform.localScale != original.localScale)
            {
                Undo.RecordObject(copyTransform, UndoName);
                copyTransform.SetPositionAndRotation(position, rotation);
                copyTransform.localScale = original.localScale;
            }

            if (copy.name != name)
            {
                Undo.RecordObject(copy, UndoName);
                copy.name = name;
            }

            var siblingIndex = original.GetSiblingIndex() + 1 + index;

            if (copyTransform.GetSiblingIndex() != siblingIndex)
                copyTransform.SetSiblingIndex(siblingIndex);
        }

        // Копии с этого номера и дальше — прочь.
        private static void RemoveCopies(ArrayModifier array, int firstRemoved)
        {
            for (var index = array.Copies.Count - 1; index >= firstRemoved; index--)
            {
                if (array.Copies[index] != null)
                    Undo.DestroyObjectImmediate(array.Copies[index]);

                array.Copies.RemoveAt(index);
            }
        }


        // ── Apply и Remove ───────────────────────────────────────────────

        // Копии остаются обычными объектами, модификатор снимается. Один шаг Ctrl+Z.
        public static void Apply(ArrayModifier array)
        {
            Undo.SetCurrentGroupName("Apply Array");

            if (!IsUpToDate(array))
                Rebuild(array);

            Undo.DestroyObjectImmediate(array);
        }


        // Копии — прочь, модификатор — тоже: объект как был. Один шаг Ctrl+Z.
        public static void Remove(ArrayModifier array)
        {
            Undo.SetCurrentGroupName("Remove Array");
            Undo.RecordObject(array, UndoName);
            RemoveCopies(array, 0);
            Undo.DestroyObjectImmediate(array);
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


        // Размер объекта в его осях, в метрах: рамка мешей его и его детей. Копии — точно такие же, поэтому шаг «от края» — размер плюс зазор.
        // Объект, чей размер по мешам не виден (проём режет чужую стену), говорит его сам — IArrayCopySize.
        public static Vector3 CopySizeOf(GameObject original)
        {
            if (original.TryGetComponent<IArrayCopySize>(out var ownSize))
                return ownSize.ArrayCopySize;

            var root = original.transform;
            var toOriginal = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
            var bounds = new Bounds();
            var hasBounds = false;

            foreach (var mesh in MeshesOf(original))
            {
                var meshToOriginal = toOriginal * mesh.Owner.localToWorldMatrix;

                for (var corner = 0; corner < 8; corner++)
                {
                    var point = meshToOriginal.MultiplyPoint3x4(mesh.Bounds.center + Vector3.Scale(mesh.Bounds.extents, CornerDirection(corner)));

                    if (hasBounds)
                        bounds.Encapsulate(point);
                    else
                        bounds = new Bounds(point, Vector3.zero);

                    hasBounds = true;
                }
            }

            return hasBounds ? bounds.size : FallbackCopySize;
        }

        private static IEnumerable<(Transform Owner, Bounds Bounds)> MeshesOf(GameObject original)
        {
            foreach (var filter in original.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    yield return (filter.transform, filter.sharedMesh.bounds);

            foreach (var skinned in original.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skinned.sharedMesh != null)
                    yield return (skinned.transform, skinned.sharedMesh.bounds);
        }

        private static Vector3 CornerDirection(int corner) => new(
            (corner & 1) == 0 ? -1f : 1f,
            (corner & 2) == 0 ? -1f : 1f,
            (corner & 4) == 0 ? -1f : 1f);


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
                .Append('|').Append(string.Join(",", array.SkippedCopies)).Append('|').Append(array.Copies.Count)
                .Append('|').Append(original.position.ToString("F4", culture)).Append(original.rotation.eulerAngles.ToString("F3", culture))
                .Append(original.localScale.ToString("F4", culture)).Append(original.parent != null ? original.parent.GetEntityId().ToString() : "-")
                .Append('|').Append(array.gameObject.name);

            if (array.StepsFromSize)
                signature.Append('|').Append(CopySizeOf(array.gameObject).ToString("F4", culture));

            return signature.ToString();
        }
    }
}
