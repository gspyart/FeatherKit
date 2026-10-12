using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Во сколько раз уменьшать разрешение, в котором считается туман. Число — делитель
    /// стороны кадра, поэтому x2 даёт вчетверо меньше пикселей, а x4 — в шестнадцать раз.
    /// Туман низкочастотный, и на растяжении потеря разрешения почти не читается — видно
    /// её только по краям силуэтов вблизи.
    /// </summary>
    public enum HeightFogDownscale
    {
        [InspectorName("x1")]
        X1 = 1,

        [InspectorName("x2")]
        X2 = 2,

        [InspectorName("x4")]
        X4 = 4,
    }

    /// <summary>
    /// Рисует туман по высоте, настройки которого пришли из Volume. Отвечает за то, КОГДА,
    /// НАДО ЛИ и НАСКОЛЬКО подробно его рисовать: спрашивает стек Volume текущей камеры
    /// и, если тумана там нет, прохода не ставит вовсе. Сами числа тумана живут
    /// в <see cref="HeightFogVolume"/>, высота пола под камеру — в <see cref="HeightFogFloorTracker"/>,
    /// отрисовка — в <see cref="HeightFogPass"/>.
    ///
    /// Добавляется в ассет рендерера один раз на проект: участки карты со своим туманом
    /// делаются локальными Volume, а не отдельными фичами.
    /// </summary>
    [DisallowMultipleRendererFeature("Height Fog")]
    public class HeightFogFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// Сколько клубов укладывается в текстуру клубов по каждой стороне. Общее число
        /// для того, кто текстуру запекает, и для прохода, который переводит размер клуба
        /// в метрах в масштаб текстуры.
        /// </summary>
        public const int NoiseVolumeSpotsPerTile = 4;

        private const string NoiseVolumeKeyword = "_HEIGHT_FOG_NOISE_VOLUME";

        private static readonly int NoiseVolumeId = Shader.PropertyToID("_HeightFogNoiseVolume");
        private static readonly int StepCountId = Shader.PropertyToID("_HeightFogStepCount");

        [Tooltip("Шейдер тумана — featherHeightFog из этого же пакета. Пустое поле значит, " +
                 "что рисовать нечем: фича ругнётся в консоль один раз и промолчит дальше")]
        [SerializeField] private Shader shader;

        [Tooltip("Когда подмешивать туман в кадр. До прозрачных (Before Rendering " +
                 "Transparents) — дым, трава и стёкла остаются поверх пелены: глубины у них " +
                 "нет, и правильной густоты им всё равно не посчитать. До постобработки " +
                 "(Before Rendering Post Processing) — тонет и прозрачное, но густота у него " +
                 "будет от того, что за ним. События раньше непрозрачных тумана не дадут " +
                 "вовсе: глубина сцены к тому времени ещё не готова")]
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

        [Tooltip("Во сколько раз уменьшить разрешение, в котором считается туман. Главный " +
                 "рычаг цены: шаги по туману идут на каждый пиксель, и x2 снимает три четверти " +
                 "работы. На стилизованной картинке разница видна только по краям силуэтов " +
                 "вблизи")]
        [SerializeField] private HeightFogDownscale downscale = HeightFogDownscale.X2;

        [Tooltip("3D-текстура клубов тумана. Готовая — featherHeightFogNoise в пакете, своя " +
                 "печётся кнопкой FeatherKit → Запечь клубы тумана. Пусто — туман ровный, " +
                 "без клубов")]
        [SerializeField] private Texture3D noiseVolume;

        [Tooltip("Сколько шагов делает взгляд сквозь туман. Больше — клубы чётче и без " +
                 "зерна, но каждый шаг стоит выборку текстуры на каждый пиксель тумана. " +
                 "Первое, что снижать, если кадр не укладывается в бюджет")]
        [SerializeField, Range(4, 32)] private int stepCount = 12;

        private HeightFogPass pass;
        private readonly HeightFogFloorTracker floorTracker = new HeightFogFloorTracker();
        private Material material;
        private bool missingShaderReported;


        // ── Жизнь фичи ────────────────────────────────────────────────────────

        public override void Create()
        {
            pass = new HeightFogPass();
            missingShaderReported = false;
        }

        protected override void Dispose(bool disposing)
        {
            // Материал наш собственный, из шейдера, — значит и убирать его нам. Ассета
            // у него нет, поэтому иначе он остался бы висеть до перезапуска редактора.
            CoreUtils.Destroy(material);
            material = null;
        }


        // ── Кадр ──────────────────────────────────────────────────────────────

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;

            // Превью материалов и отражения рисуются своими камерами, и туман в них
            // только мешает: иконка ассета оказалась бы затянутой пеленой.
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            var settings = GetActiveSettings();

            if (settings == null || !TryPrepareMaterial())
                return;

            pass.renderPassEvent = injectionPoint;

            // Глубина сцены — единственное, что тумана требует от URP. Просим её здесь,
            // до записи графа: иначе в дешёвом кадре её просто не станут готовить.
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);

            var floorHeight = floorTracker.GetFloorHeight(renderingData.cameraData.camera, settings);

            pass.Setup(material, settings, floorHeight, (int)downscale);

            renderer.EnqueuePass(pass);
        }

        // Стек Volume к этому моменту уже посчитан под текущую камеру — URP обновляет его
        // раньше, чем спрашивает фичи. Поэтому берём готовый ответ, а не ищем Volume сами.
        private HeightFogVolume GetActiveSettings()
        {
            var stack = VolumeManager.instance?.stack;

            if (stack == null)
                return null;

            var settings = stack.GetComponent<HeightFogVolume>();

            return settings != null && settings.IsActive() ? settings : null;
        }

        // Ругаемся один раз, а не каждый кадр: фича вызывается на каждую камеру, и консоль
        // забилась бы одной и той же строкой за секунду.
        private bool TryPrepareMaterial()
        {
            if (shader == null)
            {
                if (!missingShaderReported)
                {
                    missingShaderReported = true;
                    Debug.LogWarning($"Фича «{name}» не рисует туман: не задан шейдер.");
                }

                return false;
            }

            if (material == null)
                material = CoreUtils.CreateEngineMaterial(shader);

            if (material == null)
                return false;

            // Ключевые слова и текстура держатся здесь, а не в проходе: они живут
            // в материале, а материал один на все камеры — раскладывать их по блоку свойств
            // на каждый проход незачем.
            CoreUtils.SetKeyword(material, NoiseVolumeKeyword, noiseVolume != null);
            material.SetFloat(StepCountId, stepCount);

            if (noiseVolume != null)
                material.SetTexture(NoiseVolumeId, noiseVolume);

            return true;
        }
    }
}
