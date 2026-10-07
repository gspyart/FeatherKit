using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Полноэкранный проход тумана: раскладывает числа из Volume по блоку свойств и рисует
    /// туман — либо прямо в кадр, либо в свою текстуру пониженного разрешения, которую потом
    /// растягивает поверх кадра. Отвечает ТОЛЬКО за отрисовку: надо ли рисовать, решает
    /// <see cref="HeightFogFeature"/>, а сами числа живут в <see cref="HeightFogVolume"/>.
    /// </summary>
    internal sealed class HeightFogPass : ScriptableRenderPass
    {
        private const string DirectPassName = "Height Fog";
        private const string OffscreenPassName = "Height Fog (в свою текстуру)";
        private const string CompositePassName = "Height Fog (растянуть)";

        // Порядок проходов в шейдере.
        private const int DirectShaderPass = 0;
        private const int OffscreenShaderPass = 1;
        private const int CompositeShaderPass = 2;

        private static readonly int ColorId = Shader.PropertyToID("_HeightFogColor");
        private static readonly int LayerId = Shader.PropertyToID("_HeightFogLayer");
        private static readonly int NoiseId = Shader.PropertyToID("_HeightFogNoise");
        private static readonly int WindId = Shader.PropertyToID("_HeightFogWind");
        private static readonly int DepthTapId = Shader.PropertyToID("_HeightFogDepthTap");
        private static readonly int SourceId = Shader.PropertyToID("_BlitTexture");

        // Полноэкранный треугольник из Blit.hlsl растягивается по этим числам. Своей копии
        // экрана проход не заводит, поэтому масштаб всегда единичный — но выставить его надо:
        // без него вершинный шейдер возьмёт мусор от предыдущего блита.
        private static readonly int ScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly Vector4 NoScaleBias = new Vector4(1f, 1f, 0f, 0f);

        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        private Material material;
        private int resolutionDivider = 1;
        private Vector4 color;
        private Vector4 layer;
        private Vector4 noise;
        private Vector4 wind;
        private Vector4 depthTap;


        public HeightFogPass()
        {
            profilingSampler = new ProfilingSampler(DirectPassName);
        }


        // ── Настройка ─────────────────────────────────────────────────────────

        // Снимок настроек берём здесь, а не в момент отрисовки: стек Volume к этому времени
        // уже посчитан под нужную камеру, а сам граф исполняется позже и про камеру,
        // для которой его записали, уже не спрашивает.
        public void Setup(Material fogMaterial, HeightFogVolume settings, int divider)
        {
            material = fogMaterial;
            resolutionDivider = Mathf.Max(divider, 1);

            color = ToShaderColor(settings.color.value);

            var reach = Mathf.Max(settings.maxDistance.value, 1f);
            var thickness = Mathf.Max(settings.layerThickness.value, 0.1f);

            layer = new Vector4(ToDensity(settings.coverage.value, thickness),
                                settings.floorHeight.value,
                                1f / thickness,
                                reach);

            noise = new Vector4(1f / Mathf.Max(settings.spotSize.value, 0.5f),
                                settings.noiseStrength.value,
                                settings.fogSky.value ? 1f : 0f,
                                settings.noiseBrightness.value);

            wind = settings.wind.value;
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

        // Цвет из Volume — в то, что ждёт шейдер. Свойства материала Unity переводит
        // в линейное пространство сама, а блок свойств отдаёт числа как есть, и туман
        // оказался бы заметно светлее выбранного на палитре.
        private static Vector4 ToShaderColor(Color value)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? value.linear : value;
        }


        // ── Кадр ──────────────────────────────────────────────────────────────

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();

            if (material == null || !resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                return;

            if (resolutionDivider <= 1)
            {
                depthTap = Vector4.zero;
                RecordDirectPass(renderGraph, resources);
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();

            depthTap = CalculateDepthTap(cameraData);

            var fogTexture = CreateFogTexture(renderGraph, resources, cameraData);

            RecordOffscreenPass(renderGraph, resources, fogTexture);
            RecordCompositePass(renderGraph, resources, fogTexture);
        }

        // Полное разрешение: считаем и подмешиваем за один проход, промежуточной текстуры нет.
        private void RecordDirectPass(RenderGraph renderGraph, UniversalResourceData resources)
        {
            using (var builder = renderGraph.AddRasterRenderPass<HeightFogPassData>(DirectPassName, out var passData, profilingSampler))
            {
                FillPassData(passData, DirectShaderPass);

                // Глубину объявляем зависимостью явно: иначе граф вправе не готовить её
                // к нашему проходу, и туман лёг бы по чужому кадру.
                builder.UseTexture(resources.cameraDepthTexture);

                // ReadWrite, а не Write: туман подмешивается альфа-блендом, то есть читает
                // то, что под ним. Чистая запись разрешила бы графу выбросить содержимое
                // цели перед проходом — на тайловых GPU под туманом остался бы мусор.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);

                builder.SetRenderFunc((HeightFogPassData data, RasterGraphContext context) => Render(context.cmd, data));
            }
        }

        // Пониженное разрешение: сперва считаем туман в свою текстуру...
        private void RecordOffscreenPass(RenderGraph renderGraph, UniversalResourceData resources, TextureHandle fogTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<HeightFogPassData>(OffscreenPassName, out var passData, profilingSampler))
            {
                FillPassData(passData, OffscreenShaderPass);

                builder.UseTexture(resources.cameraDepthTexture);

                // Здесь запись честно полная: текстура наша, очищается перед проходом,
                // и читать из неё в этом же проходе нечего.
                builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((HeightFogPassData data, RasterGraphContext context) => Render(context.cmd, data));
            }
        }

        // ...а потом растягиваем её поверх кадра.
        private void RecordCompositePass(RenderGraph renderGraph, UniversalResourceData resources, TextureHandle fogTexture)
        {
            using (var builder = renderGraph.AddRasterRenderPass<HeightFogPassData>(CompositePassName, out var passData, profilingSampler))
            {
                FillPassData(passData, CompositeShaderPass);
                passData.source = fogTexture;

                builder.UseTexture(fogTexture);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);

                builder.SetRenderFunc((HeightFogPassData data, RasterGraphContext context) => Render(context.cmd, data));
            }
        }

        // Смещение от середины блока до центра его углового пикселя, в uv кадра. Считаем здесь,
        // а не от _CameraDepthTexture_TexelSize: тот глобал приходит от чужой текстуры и врёт размером.
        private Vector4 CalculateDepthTap(UniversalCameraData cameraData)
        {
            var step = resolutionDivider * 0.5f - 0.5f;

            return new Vector4(step / Mathf.Max(cameraData.scaledWidth, 1),
                               step / Mathf.Max(cameraData.scaledHeight, 1),
                               0f,
                               0f);
        }

        // Размер берём от кадра, а не от экрана: render scale и динамическое разрешение
        // уже учтены в нём, и туман обязан делиться от того же числа.
        private TextureHandle CreateFogTexture(RenderGraph renderGraph, UniversalResourceData resources, UniversalCameraData cameraData)
        {
            var description = renderGraph.GetTextureDesc(resources.activeColorTexture);

            description.name = "_HeightFogTexture";
            description.sizeMode = TextureSizeMode.Explicit;
            description.width = Mathf.Max(description.width / resolutionDivider, 1);
            description.height = Mathf.Max(description.height / resolutionDivider, 1);

            // Формат задаём свой: у кадра его может не быть вовсе — HDR-формат B10G11R11
            // альфу не хранит, а туману она и есть главное.
            description.format = cameraData.isHdrEnabled ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;

            description.filterMode = FilterMode.Bilinear;
            description.wrapMode = TextureWrapMode.Clamp;
            description.msaaSamples = MSAASamples.None;
            description.bindTextureMS = false;
            description.useMipMap = false;
            description.autoGenerateMips = false;

            // Чистим в прозрачное: туман рисуется не везде, и там, где его нет, в текстуре
            // должен остаться ноль, а не мусор прошлого кадра.
            description.clearBuffer = true;
            description.clearColor = Color.clear;

            return renderGraph.CreateTexture(description);
        }

        private void FillPassData(HeightFogPassData passData, int shaderPass)
        {
            passData.material = material;
            passData.shaderPass = shaderPass;

            // Обнуляем явно: экземпляры данных граф переиспользует между кадрами, и текстура
            // от прошлого прохода осталась бы лежать в поле у того, кто её не просил.
            passData.source = TextureHandle.nullHandle;

            passData.depthTap = depthTap;
            passData.color = color;
            passData.layer = layer;
            passData.noise = noise;
            passData.wind = wind;
        }

        // Блок свойств, а не материал: команда отрисовки исполняется позже записи, и значения,
        // положенные в материал, к тому времени успел бы перетереть соседний проход.
        private static void Render(RasterCommandBuffer command, HeightFogPassData data)
        {
            Properties.Clear();
            Properties.SetVector(ScaleBiasId, NoScaleBias);
            Properties.SetVector(ColorId, data.color);
            Properties.SetVector(LayerId, data.layer);
            Properties.SetVector(NoiseId, data.noise);
            Properties.SetVector(WindId, data.wind);
            Properties.SetVector(DepthTapId, data.depthTap);

            if (data.source.IsValid())
                Properties.SetTexture(SourceId, data.source);

            command.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, Properties);
        }


        // Данные одного записанного прохода. Граф исполняет проходы позже записи, поэтому
        // всё, что нужно отрисовке, кладётся сюда, а не читается из полей на месте.
        private class HeightFogPassData
        {
            public Material material;
            public int shaderPass;
            public Vector4 depthTap;
            public Vector4 color;
            public Vector4 layer;
            public Vector4 noise;
            public Vector4 wind;
            public TextureHandle source;
        }
    }
}
