using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Переключатели частей шейдера тумана: клубы, области, свет, лампы, тени. Выключенная
    /// часть вырезана из шейдера целиком, а не пропускается на ходу, — поэтому то, чем не
    /// пользуешься, не стоит ничего. Отвечает ТОЛЬКО за то, чтобы включить нужные части
    /// перед отрисовкой; что именно нужно, решает <see cref="HeightFogShaderInputs"/>.
    ///
    /// Включаются через команду отрисовки, а не прямо в материале: материал один на все
    /// камеры, а части у камер разные — в окне сцены ламп может быть больше, чем в игре.
    ///
    /// Части, которые без другой не нужны, сложены в один переключатель на несколько
    /// положений: мелкие клубы — только с крупными, тени ламп — только с лампами. Так
    /// вариантов шейдера, которые надо собирать, в разы меньше.
    /// </summary>
    internal sealed class HeightFogShaderKeywords
    {
        private readonly LocalKeyword clubs;
        private readonly LocalKeyword clubsWithDetail;
        private readonly LocalKeyword areas;
        private readonly LocalKeyword areasCuttingGlobalFog;
        private readonly LocalKeyword lit;
        private readonly LocalKeyword litWithLamps;
        private readonly LocalKeyword litWithShadowedLamps;
        private readonly LocalKeyword sunShadows;
        private readonly LocalKeyword sunShadowsWithCascades;


        public HeightFogShaderKeywords(Shader shader)
        {
            Shader = shader;

            clubs = new LocalKeyword(shader, "_HEIGHT_FOG_NOISE_VOLUME");
            clubsWithDetail = new LocalKeyword(shader, "_HEIGHT_FOG_NOISE_DETAIL");
            areas = new LocalKeyword(shader, "_HEIGHT_FOG_AREAS");
            areasCuttingGlobalFog = new LocalKeyword(shader, "_HEIGHT_FOG_AREAS_CUT");
            lit = new LocalKeyword(shader, "_HEIGHT_FOG_LIT");
            litWithLamps = new LocalKeyword(shader, "_HEIGHT_FOG_LIT_LAMPS");
            litWithShadowedLamps = new LocalKeyword(shader, "_HEIGHT_FOG_LIT_LAMP_SHADOWS");
            sunShadows = new LocalKeyword(shader, "_HEIGHT_FOG_SUN_SHADOWS");
            sunShadowsWithCascades = new LocalKeyword(shader, "_HEIGHT_FOG_SUN_SHADOWS_CASCADE");
        }


        /// <summary>
        /// Шейдер, для которого собраны переключатели. Сменился шейдер — нужен новый набор.
        /// </summary>
        public Shader Shader { get; }


        // ── Включение ─────────────────────────────────────────────────────────

        public void Apply(RasterCommandBuffer command, Material material, HeightFogShaderInputs inputs)
        {
            command.SetKeyword(material, clubs, inputs.HasClubs && !inputs.HasDetailClubs);
            command.SetKeyword(material, clubsWithDetail, inputs.HasDetailClubs);

            command.SetKeyword(material, areas, inputs.HasAreas && !inputs.CutsGlobalFog);
            command.SetKeyword(material, areasCuttingGlobalFog, inputs.HasAreas && inputs.CutsGlobalFog);

            command.SetKeyword(material, lit, inputs.IsLit && !inputs.HasLamps);
            command.SetKeyword(material, litWithLamps, inputs.HasLamps && !inputs.HasLampShadows);
            command.SetKeyword(material, litWithShadowedLamps, inputs.HasLamps && inputs.HasLampShadows);

            command.SetKeyword(material, sunShadows, inputs.SunShadows == HeightFogSunShadows.Single);
            command.SetKeyword(material, sunShadowsWithCascades, inputs.SunShadows == HeightFogSunShadows.Cascades);
        }
    }
}
