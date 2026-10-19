using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Основная текстура и основной цвет материала (Base Map и Base Color) у любого шейдера.
    /// Свойства ищутся по пометкам шейдера [MainTexture] и [MainColor], а не по имени:
    /// у URP это _BaseMap, у старых шейдеров — _MainTex. Встроенные material.mainTexture и material.color
    /// ругаются в консоль, если такого свойства у шейдера нет, — здесь вместо ругани приходит «нет».
    /// </summary>
    public static class MaterialMainTexture
    {
        private const string LegacyTextureName = "_MainTex";
        private const string LegacyColorName = "_Color";


        // ── Текстура ─────────────────────────────────────────────────────

        public static Texture Find(Material material)
        {
            var propertyId = FindPropertyId(material, ShaderPropertyFlags.MainTexture, LegacyTextureName);

            return propertyId.HasValue ? material.GetTexture(propertyId.Value) : null;
        }


        /// <summary>Поставить основную текстуру. Нет такого свойства у шейдера — false.</summary>
        public static bool Assign(Material material, Texture texture)
        {
            var propertyId = FindPropertyId(material, ShaderPropertyFlags.MainTexture, LegacyTextureName);

            if (!propertyId.HasValue)
                return false;

            material.SetTexture(propertyId.Value, texture);

            return true;
        }


        // ── Цвет ─────────────────────────────────────────────────────────

        /// <summary>Основной цвет материала. Нет такого свойства — белый.</summary>
        public static Color FindColor(Material material)
        {
            var propertyId = FindPropertyId(material, ShaderPropertyFlags.MainColor, LegacyColorName);

            return propertyId.HasValue ? material.GetColor(propertyId.Value) : Color.white;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static int? FindPropertyId(Material material, ShaderPropertyFlags flag, string legacyName)
        {
            var shader = material != null ? material.shader : null;

            if (shader == null)
                return null;

            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                if ((shader.GetPropertyFlags(i) & flag) != 0)
                    return shader.GetPropertyNameId(i);
            }

            return material.HasProperty(legacyName) ? Shader.PropertyToID(legacyName) : (int?)null;
        }
    }
}
