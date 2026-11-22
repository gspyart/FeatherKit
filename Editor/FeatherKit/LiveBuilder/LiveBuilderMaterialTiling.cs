using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Строка таблицы плиток в настройках проекта: материал и сколько метров на один повтор его текстуры по U и V.</summary>
    [System.Serializable]
    public struct LiveBuilderMaterialTileSize
    {
        [Tooltip("Материал построек Live Builder.")]
        [SerializeField] private Material material;

        [Tooltip("Метров на один повтор текстуры по U и V. Фасадная панель — 3 × 3: текстура ложится ровно по панели при шаге сетки 3 м.")]
        [SerializeField] private Vector2 metersPerTile;


        public LiveBuilderMaterialTileSize(Material material, Vector2 metersPerTile)
        {
            this.material = material;
            this.metersPerTile = metersPerTile;
        }


        public Material Material => material;
        public Vector2 MetersPerTile => metersPerTile;
    }


    /// <summary>
    /// Сколько метров на одну плитку у материала — по таблице в настройках проекта (<see cref="LiveBuilderProjectSettings"/>); нет в таблице — 1 × 1 м.
    /// - помнит таблицу до её правки: спрашивают на каждую грань каждой сборки
    /// - правка таблицы — постройки сверяются заново, пересобираются только те, где этот материал (подписи)
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderMaterialTiling
    {
        public static readonly Vector2 DefaultMetersPerTile = Vector2.one;

        // Меньше этого — не плитка, а ошибка ввода: считаем 1 м.
        private const float MinMetersPerTile = 0.001f;

        private static Dictionary<Material, Vector2> sizesByMaterial;


        static LiveBuilderMaterialTiling()
        {
            LiveBuilderProjectSettings.Changed += OnSettingsChanged;
        }


        // Метров на плитку по U и V; материала нет в таблице или он пустой — 1 × 1.
        public static Vector2 SizeOf(Material material)
        {
            if (material == null)
                return DefaultMetersPerTile;

            sizesByMaterial ??= ReadTable();
            return sizesByMaterial.TryGetValue(material, out var size) ? size : DefaultMetersPerTile;
        }


        // В таблице есть хоть один материал — иначе плитки в подписях можно не искать.
        public static bool HasAnySize
        {
            get
            {
                sizesByMaterial ??= ReadTable();
                return sizesByMaterial.Count > 0;
            }
        }


        public static bool HasOwnSize(Material material)
        {
            return SizeOf(material) != DefaultMetersPerTile;
        }

        private static Dictionary<Material, Vector2> ReadTable()
        {
            var sizes = new Dictionary<Material, Vector2>();

            foreach (var row in LiveBuilderProjectSettings.instance.MaterialTileSizes)
                if (row.Material != null)
                    sizes[row.Material] = new Vector2(Sanitized(row.MetersPerTile.x), Sanitized(row.MetersPerTile.y));

            return sizes;
        }

        private static float Sanitized(float metersPerTile)
        {
            return Mathf.Abs(metersPerTile) < MinMetersPerTile ? 1f : Mathf.Abs(metersPerTile);
        }


        // Таблицу поправили — забыть её и сверить постройки: подпись у построек с этим материалом другая.
        private static void OnSettingsChanged()
        {
            sizesByMaterial = null;
            BoxModelRebuilder.ForgetCachedResults();
            LiveBuilderRebuildQueue.RequestCheck();
        }
    }
}
