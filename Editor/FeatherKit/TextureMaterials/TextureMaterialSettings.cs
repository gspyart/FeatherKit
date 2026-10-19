using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Настройки создания материалов из текстур: общие на проект, лежат в ProjectSettings и попадают в git.
    /// - куда класть материалы и повторять ли подпапки пака
    /// - шейдер новых материалов и образец, с которого копируются их настройки
    /// - какие текстуры пропускать: карты рельефа, служебные карты по окончанию имени
    /// - как заодно поправить импорт текстур под ретро-вид
    /// Ими пользуются и окно создания, и палитра уровня, когда делает материал на лету.
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/TextureMaterials.asset", FilePathAttribute.Location.ProjectFolder)]
    public class TextureMaterialSettings : ScriptableSingleton<TextureMaterialSettings>
    {
        public const string DefaultShaderName = "Universal Render Pipeline/Lit";

        private const string DefaultOutputFolder = "Assets/Materials";

        private static readonly string[] DefaultSkippedNameEndings =
        {
            "_normal", "_nrm", "_norm", "_n", "_bump",
            "_rough", "_roughness", "_gloss", "_spec", "_specular",
            "_metal", "_metallic", "_ao", "_occlusion",
            "_height", "_disp", "_displacement", "_mask", "_emission", "_emissive"
        };

        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private bool repeatSubfolders = true;
        [SerializeField] private string shaderGuid = string.Empty;
        [SerializeField] private string templateMaterialGuid = string.Empty;
        [SerializeField] private bool skipNormalMaps = true;
        [SerializeField] private List<string> skippedNameEndings = new List<string>(DefaultSkippedNameEndings);
        [SerializeField] private bool changeTextureFilter;
        [SerializeField] private FilterMode textureFilter = FilterMode.Point;
        [SerializeField] private bool limitTextureSize;
        [SerializeField] private int maxTextureSize = 512;


        // ── Куда класть ──────────────────────────────────────────────────

        /// <summary>Корень для новых материалов. Внутри — папка пака или папка текстуры.</summary>
        public string OutputFolder
        {
            get => string.IsNullOrEmpty(outputFolder) ? DefaultOutputFolder : outputFolder;
            set => outputFolder = value;
        }

        /// <summary>Повторять подпапки пака: Pack/Walls/brick.png → Materials/Pack/Walls/brick.mat.</summary>
        public bool RepeatSubfolders
        {
            get => repeatSubfolders;
            set => repeatSubfolders = value;
        }


        // ── Образец ──────────────────────────────────────────────────────

        /// <summary>Шейдер новых материалов, когда образца нет. Не задан — URP Lit.</summary>
        public Shader Shader
        {
            get => string.IsNullOrEmpty(shaderGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(shaderGuid));
            set => shaderGuid = value == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
        }

        /// <summary>Материал, чьи настройки получат новые. Не задан — чистый материал на шейдере.</summary>
        public Material TemplateMaterial
        {
            get => string.IsNullOrEmpty(templateMaterialGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(templateMaterialGuid));
            set => templateMaterialGuid = value == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
        }

        /// <summary>Шейдер, на котором выйдут новые материалы: образца, заданный или URP Lit. По нему же ищутся дубликаты.</summary>
        public Shader MaterialShader
        {
            get
            {
                var template = TemplateMaterial;

                if (template != null)
                    return template.shader;

                var shader = Shader;

                return shader != null ? shader : Shader.Find(DefaultShaderName);
            }
        }


        // ── Что пропускать ───────────────────────────────────────────────

        /// <summary>Пропускать текстуры, импортированные как карта рельефа (Normal map).</summary>
        public bool SkipNormalMaps
        {
            get => skipNormalMaps;
            set => skipNormalMaps = value;
        }

        /// <summary>Окончания имён служебных карт: brick_rough, brick_ao — материалом не становятся.</summary>
        public IReadOnlyList<string> SkippedNameEndings => skippedNameEndings;

        /// <summary>Те же окончания одной строкой через запятую — для поля в окне.</summary>
        public string SkippedNameEndingsText
        {
            get => string.Join(", ", skippedNameEndings);
            set
            {
                skippedNameEndings.Clear();

                foreach (var ending in value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    skippedNameEndings.Add(ending.Trim().ToLowerInvariant());
            }
        }


        // ── Импорт текстур ───────────────────────────────────────────────

        /// <summary>Заодно поставить текстурам фильтр — Point даёт резкие ретро-пиксели.</summary>
        public bool ChangeTextureFilter
        {
            get => changeTextureFilter;
            set => changeTextureFilter = value;
        }

        public FilterMode TextureFilter
        {
            get => textureFilter;
            set => textureFilter = value;
        }

        /// <summary>Заодно ограничить размер текстур при импорте.</summary>
        public bool LimitTextureSize
        {
            get => limitTextureSize;
            set => limitTextureSize = value;
        }

        public int MaxTextureSize
        {
            get => maxTextureSize;
            set => maxTextureSize = value;
        }

        public bool ChangesTextureImport => changeTextureFilter || limitTextureSize;


        // ── Сохранение ───────────────────────────────────────────────────

        public void Save()
        {
            Save(true);
        }


        /// <summary>Вернуть настройки как в новом проекте.</summary>
        public void ResetToDefaults()
        {
            outputFolder = DefaultOutputFolder;
            repeatSubfolders = true;
            shaderGuid = string.Empty;
            templateMaterialGuid = string.Empty;
            skipNormalMaps = true;
            skippedNameEndings = new List<string>(DefaultSkippedNameEndings);
            changeTextureFilter = false;
            textureFilter = FilterMode.Point;
            limitTextureSize = false;
            maxTextureSize = 512;

            Save();
        }


        /// <summary>Подходит ли имя текстуры под служебную карту.</summary>
        public bool HasSkippedNameEnding(string texturePath)
        {
            var name = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();

            foreach (var ending in skippedNameEndings)
            {
                if (name.EndsWith(ending, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
