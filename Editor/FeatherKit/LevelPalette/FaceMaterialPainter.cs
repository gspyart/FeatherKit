using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Кладёт материал на выделенное в сцене — клик по материалу в палитре, как браузер текстур в Hammer.
    /// - на объекте ProBuilder выделены грани — только они
    /// - граней не выделено — объект целиком: у ProBuilder все грани, у остальных все слоты материалов
    /// - ProBuilder в режиме граней, а граней не выделено — ничего: красить весь объект тут было бы неожиданно
    /// Лишние слоты материалов у объекта ProBuilder после покраски убираются: каждый слот — лишний вызов отрисовки.
    /// Всё одним шагом отмены.
    ///
    /// Режим граней ProBuilder 6 — инструмент компонента, и Unity может выключить его, когда фокус уходит в другое окно
    /// (например, палитра во вкладках рядом с инспектором). Поэтому в момент клика по палитре режиму не верим,
    /// а смотрим на сами данные: выделенные грани у объекта остаются, даже если режим уже сброшен.
    /// </summary>
    public static class FaceMaterialPainter
    {
        private const string UndoName = "Покрасить материалом";

        private const SelectMode MeshElementModes = SelectMode.Vertex | SelectMode.Edge | SelectMode.Face
                                                    | SelectMode.TextureVertex | SelectMode.TextureEdge | SelectMode.TextureFace;


        // Режим граней точно включён прямо сейчас. Выключен — это ещё не значит, что граней нет: см. описание класса.
        private static bool IsInElementMode => ProBuilderEditor.instance != null && (ProBuilderEditor.selectMode & MeshElementModes) != 0;


        // ── Что будет покрашено ──────────────────────────────────────────

        /// <summary>Подпись для окна: что покрасит клик прямо сейчас. Нечего — null.</summary>
        public static string DescribeTarget()
        {
            var faceCount = 0;
            var objectCount = 0;

            foreach (var gameObject in Selection.gameObjects)
            {
                switch (TargetOf(gameObject))
                {
                    case PaintTarget.SelectedFaces:
                        faceCount += gameObject.GetComponent<ProBuilderMesh>().selectedFaceCount;
                        break;

                    case PaintTarget.WholeObject:
                        objectCount++;
                        break;
                }
            }

            if (faceCount == 0 && objectCount == 0)
                return null;

            return objectCount == 0 ? $"граней: {faceCount}"
                : faceCount == 0 ? $"объектов целиком: {objectCount}"
                : $"граней: {faceCount} и объектов целиком: {objectCount}";
        }


        // ── Покраска ─────────────────────────────────────────────────────

        /// <summary>Покрасить выделенное. Возвращает, сколько объектов задето: 0 — ничего не выделено.</summary>
        public static int Paint(Material material)
        {
            var paintedCount = 0;

            Undo.SetCurrentGroupName(UndoName);

            foreach (var gameObject in Selection.gameObjects)
            {
                switch (TargetOf(gameObject))
                {
                    case PaintTarget.SelectedFaces:
                        var mesh = gameObject.GetComponent<ProBuilderMesh>();
                        PaintFaces(mesh, mesh.GetSelectedFaces(), material);
                        paintedCount++;
                        break;

                    case PaintTarget.WholeObject:
                        PaintWholeObject(gameObject, material);
                        paintedCount++;
                        break;
                }
            }

            if (paintedCount > 0)
                ProBuilderEditor.Refresh();

            return paintedCount;
        }

        private static void PaintWholeObject(GameObject gameObject, Material material)
        {
            if (gameObject.TryGetComponent<ProBuilderMesh>(out var mesh))
            {
                PaintFaces(mesh, mesh.faces, material);
                return;
            }

            var renderer = gameObject.GetComponent<Renderer>();

            Undo.RecordObject(renderer, UndoName);
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
        }

        // Тот же порядок и та же запись отмены, что в редакторе материалов ProBuilder (MaterialEditor.ApplyMaterial).
        private static void PaintFaces(ProBuilderMesh mesh, IEnumerable<Face> faces, Material material)
        {
            Undo.RecordObjects(new Object[] { mesh.GetComponent<MeshRenderer>(), mesh }, UndoName);

            mesh.SetMaterial(faces, material);
            RemoveUnusedMaterialSlots(mesh);

            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
        }

        // Слот, на который не ссылается ни одна грань, выкидываем, а номера слотов у граней сдвигаем.
        private static void RemoveUnusedMaterialSlots(ProBuilderMesh mesh)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            var materials = renderer.sharedMaterials;
            var isUsed = new bool[materials.Length];

            foreach (var face in mesh.faces)
                isUsed[Mathf.Clamp(face.submeshIndex, 0, materials.Length - 1)] = true;

            if (isUsed.All(used => used))
                return;

            var newIndexes = new int[materials.Length];
            var keptMaterials = new List<Material>();

            for (var i = 0; i < materials.Length; i++)
            {
                newIndexes[i] = keptMaterials.Count;

                if (isUsed[i])
                    keptMaterials.Add(materials[i]);
            }

            foreach (var face in mesh.faces)
                face.submeshIndex = newIndexes[Mathf.Clamp(face.submeshIndex, 0, materials.Length - 1)];

            renderer.sharedMaterials = keptMaterials.ToArray();
        }


        // ── Что красить у объекта ────────────────────────────────────────

        private enum PaintTarget
        {
            Nothing,
            SelectedFaces,
            WholeObject
        }


        private static PaintTarget TargetOf(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<ProBuilderMesh>(out var mesh))
            {
                if (mesh.selectedFaceCount > 0)
                    return PaintTarget.SelectedFaces;

                return IsInElementMode ? PaintTarget.Nothing : PaintTarget.WholeObject;
            }

            return gameObject.TryGetComponent<Renderer>(out _) ? PaintTarget.WholeObject : PaintTarget.Nothing;
        }
    }
}
