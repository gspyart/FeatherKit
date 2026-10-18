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
    /// Рисует туман: общий, по высоте, из Volume, и области тумана (<see cref="FogArea"/>).
    /// Отвечает за то, КОГДА, НАДО ЛИ и НАСКОЛЬКО подробно его рисовать: спрашивает стек
    /// Volume текущей камеры и ищет области в кадре, и если нет ни того, ни другого, прохода
    /// не ставит вовсе. Сами числа тумана живут в <see cref="HeightFogVolume"/>, высота пола
    /// под камеру — в <see cref="HeightFogFloorTracker"/>, выбор областей —
    /// в <see cref="HeightFogAreasCollector"/>, отрисовка — в <see cref="HeightFogPass"/>.
    ///
    /// Добавляется в ассет рендерера один раз на проект: участки карты со своим туманом
    /// делаются локальными Volume или областями тумана, а не отдельными фичами.
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

        private static readonly int NoiseVolumeId = Shader.PropertyToID("_HeightFogNoiseVolume");
        private static readonly int NoiseTexelsId = Shader.PropertyToID("_HeightFogNoiseTexels");

        [Header("Отрисовка")]
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

        [Tooltip("3D-текстура клубов тумана. Готовая — featherHeightFogNoise в пакете, своя " +
                 "печётся кнопкой FeatherKit → Запечь клубы тумана. Пусто — туман ровный, " +
                 "без клубов")]
        [SerializeField] private Texture3D noiseVolume;

        [Tooltip("Насколько клубы сглаживаются на длинных шагах по лучу. 1 — где шаг длиннее " +
                 "клуба, клубы усредняются: вдали нет ряби, и там они почти ничего не стоят. " +
                 "0 — клубы сглаживаются только по размеру пикселя: вдали они рябят зерном " +
                 "и стоят дороже")]
        [SerializeField, Range(0f, 1f)] private float clubsBlurByStepLength = 1f;

        [Header("Качество")]
        [Tooltip("Насколько подробно считать туман. Low — дёшево: разрешение x4, 8 шагов, " +
                 "2 лампы по 4 шага, без теней ламп. Medium — x2, 12 шагов, 4 лампы по 6 шагов, " +
                 "все тени. High — полное разрешение, 16 шагов, 8 ламп по 10 шагов, все тени: " +
                 "вчетверо дороже Medium. Custom — всё задаётся полями ниже. Выключить туман " +
                 "совсем — снять галку у самой фичи: тогда он не стоит ничего")]
        // Custom по умолчанию — чтобы у проектов, где числа уже подобраны руками, ничего не поменялось.
        [SerializeField] private HeightFogQuality quality = HeightFogQuality.Custom;

        [Header("Своё качество — только для Quality = Custom")]
        [Tooltip("Во сколько раз уменьшить разрешение, в котором считается туман. Главный " +
                 "рычаг цены: шаги по туману идут на каждый пиксель, и x2 снимает три четверти " +
                 "работы. На стилизованной картинке разница видна только по краям силуэтов " +
                 "вблизи")]
        [SerializeField] private HeightFogDownscale downscale = HeightFogDownscale.X2;

        [Tooltip("Сколько шагов делает взгляд сквозь туман. Больше — клубы чётче и без " +
                 "зерна, но каждый шаг стоит выборку текстуры на каждый пиксель тумана. " +
                 "Первое, что снижать, если кадр не укладывается в бюджет")]
        [SerializeField, Range(4, 32)] private int stepCount = 12;

        [Tooltip("Сколько ламп, ближайших к камере, светит в тумане. Каждая лампа стоит " +
                 "на каждом шаге, но только там, где луч проходит через её свет. Ноль — " +
                 "лампы туман не освещают")]
        [SerializeField, Range(0, HeightFogLampsCollector.MaxLamps)] private int maxLamps = HeightFogLampsCollector.MaxLamps;

        [Tooltip("Сколько шагов у света каждой лампы — только там, где луч проходит через её свет. " +
                 "Шаги гуще у самой лампы, поэтому ореолу хватает немногих. Больше — ровнее " +
                 "ореолы и лучи, но две лампы на луче стоят вдвое больше шагов")]
        [SerializeField, Range(2, 15)] private int lampStepCount = 8;

        [Tooltip("Тени солнца в освещённом тумане: лучи из окон, тёмный туман там, куда " +
                 "солнце не попадает. Стоит одну выборку карты теней на каждом шаге. " +
                 "Без галки туман светится солнцем и в закрытом подвале")]
        [SerializeField] private bool sunShadows = true;

        [Tooltip("Тени ламп в освещённом тумане: свет лампы в тумане перекрывают балки " +
                 "и стены. Дороже всего остального — выборка тени на каждую лампу на " +
                 "каждом шаге. Считается только для ламп, у которых тени включены")]
        [SerializeField] private bool lampShadows = true;

        private HeightFogPass pass;
        private readonly HeightFogFloorTracker floorTracker = new HeightFogFloorTracker();
        private readonly HeightFogAreasCollector areasCollector = new HeightFogAreasCollector();
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

            var settings = GetSettings();

            if (settings == null)
                return;

            var camera = renderingData.cameraData.camera;
            var areas = areasCollector.CollectVisible(camera);

            // Ни общего тумана, ни областей в кадре — проход не ставим, кадр ничего не стоит.
            if (!settings.IsActive() && areas.Count == 0)
                return;

            if (!TryPrepareMaterial())
                return;

            pass.renderPassEvent = injectionPoint;

            // Глубина сцены — единственное, что тумана требует от URP. Просим её здесь,
            // до записи графа: иначе в дешёвом кадре её просто не станут готовить.
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);

            var floorHeight = floorTracker.GetFloorHeight(camera, settings);

            pass.Setup(material, settings, floorHeight, areas, noiseVolume != null);
            pass.SetQuality(GetQualitySettings());
            pass.SetClubsBlurByStepLength(clubsBlurByStepLength);

            renderer.EnqueuePass(pass);
        }

        // Стек Volume к этому моменту уже посчитан под текущую камеру — URP обновляет его
        // раньше, чем спрашивает фичи. Поэтому берём готовый ответ, а не ищем Volume сами.
        // Берём и неактивный: без общего тумана могут быть области.
        private HeightFogVolume GetSettings()
        {
            var stack = VolumeManager.instance?.stack;

            return stack?.GetComponent<HeightFogVolume>();
        }

        private HeightFogQualitySettings GetQualitySettings()
        {
            if (quality != HeightFogQuality.Custom)
                return HeightFogQualitySettings.ForPreset(quality);

            return new HeightFogQualitySettings(downscale, stepCount, maxLamps, lampStepCount, sunShadows, lampShadows);
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

            // Текстура держится здесь, а не в проходе: она одна на все камеры — раскладывать
            // её по блоку свойств на каждый проход незачем.
            if (noiseVolume != null)
            {
                material.SetTexture(NoiseVolumeId, noiseVolume);
                material.SetFloat(NoiseTexelsId, noiseVolume.width);
            }

            return true;
        }
    }
}
