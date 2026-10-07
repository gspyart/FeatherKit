using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Живые миниатюры материалов: перерисовываются, как только материал поправили.
    ///
    /// Своя, а не AssetPreview Unity: та запоминает картинку раз и навсегда, и у сценовой
    /// копии превью так и осталось бы тем, каким было до правки. Когда перерисовывать,
    /// говорит счётчик изменений объекта — он растёт на каждую правку, в том числе через Undo.
    ///
    /// Рисует не больше нескольких штук за кадр: окно со сотней материалов иначе замирало бы
    /// на первом открытии. Остальные дорисуются в следующих кадрах.
    /// </summary>
    public class MaterialPreviewCache
    {
        private const int PreviewSize = 64;
        private const int MaxRendersPerRepaint = 8;

        private readonly Dictionary<Material, MaterialPreviewShot> shots = new Dictionary<Material, MaterialPreviewShot>();

        private UnityEditor.Editor previewEditor;
        private int rendersThisRepaint;

        /// <summary>Кому-то не хватило очереди на отрисовку — окну стоит перерисоваться ещё раз.</summary>
        public bool HasPendingRenders { get; private set; }


        // ── Кадр ─────────────────────────────────────────

        /// <summary>Зовётся окном в начале каждой перерисовки: обнуляет очередь кадра.</summary>
        public void BeginRepaint()
        {
            rendersThisRepaint = 0;
            HasPendingRenders = false;
        }


        public Texture GetPreview(Material material)
        {
            if (material == null)
                return null;

            var dirtyCount = EditorUtility.GetDirtyCount(material);
            var hasShot = shots.TryGetValue(material, out var shot) && shot.Texture != null;

            if (hasShot && shot.DirtyCount == dirtyCount)
                return shot.Texture;

            // Рисуем только на перерисовке: в остальных событиях картинка всё равно не видна.
            if (Event.current.type != EventType.Repaint || rendersThisRepaint >= MaxRendersPerRepaint)
            {
                HasPendingRenders = true;
                return hasShot ? shot.Texture : AssetPreview.GetMiniThumbnail(material);
            }

            if (hasShot)
                Object.DestroyImmediate(shot.Texture);

            rendersThisRepaint++;

            var texture = Render(material);
            shots[material] = new MaterialPreviewShot { Texture = texture, DirtyCount = dirtyCount };

            return texture != null ? texture : AssetPreview.GetMiniThumbnail(material);
        }

        private Texture2D Render(Material material)
        {
            UnityEditor.Editor.CreateCachedEditor(material, null, ref previewEditor);

            var texture = previewEditor.RenderStaticPreview(string.Empty, null, PreviewSize, PreviewSize);

            if (texture != null)
                texture.hideFlags = HideFlags.HideAndDontSave;

            return texture;
        }


        // ── Уборка ───────────────────────────────────────

        /// <summary>Выкидывает картинки материалов, которых больше нет: копию удалили или откатили.</summary>
        public void ForgetDestroyed()
        {
            var destroyed = new List<Material>();

            foreach (var pair in shots)
                if (pair.Key == null)
                    destroyed.Add(pair.Key);

            foreach (var material in destroyed)
            {
                Object.DestroyImmediate(shots[material].Texture);
                shots.Remove(material);
            }
        }


        public void Clear()
        {
            foreach (var shot in shots.Values)
                if (shot.Texture != null)
                    Object.DestroyImmediate(shot.Texture);

            shots.Clear();

            if (previewEditor != null)
                Object.DestroyImmediate(previewEditor);
        }


        /// <summary>Снимок превью и то, с какой по счёту правкой материала он сделан.</summary>
        private struct MaterialPreviewShot
        {
            public Texture2D Texture;
            public int DirtyCount;
        }
    }
}
