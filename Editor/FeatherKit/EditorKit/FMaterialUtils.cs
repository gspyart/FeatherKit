// ═══════════════════════════════════════════════════════════════════════════════════════
// ПАМЯТКА
//
// Здесь лежит то, что нужно КАЖДОМУ инспектору материала FeatherKit, а не одному.
// Появился второй тун-шейдер — и вместе с ним второй ShaderGUI; своя копия Sanitize
// в каждом однажды разошлась бы с соседней, и разошлась бы молча.
// ═══════════════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Приведение материала в рабочее состояние после смены шейдера: общее для всех
    /// инспекторов материалов кита.
    /// </summary>
    public static class FMaterialUtils
    {
        /// <summary>
        /// Включает все проходы нового шейдера и снимает ключевые слова, которых он не знает.
        ///
        /// Нужно потому, что SetShaderPassEnabled живёт В МАТЕРИАЛЕ и смену шейдера
        /// переживает: материал, пришедший от чужого тун-шейдера, приезжает с выключенным
        /// проходом обводки. В инспекторе галка стоит, контура нет, и найти это тяжело.
        /// </summary>
        public static void Sanitize(Material material, Shader shader)
        {
            var lightMode = new UnityEngine.Rendering.ShaderTagId("LightMode");

            for (var i = 0; i < shader.passCount; i++)
            {
                var mode = shader.FindPassTagValue(i, lightMode).name;

                if (!string.IsNullOrEmpty(mode))
                    material.SetShaderPassEnabled(mode, true);
            }

            var known = new HashSet<string>(shader.keywordSpace.keywordNames);
            var current = material.shaderKeywords;

            for (var i = 0; i < current.Length; i++)
            {
                if (!known.Contains(current[i]))
                    material.DisableKeyword(current[i]);
            }
        }
    }
}
