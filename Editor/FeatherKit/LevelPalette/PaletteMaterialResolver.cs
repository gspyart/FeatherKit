using FeatherKit.EditorTools.TextureMaterials;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Каким материалом красить ассет из палитры.
    /// - материал — им самим
    /// - текстура — её материалом: есть — найденным, нет — создаётся по настройкам «Материалов из текстур».
    ///   Так с пака на тысячу текстур материалы появляются только у тех, что реально легли на уровень
    /// - остальное красить нечем
    /// Справочник дубликатов собирается при первой нужде и забывается, когда проект поменялся.
    /// </summary>
    public class PaletteMaterialResolver
    {
        private TextureMaterialIndex index;


        private TextureMaterialIndex Index => index ??= new TextureMaterialIndex();


        // ── Материал ассета ──────────────────────────────────────────────

        /// <summary>Материал для покраски. Красить нечем (префаб, служебная карта) — null.</summary>
        public Material Resolve(Object asset, LevelPaletteTab tab)
        {
            switch (asset)
            {
                case Material material:
                    return material;

                case Texture2D texture:
                    var texturePath = AssetDatabase.GetAssetPath(texture);
                    return TextureMaterialCreator.FindOrCreate(texturePath, tab.FindSourceFolderOf(texturePath),
                        TextureMaterialSettings.instance, Index);

                default:
                    return null;
            }
        }


        // ── Устаревание ──────────────────────────────────────────────────

        /// <summary>Проект поменялся — справочник мог устареть, соберётся заново при следующем вопросе.</summary>
        public void Forget()
        {
            index = null;
        }
    }
}
