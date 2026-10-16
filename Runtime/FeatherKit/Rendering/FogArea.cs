using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Форма области тумана.
    /// </summary>
    public enum FogAreaShape
    {
        Box = 0,
        Sphere = 1,
    }

    /// <summary>
    /// Как область тумана обходится с общим туманом внутри себя.
    /// </summary>
    public enum FogAreaBlendMode
    {
        /// <summary>Свой туман поверх общего.</summary>
        Add = 0,

        /// <summary>Внутри только свой туман, общего нет.</summary>
        Replace = 1,

        /// <summary>Внутри тумана нет вовсе: общий убран, своего нет.</summary>
        Clear = 2,
    }

    /// <summary>
    /// Область тумана: коробка или шар, внутри которых стоит свой туман — в комнате, в яме,
    /// у входа в подвал. Туман стоит на месте и виден со стороны, в отличие от локального
    /// Volume, который меняет туман, только когда в него входит камера. Отвечает ТОЛЬКО
    /// за то, где область лежит и какой в ней туман; рисует её <see cref="HeightFogFeature"/>
    /// вместе с общим туманом.
    ///
    /// Настройки у области все свои — от общего тумана в Volume она не берёт ничего.
    /// Общее только качество, оно в <see cref="HeightFogFeature"/>.
    ///
    /// Размер и поворот — от объекта: двигаешь, крутишь и масштабируешь его как обычно.
    /// Областей в кадре нет или они дальше затухания — они ничего не стоят.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("FeatherKit/Fog Area")]
    public sealed class FogArea : MonoBehaviour
    {
        private static readonly List<FogArea> EnabledAreas = new List<FogArea>();

        [Header("Форма")]
        [Tooltip("Форма области. Коробка — для комнат и коридоров, шар — для облака " +
                 "в открытом месте")]
        [SerializeField] private FogAreaShape shape = FogAreaShape.Box;

        [Tooltip("Размер области в метрах, вдобавок к масштабу объекта. У шара — " +
                 "поперечник по каждой оси")]
        [SerializeField] private Vector3 size = new Vector3(4f, 3f, 4f);

        [Tooltip("Мягкость краёв: какая доля области от края к середине плавно густеет. " +
                 "Ноль — резкая стенка тумана, единица — густо только в самой середине")]
        [SerializeField, Range(0f, 1f)] private float edgeSoftness = 0.3f;

        [Tooltip("Что делать с общим туманом внутри области. Add — свой туман поверх общего. " +
                 "Replace — внутри только свой туман: общий, пришедший снаружи, убран. " +
                 "Clear — внутри тумана нет совсем, например чистая комната посреди тумана")]
        [SerializeField] private FogAreaBlendMode blendMode = FogAreaBlendMode.Add;

        [Header("Густота и цвет")]
        [Tooltip("Цвет тумана в области. Со светом сцены — цвет самих капель, " +
                 "без него — как виден на экране")]
        [SerializeField] private Color color = new Color(0.72f, 0.76f, 0.82f);

        [Tooltip("Насколько густой туман, в метрах: сквозь столько метров тумана видно " +
                 "только треть того, что за ним. Меньше — гуще")]
        [SerializeField, Min(0.1f)] private float visibilityDistance = 6f;

        [Tooltip("Насколько туман закрывает то, что за ним. Единица — как настоящий туман. " +
                 "Ноль — ничего не закрывает, только светится там, где на него падает свет. " +
                 "Вместе с нулевой подсветкой (Ambient Strength) — объёмные лучи без пелены")]
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;

        [Tooltip("Туман лежит у пола: на этой высоте над низом области, в метрах, он " +
                 "редеет примерно втрое. Ноль — туман ровный по всей высоте")]
        [SerializeField, Min(0f)] private float floorLayerThickness;

        [Header("Клубы")]
        [Tooltip("Насколько туман клубится. Ноль — ровная заливка, единица — густота " +
                 "гуляет от нуля до двойной")]
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.45f;

        [Tooltip("Размер клуба в метрах")]
        [SerializeField, Min(0.25f)] private float spotSize = 3f;

        [Tooltip("Насколько клубы меняют яркость тумана. Плюс — густые клубы светлее, " +
                 "минус — темнее, ноль — яркость ровная")]
        [SerializeField, Range(-1f, 1f)] private float noiseBrightness;

        [Tooltip("Мелкие клубы поверх крупных: туман рваный, а не ватный. Ноль — их нет " +
                 "и они ничего не стоят. Работают вместе с крупными: при нулевой силе " +
                 "клубов густоту не меняют")]
        [SerializeField, Range(0f, 1f)] private float detailStrength;

        [Tooltip("Размер мелкого клуба в метрах. Раза в три-пять меньше крупного")]
        [SerializeField, Min(0.1f)] private float detailSize = 0.8f;

        [Tooltip("Куда и с какой скоростью плывут клубы, метров в секунду")]
        [SerializeField] private Vector3 wind = new Vector3(0.2f, 0.05f, 0.1f);

        [Header("Освещение")]
        [Tooltip("Насколько туман освещается сценой. Ноль — светится своим цветом. " +
                 "Единица — светлеет и темнеет вместе со сценой: от общей подсветки, " +
                 "солнца и ламп")]
        [SerializeField, Range(0f, 1f)] private float lightResponse = 1f;

        [Tooltip("Насколько туман подсвечен общей подсветкой сцены. Единица — как стены. " +
                 "Ноль — туман тёмный и виден только там, где его освещают солнце и лампы")]
        [SerializeField, Min(0f)] private float ambientStrength = 1f;

        [Tooltip("Насколько туман ярче, если смотреть в сторону солнца или лампы. Ноль — " +
                 "одинаково со всех сторон, настоящая дымка — около 0.5–0.7")]
        [SerializeField, Range(0f, 0.9f)] private float glowTowardLight = 0.5f;

        [Tooltip("Насколько сильно лампы светят в тумане области. Ноль — не светят и ничего " +
                 "не стоят, единица — как в жизни")]
        [SerializeField, Min(0f)] private float lampStrength = 1f;

        [Header("Затухание вдали")]
        [Tooltip("С какого расстояния от камеры область начинает таять, метров")]
        [SerializeField, Min(0f)] private float fadeStartDistance = 60f;

        [Tooltip("С какого расстояния области не видно совсем, метров. Дальше она не " +
                 "считается вовсе и ничего не стоит")]
        [SerializeField, Min(0f)] private float fadeEndDistance = 80f;


        // ── Включённые области ────────────────────────────────────────────────

        /// <summary>
        /// Все включённые области во всех открытых сценах. Порядок не значит ничего.
        /// </summary>
        public static IReadOnlyList<FogArea> Enabled => EnabledAreas;


        // ── Настройки ─────────────────────────────────────────────────────────

        public FogAreaShape Shape => shape;
        public float EdgeSoftness => edgeSoftness;
        public FogAreaBlendMode BlendMode => blendMode;

        public Color Color => color;
        public float VisibilityDistance => visibilityDistance;
        public float Opacity => opacity;
        public float FloorLayerThickness => floorLayerThickness;

        public float NoiseStrength => noiseStrength;
        public float SpotSize => spotSize;
        public float NoiseBrightness => noiseBrightness;
        public float DetailStrength => detailStrength;
        public float DetailSize => detailSize;
        public Vector3 Wind => wind;

        public float LightResponse => lightResponse;
        public float AmbientStrength => ambientStrength;
        public float GlowTowardLight => glowTowardLight;
        public float LampStrength => lampStrength;

        public float FadeStartDistance => fadeStartDistance;
        public float FadeEndDistance => fadeEndDistance;

        /// <summary>
        /// Есть ли в области свой туман. В режиме Clear своего нет — только убирает общий.
        /// </summary>
        public bool HasOwnFog => blendMode != FogAreaBlendMode.Clear;


        // ── Положение ─────────────────────────────────────────────────────────

        /// <summary>
        /// Из мира — внутрь области, где она занимает куб или шар от −0.5 до 0.5.
        /// </summary>
        public Matrix4x4 WorldToArea => AreaToWorld.inverse;

        /// <summary>
        /// Высота области в мире, метров, — вдоль её собственной оси «вверх».
        /// </summary>
        public float WorldHeight => ((Vector3)AreaToWorld.GetColumn(1)).magnitude;

        /// <summary>
        /// Есть ли у области объём. Сжатая в ноль (размер или масштаб — ноль, например
        /// в анимации появления) не обращается внутрь, и шейдер залил бы туманом весь кадр.
        /// </summary>
        public bool HasVolume => Mathf.Abs(AreaToWorld.determinant) > 1e-6f;

        /// <summary>
        /// Коробка в мире, в которую область целиком помещается. По ней область
        /// отсеивается, если её не видно в кадре.
        /// </summary>
        public Bounds WorldBounds
        {
            get
            {
                // Полуразмеры повёрнутой коробки по осям мира — сумма модулей её осей.
                var areaToWorld = AreaToWorld;
                var halfX = Abs(areaToWorld.GetColumn(0)) * 0.5f;
                var halfY = Abs(areaToWorld.GetColumn(1)) * 0.5f;
                var halfZ = Abs(areaToWorld.GetColumn(2)) * 0.5f;

                return new Bounds(areaToWorld.GetColumn(3), (halfX + halfY + halfZ) * 2f);
            }
        }

        private Matrix4x4 AreaToWorld => transform.localToWorldMatrix * Matrix4x4.Scale(size);


        // ── Затухание ─────────────────────────────────────────────────────────

        /// <summary>
        /// Насколько область видна с этого расстояния: единица — целиком, ноль — растаяла.
        /// </summary>
        public float CalculateFade(float distance)
        {
            if (fadeEndDistance <= fadeStartDistance)
                return distance <= fadeEndDistance ? 1f : 0f;

            return 1f - Mathf.InverseLerp(fadeStartDistance, fadeEndDistance, distance);
        }


        // ── Помощники ─────────────────────────────────────────────────────────

        private static Vector3 Abs(Vector4 axis)
        {
            return new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        }


        // ── Unity-события ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            EnabledAreas.Add(this);
        }

        private void OnDisable()
        {
            EnabledAreas.Remove(this);
        }

        private void OnValidate()
        {
            fadeEndDistance = Mathf.Max(fadeEndDistance, fadeStartDistance);
        }

        // Рамка видна всегда, а не только у выделенной: иначе область в сцене не найти.
        // Чистая область — тусклой белой рамкой, чтобы не путать её с туманной.
        private void OnDrawGizmos()
        {
            Gizmos.color = HasOwnFog ? new Color(color.r, color.g, color.b, 0.8f) : new Color(1f, 1f, 1f, 0.35f);
            Gizmos.matrix = AreaToWorld;

            if (shape == FogAreaShape.Sphere)
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            else
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
