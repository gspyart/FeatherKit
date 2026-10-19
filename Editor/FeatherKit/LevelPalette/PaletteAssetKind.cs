using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>Какие ассеты живут во вкладке палитры.</summary>
    public enum PaletteAssetKind
    {
        Materials,
        Textures,
        Prefabs,
        Any
    }


    /// <summary>Правила вида ассетов: как искать в папке, кого принимать перетаскиванием, как подписать.</summary>
    public static class PaletteAssetKindRules
    {
        public static readonly string[] DisplayNames = { "Материалы", "Текстуры", "Префабы", "Всё подряд" };

        private static readonly string[] IconNames = { "Material Icon", "Texture Icon", "Prefab Icon", "FilterByType" };


        /// <summary>Встроенный значок Unity для вида — его рисуют у имени вкладки.</summary>
        public static Texture ToIcon(this PaletteAssetKind kind)
        {
            return EditorGUIUtility.IconContent(IconNames[(int)kind]).image;
        }


        /// <summary>Фильтр для AssetDatabase.FindAssets.</summary>
        public static string ToSearchFilter(this PaletteAssetKind kind)
        {
            switch (kind)
            {
                case PaletteAssetKind.Materials: return "t:Material";
                case PaletteAssetKind.Textures: return "t:Texture2D";
                case PaletteAssetKind.Prefabs: return "t:Prefab";
                default: return string.Empty;
            }
        }


        /// <summary>
        /// Сам ли файл — ассет этого вида. FindAssets находит и вложенные: материал внутри FBX вернёт GUID модели,
        /// поэтому найденное из папки проверяется ещё и по главному ассету файла.
        /// </summary>
        public static bool IsMainAssetOfKind(this PaletteAssetKind kind, string assetPath)
        {
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

            switch (kind)
            {
                case PaletteAssetKind.Materials: return mainType == typeof(Material);
                case PaletteAssetKind.Textures: return mainType == typeof(Texture2D);
                case PaletteAssetKind.Prefabs: return mainType == typeof(GameObject);
                default: return mainType != null && mainType != typeof(DefaultAsset);
            }
        }


        /// <summary>Подходит ли ассет вкладке. Папки не подходят никогда — они становятся источниками.</summary>
        public static bool Accepts(this PaletteAssetKind kind, Object asset)
        {
            if (asset == null || AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(asset)))
                return false;

            switch (kind)
            {
                case PaletteAssetKind.Materials: return asset is Material;
                case PaletteAssetKind.Textures: return asset is Texture2D;
                case PaletteAssetKind.Prefabs: return asset is GameObject && PrefabUtility.IsPartOfPrefabAsset(asset);
                default: return EditorUtility.IsPersistent(asset);
            }
        }
    }
}
