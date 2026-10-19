using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// План создания материалов: по каждой текстуре — создать, пропустить или уже есть, и куда ляжет материал.
    /// Сам ничего не создаёт — только решает, чтобы окно могло показать итог до нажатия «Создать».
    ///
    /// Откуда берутся текстуры:
    /// - выделена папка — все текстуры в ней и подпапках; папка считается паком, его имя станет папкой материалов
    /// - выделена текстура — она сама; паком считается папка, где она лежит
    /// </summary>
    public class TextureMaterialPlan
    {
        private const string TextureSearchFilter = "t:Texture2D";
        private const string MaterialExtension = ".mat";

        private readonly List<TextureMaterialPlanItem> items = new List<TextureMaterialPlanItem>();
        private readonly Dictionary<TextureMaterialDecision, int> countsByDecision = new Dictionary<TextureMaterialDecision, int>();


        private TextureMaterialPlan()
        {
        }


        public IReadOnlyList<TextureMaterialPlanItem> Items => items;

        public int CreateCount => CountOf(TextureMaterialDecision.Create);


        // ── Сборка плана ─────────────────────────────────────────────────

        /// <summary>План по выделенным в Project папкам и текстурам.</summary>
        public static TextureMaterialPlan ForAssetPaths(IEnumerable<string> assetPaths, TextureMaterialSettings settings,
            TextureMaterialIndex index)
        {
            var plan = new TextureMaterialPlan();
            var seenTexturePaths = new HashSet<string>();
            var plannedMaterialPaths = new HashSet<string>();

            foreach (var texture in CollectTextures(assetPaths))
            {
                if (seenTexturePaths.Add(texture.TexturePath))
                    plan.Add(Decide(texture, settings, index, plannedMaterialPaths));
            }

            return plan;
        }

        private static IEnumerable<TextureInPackFolder> CollectTextures(IEnumerable<string> assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    foreach (var guid in AssetDatabase.FindAssets(TextureSearchFilter, new[] { assetPath }))
                        yield return new TextureInPackFolder(AssetDatabase.GUIDToAssetPath(guid), assetPath);
                }
                else if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(Texture2D))
                {
                    yield return new TextureInPackFolder(assetPath, ParentFolder(assetPath));
                }
            }
        }


        /// <summary>
        /// План на одну текстуру — для палитры, когда материал нужен прямо сейчас.
        /// Пак — папка, относительно которой повторяются подпапки; не задан — папка самой текстуры.
        /// </summary>
        public static TextureMaterialPlanItem ForTexture(string texturePath, string packFolder, TextureMaterialSettings settings,
            TextureMaterialIndex index)
        {
            var texture = new TextureInPackFolder(texturePath, string.IsNullOrEmpty(packFolder) ? ParentFolder(texturePath) : packFolder);

            return Decide(texture, settings, index, new HashSet<string>());
        }


        // Порядок проверок важен: служебная карта не должна попасть в «имя занято».
        private static TextureMaterialPlanItem Decide(TextureInPackFolder texture, TextureMaterialSettings settings,
            TextureMaterialIndex index, HashSet<string> plannedMaterialPaths)
        {
            var path = texture.TexturePath;

            if (settings.SkipNormalMaps && IsImportedAsNormalMap(path))
                return new TextureMaterialPlanItem(path, TextureMaterialDecision.NormalMap, null);

            if (settings.HasSkippedNameEnding(path))
                return new TextureMaterialPlanItem(path, TextureMaterialDecision.ServiceMapByName, null);

            var existing = index.FindMaterial(path, settings.MaterialShader);

            if (existing != null)
                return new TextureMaterialPlanItem(path, TextureMaterialDecision.AlreadyHasMaterial, AssetDatabase.GetAssetPath(existing));

            var materialPath = MaterialPathFor(texture, settings);

            if (!plannedMaterialPaths.Add(materialPath) || AssetDatabase.LoadMainAssetAtPath(materialPath) != null)
                return new TextureMaterialPlanItem(path, TextureMaterialDecision.NameTaken, materialPath);

            return new TextureMaterialPlanItem(path, TextureMaterialDecision.Create, materialPath);
        }

        private static bool IsImportedAsNormalMap(string texturePath)
        {
            return AssetImporter.GetAtPath(texturePath) is TextureImporter importer
                   && importer.textureType == TextureImporterType.NormalMap;
        }

        // Pack/Walls/brick.png → <корень>/Pack/Walls/brick.mat; без подпапок — <корень>/Pack/brick.mat.
        private static string MaterialPathFor(TextureInPackFolder texture, TextureMaterialSettings settings)
        {
            var packName = Path.GetFileName(texture.PackFolder);
            var folder = settings.OutputFolder + "/" + packName;

            if (settings.RepeatSubfolders)
            {
                var textureFolder = ParentFolder(texture.TexturePath);

                if (textureFolder.Length > texture.PackFolder.Length)
                    folder += textureFolder.Substring(texture.PackFolder.Length);
            }

            return folder + "/" + Path.GetFileNameWithoutExtension(texture.TexturePath) + MaterialExtension;
        }


        // ── Итоги ────────────────────────────────────────────────────────

        public int CountOf(TextureMaterialDecision decision)
        {
            return countsByDecision.TryGetValue(decision, out var count) ? count : 0;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void Add(TextureMaterialPlanItem item)
        {
            items.Add(item);
            countsByDecision[item.Decision] = CountOf(item.Decision) + 1;
        }

        private static string ParentFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
        }


        // ── Текстура и её пак ────────────────────────────────────────────

        private readonly struct TextureInPackFolder
        {
            public TextureInPackFolder(string texturePath, string packFolder)
            {
                TexturePath = texturePath;
                PackFolder = packFolder;
            }


            public string TexturePath { get; }

            public string PackFolder { get; }
        }
    }
}
