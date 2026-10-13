using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Выбирает лампы — точечные и прожекторы, — которые светят в тумане: ближайшие к камере,
    /// не больше заданного числа. Отвечает ТОЛЬКО за выбор: номер лампы в данных URP, её
    /// яркость и тень в тумане (<see cref="FogLightSettings"/>), где её свет кончается. Сам
    /// свет и тени шейдер берёт у URP по номеру — ровно те же, что получают стены.
    ///
    /// Лимит нужен потому, что каждая лампа стоит на каждом шаге пикселя, чей луч проходит
    /// через её свет, — с тенью это ещё и выборка карты теней.
    /// </summary>
    internal sealed class HeightFogLampsCollector
    {
        /// <summary>
        /// Больше скольких ламп туман не берёт ни при каком качестве. Столько же ждёт шейдер.
        /// </summary>
        public const int MaxLamps = 8;

        private readonly float[] selectedDistances = new float[MaxLamps];


        // ── Выбор ламп ────────────────────────────────────────────────────────

        /// <summary>
        /// Кладёт в массивы ближайшие лампы и возвращает их число. В <paramref name="lamps"/> —
        /// x номер в данных URP, y множитель яркости в тумане, z даёт ли тень (1 или 0),
        /// w насколько лампа ярка в тумане целиком;
        /// в <paramref name="lampSpheres"/> — где лампа и докуда светит. Лампы за кадром
        /// URP уже отбросила сама.
        /// </summary>
        public int CollectNearest(UniversalLightData lightData, Vector3 cameraPosition, int limit,
                                  Vector4[] lamps, Vector4[] lampSpheres)
        {
            var visibleLights = lightData.visibleLights;
            var maxUrpLamps = UniversalRenderPipeline.maxVisibleAdditionalLights;
            var maxCount = Mathf.Min(limit, MaxLamps);
            var count = 0;

            if (maxCount <= 0)
                return 0;

            for (var visibleIndex = 0; visibleIndex < visibleLights.Length; visibleIndex++)
            {
                if (visibleIndex == lightData.mainLightIndex)
                    continue;

                var lamp = visibleLights[visibleIndex];

                if (lamp.lightType != LightType.Point && lamp.lightType != LightType.Spot)
                    continue;

                var urpIndex = ToUrpLampIndex(visibleIndex, lightData.mainLightIndex);

                // Дальше лимита URP лампа в её массивы не попала, и света у неё для шейдера нет.
                if (urpIndex >= maxUrpLamps)
                    continue;

                FogLightSettings.Read(lamp.light, out var intensity, out var castsShadows);

                if (intensity <= 0f)
                    continue;

                var position = (Vector3)lamp.localToWorldMatrix.GetColumn(3);

                // Расстояние до края света лампы, а не до неё самой: большая лампа
                // вдалеке важнее маленькой рядом, если камера стоит в её свете.
                var distance = Mathf.Max(Vector3.Distance(cameraPosition, position) - lamp.range, 0f);

                // Лампе без теней тень в тумане не читаем вовсе — даже пустую выборку.
                var hasShadow = castsShadows && lamp.light != null && lamp.light.shadows != LightShadows.None;

                // Яркость в тумане — по ней шейдер решает, у какой лампы на луче сгущать шаги.
                var brightness = lamp.finalColor.maxColorComponent * intensity;
                var lampData = new Vector4(urpIndex, intensity, hasShadow ? 1f : 0f, brightness);
                var sphere = new Vector4(position.x, position.y, position.z, lamp.range);

                count = InsertByDistance(lamps, lampSpheres, count, maxCount, lampData, sphere, distance);
            }

            return count;
        }

        // Номер лампы в массивах URP. URP складывает туда все видимые источники по порядку,
        // пропуская только солнце, — так же считают и поверхности, и карта теней.
        private static int ToUrpLampIndex(int visibleIndex, int mainLightIndex)
        {
            return mainLightIndex >= 0 && visibleIndex > mainLightIndex ? visibleIndex - 1 : visibleIndex;
        }

        // Вставка в отсортированный по расстоянию список, лишняя дальняя — вылетает.
        private int InsertByDistance(Vector4[] lamps, Vector4[] lampSpheres, int count, int maxCount,
                                     Vector4 lampData, Vector4 sphere, float distance)
        {
            var index = count;

            while (index > 0 && selectedDistances[index - 1] > distance)
                index--;

            if (index >= maxCount)
                return count;

            var last = Mathf.Min(count, maxCount - 1);

            for (var i = last; i > index; i--)
            {
                lamps[i] = lamps[i - 1];
                lampSpheres[i] = lampSpheres[i - 1];
                selectedDistances[i] = selectedDistances[i - 1];
            }

            lamps[index] = lampData;
            lampSpheres[index] = sphere;
            selectedDistances[index] = distance;

            return Mathf.Min(count + 1, maxCount);
        }
    }
}
