using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Справочник «текстура → её материал» по всей папке Assets: защита от дубликатов.
    /// Материал текстуры — тот, у кого она стоит основной (Base Map), а не картой рельефа или маской.
    ///
    /// Сам материал при сборке не грузится: читаются только зависимости файлов, это быстро даже на тысячах.
    /// Загружаются лишь те немногие, что ссылаются на спрошенную текстуру, — чтобы проверить, что она у них основная.
    /// Справочник — снимок: проект поменялся — собрать новый или дописать созданное через <see cref="Remember"/>.
    /// </summary>
    public class TextureMaterialIndex
    {
        private const string MaterialSearchFilter = "t:Material";

        private static readonly string[] SearchFolders = { "Assets" };

        // Путь текстуры → пути материалов, которые на неё ссылаются хоть как-то.
        private readonly Dictionary<string, List<string>> materialPathsByTexturePath = new Dictionary<string, List<string>>();


        public TextureMaterialIndex()
        {
            foreach (var guid in AssetDatabase.FindAssets(MaterialSearchFilter, SearchFolders))
            {
                var materialPath = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var dependencyPath in AssetDatabase.GetDependencies(materialPath, false))
                    AddLink(dependencyPath, materialPath);
            }
        }


        // ── Поиск ────────────────────────────────────────────────────────

        /// <summary>
        /// Материал на этом шейдере, у которого эта текстура основная. Нет такого — null.
        /// Шейдер важен: родной материал пака на Standard — не дубликат нашего на шейдере из настроек.
        /// Спрашивает по пути: саму текстуру не грузит — на тысячах текстур это заметная память.
        /// </summary>
        public Material FindMaterial(string texturePath, Shader shader)
        {
            if (!materialPathsByTexturePath.TryGetValue(texturePath, out var materialPaths))
                return null;

            foreach (var materialPath in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                if (material != null && material.shader == shader
                                     && AssetDatabase.GetAssetPath(MaterialMainTexture.Find(material)) == texturePath)
                    return material;
            }

            return null;
        }


        // ── Дополнение ───────────────────────────────────────────────────

        /// <summary>Записать только что созданный материал, чтобы не собирать справочник заново.</summary>
        public void Remember(Material material, Texture texture)
        {
            AddLink(AssetDatabase.GetAssetPath(texture), AssetDatabase.GetAssetPath(material));
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void AddLink(string texturePath, string materialPath)
        {
            if (!materialPathsByTexturePath.TryGetValue(texturePath, out var materialPaths))
            {
                materialPaths = new List<string>();
                materialPathsByTexturePath.Add(texturePath, materialPaths);
            }

            if (!materialPaths.Contains(materialPath))
                materialPaths.Add(materialPath);
        }
    }
}
