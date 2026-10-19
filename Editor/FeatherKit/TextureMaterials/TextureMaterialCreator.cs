using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Выполняет план: создаёт материалы на шейдере из настроек или копией образца и ставит текстуру основной.
    /// Заодно, если включено в настройках, правит импорт текстур: фильтр и наибольший размер.
    ///
    /// Тысяча материалов делается одним заходом: импорт отложен до конца, есть полоса прогресса с отменой.
    /// Созданное сразу записывается в справочник, чтобы следующий план видел его без пересборки.
    /// </summary>
    public static class TextureMaterialCreator
    {
        private const string ProgressTitle = "Материалы из текстур";


        // ── Пачка по плану ───────────────────────────────────────────────

        /// <summary>Создать всё, что план решил создать. Отменили — остановится, созданное останется.</summary>
        public static List<Material> CreateAll(TextureMaterialPlan plan, TextureMaterialSettings settings, TextureMaterialIndex index)
        {
            var created = new List<Material>();

            if (!CanCreate(settings))
                return created;

            var items = plan.Items;

            // Папки — до пакетного режима: внутри него новые папки база ассетов видит не сразу.
            foreach (var item in items)
            {
                if (item.Decision == TextureMaterialDecision.Create)
                    CreateFolders(ParentFolder(item.MaterialPath));
            }

            AssetDatabase.StartAssetEditing();

            try
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, item.TexturePath, (float)i / items.Count))
                        break;

                    if (!item.IsSurfaceTexture)
                        continue;

                    if (settings.ChangesTextureImport)
                        AdjustTextureImport(item.TexturePath, settings);

                    if (item.Decision == TextureMaterialDecision.Create)
                        created.Add(CreateMaterial(item, settings, index));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            return created;
        }


        // ── Одна текстура ────────────────────────────────────────────────

        /// <summary>
        /// Материал текстуры для палитры: есть — найденный, нет — новый.
        /// Имя занято другим материалом — новый получит имя с номером. Служебная карта — null.
        /// </summary>
        public static Material FindOrCreate(string texturePath, string packFolder, TextureMaterialSettings settings,
            TextureMaterialIndex index)
        {
            var item = TextureMaterialPlan.ForTexture(texturePath, packFolder, settings, index);

            switch (item.Decision)
            {
                case TextureMaterialDecision.AlreadyHasMaterial:
                    return AssetDatabase.LoadAssetAtPath<Material>(item.MaterialPath);

                case TextureMaterialDecision.Create:
                    return CreateSingle(item, item.MaterialPath, settings, index);

                case TextureMaterialDecision.NameTaken:
                    return CreateSingle(item, AssetDatabase.GenerateUniqueAssetPath(item.MaterialPath), settings, index);

                default:
                    return null;
            }
        }

        private static Material CreateSingle(TextureMaterialPlanItem item, string materialPath, TextureMaterialSettings settings,
            TextureMaterialIndex index)
        {
            if (!CanCreate(settings))
                return null;

            if (settings.ChangesTextureImport)
                AdjustTextureImport(item.TexturePath, settings);

            var plannedItem = new TextureMaterialPlanItem(item.TexturePath, TextureMaterialDecision.Create, materialPath);

            return CreateMaterial(plannedItem, settings, index);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        // Нет ни образца, ни шейдера — создавать не из чего; говорим один раз, а не на каждую текстуру.
        private static bool CanCreate(TextureMaterialSettings settings)
        {
            if (settings.MaterialShader != null)
                return true;

            Debug.LogError($"Материалы из текстур: не найден шейдер {TextureMaterialSettings.DefaultShaderName} и не задан образец");

            return false;
        }

        private static Material CreateMaterial(TextureMaterialPlanItem item, TextureMaterialSettings settings, TextureMaterialIndex index)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(item.TexturePath);
            var template = settings.TemplateMaterial;
            var material = template != null ? new Material(template) : new Material(settings.MaterialShader);

            MaterialMainTexture.Assign(material, texture);

            CreateFolders(ParentFolder(item.MaterialPath));
            AssetDatabase.CreateAsset(material, item.MaterialPath);
            index.Remember(material, texture);

            return material;
        }

        // AssetDatabase.CreateFolder создаёт по одной папке за раз — идём от корня вглубь.
        private static void CreateFolders(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = ParentFolder(folderPath);

            CreateFolders(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        private static string ParentFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
        }

        // Размер только уменьшаем: маленькую текстуру растягивать незачем.
        private static void AdjustTextureImport(string texturePath, TextureMaterialSettings settings)
        {
            if (!(AssetImporter.GetAtPath(texturePath) is TextureImporter importer))
                return;

            var isChanged = false;

            if (settings.ChangeTextureFilter && importer.filterMode != settings.TextureFilter)
            {
                importer.filterMode = settings.TextureFilter;
                isChanged = true;
            }

            if (settings.LimitTextureSize && importer.maxTextureSize > settings.MaxTextureSize)
            {
                importer.maxTextureSize = settings.MaxTextureSize;
                isChanged = true;
            }

            if (isChanged)
                importer.SaveAndReimport();
        }
    }
}
