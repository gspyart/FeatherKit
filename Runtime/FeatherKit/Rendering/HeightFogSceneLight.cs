using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Свет сцены, которым освещается туман: общая подсветка и солнце — главный
    /// направленный свет кадра. Отвечает ТОЛЬКО за то, чтобы достать эти числа из сцены;
    /// как туман ими освещается, решает шейдер.
    /// </summary>
    internal static class HeightFogSceneLight
    {
        // Шесть сторон, с которых снимаем общую подсветку.
        private static readonly Vector3[] AmbientDirections =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        private static readonly Color[] AmbientSamples = new Color[6];


        // ── Общая подсветка ───────────────────────────────────────────────────

        // Средняя подсветка со всех сторон. Туман светится сразу во все стороны, поэтому
        // ему нужна не подсветка сверху или сбоку, а среднее по всем. Пробу Unity собирает
        // при любом режиме подсветки — цветом, тремя цветами или от неба, — уже в линейном
        // пространстве и с учётом множителя яркости.
        public static Vector4 CalculateAmbientColor()
        {
            RenderSettings.ambientProbe.Evaluate(AmbientDirections, AmbientSamples);

            var sum = Color.black;

            foreach (var sample in AmbientSamples)
                sum += sample;

            return sum / AmbientSamples.Length;
        }


        // ── Солнце ────────────────────────────────────────────────────────────

        // Цвет солнца вместе с яркостью в тумане и направление НА солнце. Солнца в кадре
        // нет — цвет нулевой, и туман освещается одной общей подсветкой. Можно ли туману
        // тень солнца, решает Fog Light Settings на нём, если есть.
        public static void FindSun(UniversalLightData lightData, out Vector4 color, out Vector4 direction, out bool castsShadows)
        {
            color = Vector4.zero;
            direction = Vector4.zero;
            castsShadows = false;

            var index = lightData.mainLightIndex;

            if (index < 0 || index >= lightData.visibleLights.Length)
                return;

            var sun = lightData.visibleLights[index];

            FogLightSettings.Read(sun.light, out var intensity, out castsShadows);

            // URP отдаёт свет уже с яркостью и в линейном пространстве — так же, как его
            // получают поверхности сцены.
            color = (Vector4)sun.finalColor * intensity;
            direction = -sun.localToWorldMatrix.GetColumn(2);
        }
    }
}
