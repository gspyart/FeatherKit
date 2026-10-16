using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Числа тумана для одного кадра одной камеры — в том виде, в каком их ждёт шейдер:
    /// общий слой, области, свет, лампы, качество, а ещё какие части шейдера включить.
    /// Отвечает за перевод настроек и найденного в сцене в векторы и за раскладку их
    /// по блоку свойств. Ничего не ищет и не рисует: области и лампы ему отдают сборщики,
    /// рисует <see cref="HeightFogPass"/>.
    /// </summary>
    internal sealed class HeightFogShaderInputs
    {
        private const int MaxAreas = HeightFogAreasCollector.MaxAreas;
        private const int MaxLamps = HeightFogLampsCollector.MaxLamps;

        private static readonly int StepCountsId = Shader.PropertyToID("_HeightFogStepCounts");
        private static readonly int ColorId = Shader.PropertyToID("_HeightFogColor");
        private static readonly int LayerId = Shader.PropertyToID("_HeightFogLayer");
        private static readonly int NoiseId = Shader.PropertyToID("_HeightFogNoise");
        private static readonly int DetailId = Shader.PropertyToID("_HeightFogDetail");
        private static readonly int WindId = Shader.PropertyToID("_HeightFogWind");
        private static readonly int StartDistanceId = Shader.PropertyToID("_HeightFogStartDistance");
        private static readonly int LightingId = Shader.PropertyToID("_HeightFogLighting");

        private static readonly int AmbientId = Shader.PropertyToID("_HeightFogAmbient");
        private static readonly int SunColorId = Shader.PropertyToID("_HeightFogSunColor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_HeightFogSunDirection");
        private static readonly int LampCountId = Shader.PropertyToID("_HeightFogLampCount");
        private static readonly int LampsId = Shader.PropertyToID("_HeightFogLamps");
        private static readonly int LampSpheresId = Shader.PropertyToID("_HeightFogLampSpheres");

        private static readonly int AreaCountId = Shader.PropertyToID("_HeightFogAreaCount");
        private static readonly int AreaWorldToLocalId = Shader.PropertyToID("_HeightFogAreaWorldToLocal");
        private static readonly int AreaColorId = Shader.PropertyToID("_HeightFogAreaColor");
        private static readonly int AreaShapeId = Shader.PropertyToID("_HeightFogAreaShape");
        private static readonly int AreaClubsId = Shader.PropertyToID("_HeightFogAreaClubs");
        private static readonly int AreaWindId = Shader.PropertyToID("_HeightFogAreaWind");
        private static readonly int AreaLightingId = Shader.PropertyToID("_HeightFogAreaLighting");
        private static readonly int AreaVisibilityId = Shader.PropertyToID("_HeightFogAreaVisibility");

        private Vector4 stepCounts;

        private Vector4 color;
        private Vector4 layer;
        private Vector4 noise;
        private Vector4 detail;
        private Vector4 wind;
        private float startDistance;
        private Vector4 lighting;

        private Vector4 ambient;
        private Vector4 sunColor;
        private Vector4 sunDirection;
        private int lampCount;
        private readonly Vector4[] lamps = new Vector4[MaxLamps];
        private readonly Vector4[] lampSpheres = new Vector4[MaxLamps];

        private int areaCount;
        private readonly Matrix4x4[] areaWorldToLocal = new Matrix4x4[MaxAreas];
        private readonly Vector4[] areaColors = new Vector4[MaxAreas];
        private readonly Vector4[] areaShapes = new Vector4[MaxAreas];
        private readonly Vector4[] areaClubs = new Vector4[MaxAreas];
        private readonly Vector4[] areaWinds = new Vector4[MaxAreas];
        private readonly Vector4[] areaLighting = new Vector4[MaxAreas];
        private readonly Vector4[] areaVisibility = new Vector4[MaxAreas];

        // Что из необязательного нужно хоть кому-то — общему туману или области.
        private bool hasNoiseVolume;
        private bool wantsClubs;
        private bool wantsDetail;
        private bool wantsLight;
        private bool wantsLamps;
        private bool hasCuttingAreas;


        // ── Какие части шейдера нужны ─────────────────────────────────────────

        public bool HasClubs => hasNoiseVolume && wantsClubs;
        public bool HasDetailClubs => HasClubs && wantsDetail;
        public bool HasAreas => areaCount > 0;
        public bool CutsGlobalFog => hasCuttingAreas;
        public bool IsLit => wantsLight;
        public bool HasLamps => wantsLight && lampCount > 0;
        public HeightFogSunShadows SunShadows { get; private set; }
        public bool HasLampShadows { get; private set; }

        private bool HasGlobalFog => layer.x > 0f;


        // ── Общий туман из Volume ─────────────────────────────────────────────

        // Высота пола приходит отдельно: она зависит от камеры, а не только от Volume.
        // Нулевая густота при закрытии ноль — шейдер по ней пропускает общий туман целиком.
        public void CaptureVolume(HeightFogVolume settings, float floorHeight, bool noiseVolumeAssigned)
        {
            hasNoiseVolume = noiseVolumeAssigned;

            // В w — насколько туман закрывает то, что за ним.
            color = ToShaderColor(settings.color.value);
            color.w = settings.opacity.value;

            var reach = Mathf.Max(settings.maxDistance.value, 1f);
            var thickness = Mathf.Max(settings.layerThickness.value, 0.1f);

            layer = new Vector4(ToDensity(settings.coverage.value, thickness),
                                floorHeight,
                                1f / thickness,
                                reach);

            noise = new Vector4(ToClubsScale(settings.spotSize.value),
                                settings.noiseStrength.value,
                                settings.fogSky.value ? 1f : 0f,
                                settings.noiseBrightness.value);

            detail = new Vector4(ToClubsScale(settings.detailSize.value), settings.detailStrength.value, 0f, 0f);
            wind = settings.wind.value;
            startDistance = Mathf.Max(settings.startDistance.value, 0f);

            lighting = new Vector4(settings.lightResponse.value,
                                   settings.glowTowardLight.value,
                                   Mathf.Max(settings.lampStrength.value, 0f),
                                   Mathf.Max(settings.ambientStrength.value, 0f));

            // Без общего тумана его клубы и свет никому не нужны.
            wantsClubs = HasGlobalFog && (noise.y > 0f || noise.w != 0f);
            wantsDetail = wantsClubs && detail.y > 0f;
            wantsLight = HasGlobalFog && lighting.x > 0f;
            wantsLamps = wantsLight && lighting.z > 0f;
        }

        // Доля закрытия — в плотность на метр. Обратная сторона того, как туман копится:
        // пелена растёт по формуле «единица минус экспонента», поэтому обратный ход —
        // логарифм. Человеку при этом остаётся понятное «сквозь слой видно столько-то»,
        // и вход в зону идёт ровно, а не выскакивает в первой трети бленда.
        //
        // Делим на ТОЛЩИНУ, а не на дальность: столб тумана над полом при взгляде сверху
        // равен плотности, помноженной на толщину, — дальность в него не входит вовсе.
        // Пока делили на дальность, число в инспекторе обещало закрытие горизонтального луча
        // длиной в дальность, а сверху луч режет слой лишь на его толщину: закрытие 0.2
        // при слое 0.6 м и дальности 60 давало на полу 0.3%.
        private static float ToDensity(float coverage, float thickness)
        {
            return -Mathf.Log(1f - Mathf.Clamp(coverage, 0f, 0.99f)) / thickness;
        }


        // ── Области тумана ────────────────────────────────────────────────────

        // Вызывать после CaptureVolume: области дописывают, что из необязательного нужно им.
        public void CaptureAreas(VisibleFogAreas visible)
        {
            areaCount = Mathf.Min(visible.Count, MaxAreas);
            hasCuttingAreas = false;

            for (var i = 0; i < areaCount; i++)
            {
                var area = visible.Areas[i];
                var fade = visible.Fades[i];

                areaWorldToLocal[i] = area.WorldToArea;
                areaColors[i] = ToAreaColor(area, fade);
                areaShapes[i] = ToAreaShape(area);
                areaClubs[i] = new Vector4(ToClubsScale(area.SpotSize), area.NoiseStrength, area.NoiseBrightness, area.DetailStrength);
                areaWinds[i] = new Vector4(area.Wind.x, area.Wind.y, area.Wind.z, ToClubsScale(area.DetailSize));
                areaLighting[i] = new Vector4(area.LightResponse, area.GlowTowardLight, area.LampStrength, area.AmbientStrength);
                areaVisibility[i] = new Vector4(area.Opacity, fade, 0f, 0f);

                // Общий туман вырезать есть смысл, только если он есть.
                hasCuttingAreas |= area.BlendMode != FogAreaBlendMode.Add && HasGlobalFog;

                if (!area.HasOwnFog)
                    continue;

                var areaHasClubs = area.NoiseStrength > 0f || area.NoiseBrightness != 0f;

                wantsClubs |= areaHasClubs;
                wantsDetail |= areaHasClubs && area.DetailStrength > 0f;
                wantsLight |= area.LightResponse > 0f;
                wantsLamps |= area.LightResponse > 0f && area.LampStrength > 0f;
            }
        }

        // Густота в цвете — уже с затуханием вдали. У чистой области своего тумана нет.
        private static Vector4 ToAreaColor(FogArea area, float fade)
        {
            var areaColor = ToShaderColor(area.Color);
            var density = area.HasOwnFog ? fade / Mathf.Max(area.VisibilityDistance, 0.1f) : 0f;

            return new Vector4(areaColor.x, areaColor.y, areaColor.z, density);
        }

        // Слой у пола — в долях высоты области: шейдер считает высоту внутри области,
        // где она от −0.5 до 0.5, и про метры не знает.
        private static Vector4 ToAreaShape(FogArea area)
        {
            var floorFalloff = area.FloorLayerThickness > 0f ? area.WorldHeight / area.FloorLayerThickness : 0f;

            return new Vector4((float)area.Shape, area.EdgeSoftness, floorFalloff, (float)area.BlendMode);
        }


        // ── Свет сцены и качество ─────────────────────────────────────────────

        public void CaptureQuality(HeightFogQualitySettings quality)
        {
            stepCounts = new Vector4(quality.StepCount, quality.LampStepCount, 0f, 0f);
        }


        // Тени включаются, только если URP их в этом кадре нарисовала: иначе шейдер читал бы
        // карту теней, которой нет. Неосвещённому туману свет не нужен вовсе — не ищем его.
        public void CaptureLight(UniversalLightData lightData, UniversalShadowData shadowData,
                                 UniversalResourceData resources, Vector3 cameraPosition, Matrix4x4 viewProjection,
                                 HeightFogLampsCollector lampsCollector, HeightFogQualitySettings quality)
        {
            SunShadows = HeightFogSunShadows.Off;
            HasLampShadows = false;
            lampCount = 0;

            if (!wantsLight)
                return;

            ambient = HeightFogSceneLight.CalculateAmbientColor();
            HeightFogSceneLight.FindSun(lightData, out sunColor, out sunDirection, out var sunCastsShadows);

            var hasSunShadows = quality.SunShadows && sunCastsShadows && sunColor != Vector4.zero
                                && shadowData.supportsMainLightShadows && resources.mainShadowsTexture.IsValid();

            if (hasSunShadows)
                SunShadows = shadowData.mainLightShadowCascadesCount > 1 ? HeightFogSunShadows.Cascades : HeightFogSunShadows.Single;

            if (!wantsLamps)
                return;

            lampCount = lampsCollector.CollectNearest(lightData, cameraPosition, viewProjection, quality.MaxLamps, lamps, lampSpheres);

            HasLampShadows = quality.LampShadows && HasShadowedLamp()
                             && shadowData.supportsAdditionalLightShadows && resources.additionalShadowsTexture.IsValid();
        }

        private bool HasShadowedLamp()
        {
            for (var i = 0; i < lampCount; i++)
            {
                if (lamps[i].z > 0f)
                    return true;
            }

            return false;
        }


        // ── Копия и раскладка ─────────────────────────────────────────────────

        // Граф рисует позже записи, а записанных проходов в кадре несколько, поэтому
        // каждому — своя копия чисел, а не ссылка на общие.
        public void CopyFrom(HeightFogShaderInputs source)
        {
            stepCounts = source.stepCounts;

            color = source.color;
            layer = source.layer;
            noise = source.noise;
            detail = source.detail;
            wind = source.wind;
            startDistance = source.startDistance;
            lighting = source.lighting;

            ambient = source.ambient;
            sunColor = source.sunColor;
            sunDirection = source.sunDirection;
            lampCount = source.lampCount;
            source.lamps.CopyTo(lamps, 0);
            source.lampSpheres.CopyTo(lampSpheres, 0);

            areaCount = source.areaCount;
            source.areaWorldToLocal.CopyTo(areaWorldToLocal, 0);
            source.areaColors.CopyTo(areaColors, 0);
            source.areaShapes.CopyTo(areaShapes, 0);
            source.areaClubs.CopyTo(areaClubs, 0);
            source.areaWinds.CopyTo(areaWinds, 0);
            source.areaLighting.CopyTo(areaLighting, 0);
            source.areaVisibility.CopyTo(areaVisibility, 0);

            hasNoiseVolume = source.hasNoiseVolume;
            wantsClubs = source.wantsClubs;
            wantsDetail = source.wantsDetail;
            wantsLight = source.wantsLight;
            wantsLamps = source.wantsLamps;
            hasCuttingAreas = source.hasCuttingAreas;
            SunShadows = source.SunShadows;
            HasLampShadows = source.HasLampShadows;
        }


        // Массивы кладутся всегда полной длины: Unity запоминает размер массива свойства
        // при первой записи, и короткий массив потом молча обрезал бы длинный.
        public void ApplyTo(MaterialPropertyBlock properties)
        {
            properties.SetVector(StepCountsId, stepCounts);

            properties.SetVector(ColorId, color);
            properties.SetVector(LayerId, layer);
            properties.SetVector(NoiseId, noise);
            properties.SetVector(DetailId, detail);
            properties.SetVector(WindId, wind);
            properties.SetFloat(StartDistanceId, startDistance);
            properties.SetVector(LightingId, lighting);

            properties.SetVector(AmbientId, ambient);
            properties.SetVector(SunColorId, sunColor);
            properties.SetVector(SunDirectionId, sunDirection);
            properties.SetFloat(LampCountId, lampCount);
            properties.SetVectorArray(LampsId, lamps);
            properties.SetVectorArray(LampSpheresId, lampSpheres);

            properties.SetFloat(AreaCountId, areaCount);
            properties.SetMatrixArray(AreaWorldToLocalId, areaWorldToLocal);
            properties.SetVectorArray(AreaColorId, areaColors);
            properties.SetVectorArray(AreaShapeId, areaShapes);
            properties.SetVectorArray(AreaClubsId, areaClubs);
            properties.SetVectorArray(AreaWindId, areaWinds);
            properties.SetVectorArray(AreaLightingId, areaLighting);
            properties.SetVectorArray(AreaVisibilityId, areaVisibility);
        }


        // ── Общие помощники ───────────────────────────────────────────────────

        // Цвет из настроек — в то, что ждёт шейдер. Свойства материала Unity переводит
        // в линейное пространство сама, а блок свойств отдаёт числа как есть, и туман
        // оказался бы заметно светлее выбранного на палитре.
        private static Vector4 ToShaderColor(Color value)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? value.linear : value;
        }

        // Размер клуба в метрах — в масштаб текстуры клубов. Текстура повторяется через
        // несколько клубов, а не через один: масштаб делим на их число, чтобы размер клуба
        // в метрах остался тем, что в инспекторе.
        private static float ToClubsScale(float spotSize)
        {
            return 1f / (Mathf.Max(spotSize, 0.1f) * HeightFogFeature.NoiseVolumeSpotsPerTile);
        }
    }

    /// <summary>
    /// Как туман берёт тени солнца: никак, с одной карты теней или с каскадов.
    /// Повторяет то, как URP рисует тени в этом кадре.
    /// </summary>
    internal enum HeightFogSunShadows
    {
        Off,
        Single,
        Cascades,
    }
}
