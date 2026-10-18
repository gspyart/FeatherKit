using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Полноэкранный проход тумана: собирает числа кадра — из Volume, областей тумана
    /// и света сцены, — считает туман в свою текстуру и растягивает её поверх кадра,
    /// слегка размыв. Отвечает ТОЛЬКО за отрисовку: надо ли рисовать, решает
    /// <see cref="HeightFogFeature"/>, перевод в числа шейдера — <see cref="HeightFogShaderInputs"/>.
    ///
    /// Своя текстура и размытие даже в полном разрешении: начало шагов по лучу у соседних
    /// пикселей сдвинуто, чтобы не было полос, и без размытия этот сдвиг виден штриховкой.
    /// </summary>
    internal sealed class HeightFogPass : ScriptableRenderPass
    {
        private const string ProfilingName = "Height Fog";
        private const string OffscreenPassName = "Height Fog (в свою текстуру)";
        private const string CompositePassName = "Height Fog (растянуть)";

        // Порядок проходов в шейдере.
        private const int OffscreenShaderPass = 0;
        private const int CompositeShaderPass = 1;

        private static readonly int DepthTapId = Shader.PropertyToID("_HeightFogDepthTap");
        private static readonly int TexelSizeId = Shader.PropertyToID("_HeightFogTexelSize");
        private static readonly int SourceId = Shader.PropertyToID("_BlitTexture");

        // Полноэкранный треугольник из Blit.hlsl растягивается по этим числам. Своей копии
        // экрана проход не заводит, поэтому масштаб всегда единичный — но выставить его надо:
        // без него вершинный шейдер возьмёт мусор от предыдущего блита.
        private static readonly int ScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly Vector4 NoScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        private readonly HeightFogShaderInputs inputs = new HeightFogShaderInputs();
        private readonly HeightFogLampsCollector lampsCollector = new HeightFogLampsCollector();
        private Material material;
        private HeightFogShaderKeywords keywords;
        private HeightFogQualitySettings quality = HeightFogQualitySettings.ForPreset(HeightFogQuality.Medium);
        private float clubsBlurByStepLength = 1f;
        private Vector4 depthTap;


        public HeightFogPass()
        {
            profilingSampler = new ProfilingSampler(ProfilingName);
        }


        // Во сколько раз туман считается мельче кадра. Единица — в полном разрешении.
        private int ResolutionDivider => Mathf.Max((int)quality.Downscale, 1);


        // ── Настройка ─────────────────────────────────────────────────────────

        // Снимок настроек берём здесь, а не в момент отрисовки: стек Volume к этому времени
        // уже посчитан под нужную камеру, а сам граф исполняется позже и про камеру,
        // для которой его записали, уже не спрашивает.
        // Высота пола приходит отдельно: она зависит от камеры, а не только от Volume.
        public void Setup(Material fogMaterial, HeightFogVolume settings, float floorHeight, VisibleFogAreas areas, bool noiseVolumeAssigned)
        {
            material = fogMaterial;

            if (keywords == null || keywords.Shader != fogMaterial.shader)
                keywords = new HeightFogShaderKeywords(fogMaterial.shader);

            inputs.CaptureVolume(settings, floorHeight, noiseVolumeAssigned);
            inputs.CaptureAreas(areas);
        }


        // То, что задаёт цену тумана, а не его вид, — настройки фичи, а не Volume.
        public void SetQuality(HeightFogQualitySettings qualitySettings)
        {
            quality = qualitySettings;
            inputs.CaptureQuality(qualitySettings);
        }


        public void SetClubsBlurByStepLength(float blurByStepLength)
        {
            clubsBlurByStepLength = blurByStepLength;
        }


        // ── Кадр ──────────────────────────────────────────────────────────────

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();

            if (material == null || !resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                return;

            CaptureLight(frameData, resources);

            var cameraData = frameData.Get<UniversalCameraData>();

            depthTap = CalculateDepthTap(cameraData);

            var fogTexture = CreateFogTexture(renderGraph, resources);

            inputs.CaptureClubsFootprint(cameraData.GetProjectionMatrix(), cameraData.camera.orthographic,
                                         renderGraph.GetTextureDesc(fogTexture).height, clubsBlurByStepLength);

            RecordOffscreenPass(renderGraph, resources, fogTexture);
            RecordCompositePass(renderGraph, resources, fogTexture, CalculateTexelSize(renderGraph, fogTexture));
        }

        // Свет берём при записи графа: данные о свете и тенях URP собирает под камеру
        // как раз к этому времени, а в момент постановки прохода их ещё нет.
        private void CaptureLight(ContextContainer frameData, UniversalResourceData resources)
        {
            var lightData = frameData.Get<UniversalLightData>();
            var shadowData = frameData.Get<UniversalShadowData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var viewProjection = cameraData.GetProjectionMatrix() * cameraData.GetViewMatrix();

            inputs.CaptureLight(lightData, shadowData, resources, cameraData.worldSpaceCameraPos, viewProjection,
                                lampsCollector, quality);
        }

        // Сперва считаем туман в свою текстуру...
        private void RecordOffscreenPass(RenderGraph renderGraph, UniversalResourceData resources, TextureHandle fogTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<HeightFogPassData>(OffscreenPassName, out var passData, profilingSampler))
            {
                FillPassData(passData, OffscreenShaderPass);
                DeclareFogInputs(builder, resources);

                // Здесь запись честно полная: текстура наша, проход пишет каждый её тексель,
                // и читать из неё в этом же проходе нечего.
                builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((HeightFogPassData data, RasterGraphContext context) => Render(context.cmd, data));
            }
        }

        // ...а потом растягиваем её поверх кадра.
        private void RecordCompositePass(RenderGraph renderGraph, UniversalResourceData resources, TextureHandle fogTexture, Vector4 texelSize)
        {
            using (var builder = renderGraph.AddRasterRenderPass<HeightFogPassData>(CompositePassName, out var passData, profilingSampler))
            {
                FillPassData(passData, CompositeShaderPass);
                passData.source = fogTexture;
                passData.texelSize = texelSize;

                builder.UseTexture(fogTexture);

                // Глубина — чтобы размытие не тащило туман через края предметов.
                builder.UseTexture(resources.cameraDepthTexture);

                // ReadWrite, а не Write: туман подмешивается поверх кадра, то есть читает
                // то, что под ним. Чистая запись разрешила бы графу выбросить содержимое
                // цели перед проходом — на тайловых GPU под туманом остался бы мусор.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);

                builder.SetRenderFunc((HeightFogPassData data, RasterGraphContext context) => Render(context.cmd, data));
            }
        }

        // Что проход, считающий туман, берёт у графа. Глубину и карты теней объявляем явно:
        // иначе граф вправе не готовить глубину к нашему проходу или отдать память карт
        // теней кому-то раньше, чем туман их прочтёт.
        private void DeclareFogInputs(IRasterRenderGraphBuilder builder, UniversalResourceData resources)
        {
            builder.UseTexture(resources.cameraDepthTexture);

            // Части шейдера включаются командой прямо перед отрисовкой, а граф считает
            // это правкой общего состояния и без разрешения бросает исключение.
            builder.AllowGlobalStateModification(true);

            if (inputs.SunShadows != HeightFogSunShadows.Off)
                builder.UseTexture(resources.mainShadowsTexture);

            if (inputs.HasLampShadows)
                builder.UseTexture(resources.additionalShadowsTexture);
        }

        // Размер текселя своей текстуры в uv — по нему растяжение размывает туман.
        private static Vector4 CalculateTexelSize(RenderGraph renderGraph, TextureHandle fogTexture)
        {
            var description = renderGraph.GetTextureDesc(fogTexture);

            return new Vector4(1f / Mathf.Max(description.width, 1), 1f / Mathf.Max(description.height, 1), 0f, 0f);
        }

        // Смещение от середины блока до центра его углового пикселя, в uv кадра. Считаем здесь,
        // а не от _CameraDepthTexture_TexelSize: тот глобал приходит от чужой текстуры и врёт размером.
        private Vector4 CalculateDepthTap(UniversalCameraData cameraData)
        {
            var step = ResolutionDivider * 0.5f - 0.5f;

            return new Vector4(step / Mathf.Max(cameraData.scaledWidth, 1),
                               step / Mathf.Max(cameraData.scaledHeight, 1),
                               0f,
                               0f);
        }

        // Размер берём от кадра, а не от экрана: render scale и динамическое разрешение
        // уже учтены в нём, и туман обязан делиться от того же числа.
        private TextureHandle CreateFogTexture(RenderGraph renderGraph, UniversalResourceData resources)
        {
            var description = renderGraph.GetTextureDesc(resources.activeColorTexture);

            description.name = "_HeightFogTexture";
            description.sizeMode = TextureSizeMode.Explicit;
            description.width = Mathf.Max(description.width / ResolutionDivider, 1);
            description.height = Mathf.Max(description.height / ResolutionDivider, 1);

            // Формат задаём свой: у кадра его может не быть вовсе — HDR-формат B10G11R11
            // альфу не хранит, а туману она и есть главное. И всегда с плавающей точкой, даже
            // без HDR: в тёмной сцене свет тумана — сотые доли, и в восьми битах он лёг бы
            // ступенями, а свет ламп у самой лампы бывает ярче единицы.
            description.format = GraphicsFormat.R16G16B16A16_SFloat;

            description.filterMode = FilterMode.Bilinear;
            description.wrapMode = TextureWrapMode.Clamp;
            description.msaaSamples = MSAASamples.None;
            description.bindTextureMS = false;
            description.useMipMap = false;
            description.autoGenerateMips = false;

            // Не чистим: проход тумана пишет каждый тексель, и там, где тумана нет, — ноль.
            // Очистка была бы лишним проходом по памяти.
            description.clearBuffer = false;

            return renderGraph.CreateTexture(description);
        }

        private void FillPassData(HeightFogPassData passData, int shaderPass)
        {
            passData.material = material;
            passData.shaderPass = shaderPass;

            // Обнуляем явно: экземпляры данных граф переиспользует между кадрами, и текстура
            // от прошлого прохода осталась бы лежать в поле у того, кто её не просил.
            passData.source = TextureHandle.nullHandle;
            passData.texelSize = Vector4.zero;

            passData.depthTap = depthTap;
            // Растяжению части шейдера не нужны: туман уже посчитан.
            passData.keywords = shaderPass == CompositeShaderPass ? null : keywords;

            // Экземпляр данных у графа живёт долго — копию чисел заводим один раз и дальше
            // только переписываем.
            passData.inputs ??= new HeightFogShaderInputs();
            passData.inputs.CopyFrom(inputs);
        }

        // Блок свойств, а не материал: команда отрисовки исполняется позже записи, и значения,
        // положенные в материал, к тому времени успел бы перетереть соседний проход.
        private static void Render(RasterCommandBuffer command, HeightFogPassData data)
        {
            Properties.Clear();
            Properties.SetVector(ScaleBiasId, NoScaleBias);
            Properties.SetVector(DepthTapId, data.depthTap);
            Properties.SetVector(TexelSizeId, data.texelSize);
            data.inputs.ApplyTo(Properties);

            if (data.source.IsValid())
                Properties.SetTexture(SourceId, data.source);

            data.keywords?.Apply(command, data.material, data.inputs);

            command.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, Properties);
        }


        // Данные одного записанного прохода. Граф исполняет проходы позже записи, поэтому
        // всё, что нужно отрисовке, кладётся сюда, а не читается из полей на месте.
        private class HeightFogPassData
        {
            public Material material;
            public int shaderPass;
            public Vector4 depthTap;
            public Vector4 texelSize;
            public TextureHandle source;
            public HeightFogShaderKeywords keywords;
            public HeightFogShaderInputs inputs;
        }
    }
}
