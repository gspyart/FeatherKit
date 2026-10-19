using FeatherKit.EditorTools.TextureMaterials;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Превью ассета в клетке палитры.
    /// - материал — его основная текстура с оттенком основного цвета; без текстуры — квадрат цвета
    /// - текстура — она сама, своим фильтром: с Point видно те же резкие пиксели, что и в игре
    /// - префаб и модель — <see cref="PrefabPreviewRenderer"/>, остальное — превью Unity; пока готовится — значок типа
    /// </summary>
    public static class PaletteAssetPreviewDrawer
    {
        private static readonly Color EmptyCellColor = new Color(0f, 0f, 0f, 0.2f);


        /// <summary>Нарисовать превью. true — превью Unity ещё готовится, окно надо перерисовать позже.</summary>
        public static bool Draw(Rect rect, Object asset)
        {
            EditorGUI.DrawRect(rect, EmptyCellColor);

            switch (asset)
            {
                case Material material:
                    DrawMaterial(rect, material);
                    return false;

                case Texture2D texture:
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop);
                    return false;

                case GameObject prefab:
                    return DrawPrefab(rect, prefab);

                default:
                    return DrawUnityPreview(rect, asset);
            }
        }

        private static void DrawMaterial(Rect rect, Material material)
        {
            var color = MaterialMainTexture.FindColor(material);
            var texture = MaterialMainTexture.Find(material);

            if (texture == null)
            {
                EditorGUI.DrawRect(rect, color);
                return;
            }

            var previousColor = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 1f);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop);
            GUI.color = previousColor;
        }

        private static bool DrawPrefab(Rect rect, GameObject prefab)
        {
            var isReady = PrefabPreviewRenderer.TryGetPreview(prefab, out var preview);

            if (preview != null)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            else
                DrawIcon(rect, prefab);

            return !isReady;
        }

        private static bool DrawUnityPreview(Rect rect, Object asset)
        {
            var preview = AssetPreview.GetAssetPreview(asset);

            if (preview != null)
            {
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                return false;
            }

            DrawIcon(rect, asset);
            return AssetPreview.IsLoadingAssetPreviews();
        }

        private static void DrawIcon(Rect rect, Object asset)
        {
            var icon = AssetPreview.GetMiniThumbnail(asset);

            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
        }
    }
}
