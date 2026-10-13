using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ставит картам света фильтр из <see cref="LightmapFilterSettings"/>.
    /// Запекание пересоздаёт файлы карт и сбрасывает фильтр на плавный — поэтому, если инструмент
    /// включён, фильтр ставится заново после каждого запекания. Можно применить и вручную.
    /// </summary>
    [InitializeOnLoad]
    public static class LightmapFilterApplier
    {
        static LightmapFilterApplier()
        {
            Lightmapping.bakeCompleted -= ApplyAfterBake;
            Lightmapping.bakeCompleted += ApplyAfterBake;
        }


        private static LightmapFilterSettings Settings => LightmapFilterSettings.instance;


        // ── Применение ───────────────────────────────────────────────────

        /// <summary>Ставит выбранный фильтр картам света открытых сцен. Возвращает, скольким файлам поменял.</summary>
        public static int ApplyToLoadedScenes()
        {
            int changedCount = 0;

            foreach (string path in CollectLightmapPaths())
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                if (importer.filterMode == Settings.FilterMode)
                    continue;

                importer.filterMode = Settings.FilterMode;
                importer.SaveAndReimport();
                changedCount++;
            }

            return changedCount;
        }

        // Пути файлов всех карт: свет, направление света, маска теней.
        private static IEnumerable<string> CollectLightmapPaths()
        {
            var paths = new HashSet<string>();

            foreach (LightmapData lightmap in LightmapSettings.lightmaps)
            {
                AddTexturePath(paths, lightmap.lightmapColor);
                AddTexturePath(paths, lightmap.lightmapDir);
                AddTexturePath(paths, lightmap.shadowMask);
            }

            return paths;
        }

        private static void AddTexturePath(HashSet<string> paths, Texture2D texture)
        {
            if (texture == null)
                return;

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }


        // ── После запекания ──────────────────────────────────────────────

        private static void ApplyAfterBake()
        {
            if (Settings.IsEnabled)
                ApplyToLoadedScenes();
        }
    }
}
