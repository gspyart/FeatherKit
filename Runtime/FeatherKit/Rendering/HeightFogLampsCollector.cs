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
    ///
    /// Лампа на краю списка не пропадает разом, а плавно гаснет, пока её догоняет первая
    /// лишняя: в момент, когда они меняются местами, её свет уже ноль. Лампа, которая только
    /// входит в кадр, считается далёкой и приближается вместе с тем, как входит, — иначе
    /// поворот камеры или шире угол обзора на бегу выбивали бы из списка лампу посреди кадра.
    /// </summary>
    internal sealed class HeightFogLampsCollector
    {
        /// <summary>
        /// Больше скольких ламп туман не берёт ни при каком качестве. Столько же ждёт шейдер.
        /// </summary>
        public const int MaxLamps = 8;

        // На скольких метрах лампа на краю списка гаснет — в долях её дальности, но не меньше метра.
        private const float FadeShareOfRange = 0.5f;
        private const float ShortestFade = 1f;

        // Ниже этой доли «насколько лампа в кадре» лампу не берём: её расстояние ушло бы в бесконечность.
        private const float LeastFrameShare = 0.001f;

        private readonly Plane[] frustumPlanes = new Plane[6];

        // Отобранные лампы с одной лишней: по ней считается, насколько гаснет последняя.
        private readonly Vector4[] candidateLamps = new Vector4[MaxLamps + 1];
        private readonly Vector4[] candidateSpheres = new Vector4[MaxLamps + 1];
        private readonly float[] candidateDistances = new float[MaxLamps + 1];


        // ── Выбор ламп ────────────────────────────────────────────────────────

        /// <summary>
        /// Кладёт в массивы ближайшие лампы и возвращает их число. В <paramref name="lamps"/> —
        /// x номер в данных URP, y множитель яркости в тумане (с плавным угасанием на краю
        /// списка), z даёт ли тень (1 или 0); в <paramref name="lampSpheres"/> — где лампа
        /// и докуда светит. Лампы за кадром URP уже отбросила сама.
        /// </summary>
        public int CollectNearest(UniversalLightData lightData, Vector3 cameraPosition, Matrix4x4 viewProjection,
                                  int limit, Vector4[] lamps, Vector4[] lampSpheres)
        {
            var maxCount = Mathf.Min(limit, MaxLamps);

            if (maxCount <= 0)
                return 0;

            GeometryUtility.CalculateFrustumPlanes(viewProjection, frustumPlanes);

            var candidateCount = CollectCandidates(lightData, cameraPosition, maxCount + 1);
            var count = Mathf.Min(candidateCount, maxCount);

            // Первая лишняя лампа — та, что догоняет последние в списке. Нет её — гасить некого.
            var firstLeftOutDistance = candidateCount > maxCount ? candidateDistances[maxCount] : float.PositiveInfinity;

            for (var i = 0; i < count; i++)
            {
                var fadeDistance = Mathf.Max(candidateSpheres[i].w * FadeShareOfRange, ShortestFade);
                var weight = Mathf.Clamp01((firstLeftOutDistance - candidateDistances[i]) / fadeDistance);
                var lampData = candidateLamps[i];

                lampData.y *= weight;
                lamps[i] = lampData;
                lampSpheres[i] = candidateSpheres[i];
            }

            return count;
        }

        // Ближайшие лампы, не больше capacity, по порядку от ближней.
        private int CollectCandidates(UniversalLightData lightData, Vector3 cameraPosition, int capacity)
        {
            var visibleLights = lightData.visibleLights;
            var maxUrpLamps = UniversalRenderPipeline.maxVisibleAdditionalLights;
            var count = 0;

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

                if (!TryCalculateLampDistance(cameraPosition, position, lamp.range, out var distance))
                    continue;

                // Лампе без теней тень в тумане не читаем вовсе — даже пустую выборку.
                var hasShadow = castsShadows && lamp.light != null && lamp.light.shadows != LightShadows.None;

                var lampData = new Vector4(urpIndex, intensity, hasShadow ? 1f : 0f, 0f);
                var sphere = new Vector4(position.x, position.y, position.z, lamp.range);

                count = InsertByDistance(count, capacity, lampData, sphere, distance);
            }

            return count;
        }

        // Номер лампы в массивах URP. URP складывает туда все видимые источники по порядку,
        // пропуская только солнце, — так же считают и поверхности, и карта теней.
        private static int ToUrpLampIndex(int visibleIndex, int mainLightIndex)
        {
            return mainLightIndex >= 0 && visibleIndex > mainLightIndex ? visibleIndex - 1 : visibleIndex;
        }

        // Насколько лампа далеко для выбора, м. Считается до края её света, а не до неё самой:
        // большая лампа вдалеке важнее маленькой рядом. Камера в свете лампы — расстояние
        // меньше нуля, и чем глубже, тем меньше. Лампа, которая лишь краем света в кадре,
        // считается дальше во столько раз, во сколько меньше её в кадре.
        private bool TryCalculateLampDistance(Vector3 cameraPosition, Vector3 position, float range, out float distance)
        {
            distance = Vector3.Distance(cameraPosition, position) - range;

            if (distance <= 0f)
                return true;

            var frameShare = CalculateFrameShare(position, range);

            if (frameShare < LeastFrameShare)
                return false;

            distance /= frameShare;

            return true;
        }

        // Насколько свет лампы в кадре: ноль — шар её света только касается края кадра, единица —
        // середина лампы внутри кадра.
        private float CalculateFrameShare(Vector3 position, float range)
        {
            var share = 1f;

            for (var i = 0; i < frustumPlanes.Length; i++)
                share = Mathf.Min(share, (frustumPlanes[i].GetDistanceToPoint(position) + range) / Mathf.Max(range, 1e-3f));

            return Mathf.Clamp01(share);
        }

        // Вставка в отсортированный по расстоянию список, лишняя дальняя — вылетает.
        private int InsertByDistance(int count, int capacity, Vector4 lampData, Vector4 sphere, float distance)
        {
            var index = count;

            while (index > 0 && candidateDistances[index - 1] > distance)
                index--;

            if (index >= capacity)
                return count;

            var last = Mathf.Min(count, capacity - 1);

            for (var i = last; i > index; i--)
            {
                candidateLamps[i] = candidateLamps[i - 1];
                candidateSpheres[i] = candidateSpheres[i - 1];
                candidateDistances[i] = candidateDistances[i - 1];
            }

            candidateLamps[index] = lampData;
            candidateSpheres[index] = sphere;
            candidateDistances[index] = distance;

            return Mathf.Min(count + 1, capacity);
        }
    }
}
