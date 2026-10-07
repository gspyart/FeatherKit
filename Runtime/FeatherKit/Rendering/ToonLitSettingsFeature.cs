using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Проектные настройки тун-шейдеров FeatherKit (Toon Lit и Toon Terrain). Здесь живёт только то,
    /// что обязано быть одинаковым у всех материалов сразу: падающие тени вместе с их оттенком, общий
    /// выключатель обводки, её опорное разрешение, потолок толщины на отлёте и поправка на то,
    /// насколько далеко стоит камера этого рендерера. Вид отдельной
    /// поверхности — ступени света, блик, цвет и толщина контура — остаётся в материале. Там же
    /// остаётся прозрачность падающей тени: насколько она видна на конкретном объекте.
    /// </summary>
    // Своего прохода отрисовки у фичи нет, и это не недоделка: она раскладывает несколько чисел
    // по глобальным значениям шейдера. Раскладываются они перед каждой камерой, поэтому
    // ни отдельный проход, ни рендер-граф им не нужны — хватает пары вызовов перед кадром.
    //
    // Метка _FeatherToonReady — не перестраховка. Глобальное значение, которое никто не выставил,
    // равно нулю, а нулевой порог тени отдал бы весь свет и падающие тени исчезли бы совсем.
    // Пока метки нет, шейдер берёт свои значения по умолчанию и выглядит нормально даже
    // в проекте, где эту фичу к рендереру не добавили.
    [DisallowMultipleRendererFeature("Toon Lit Settings")]
    public class ToonLitSettingsFeature : ScriptableRendererFeature
    {
        private static readonly int ShadowId = Shader.PropertyToID("_FeatherToonShadow");
        private static readonly int ShadowTintId = Shader.PropertyToID("_FeatherToonShadowTint");
        private static readonly int ShadowDarknessId = Shader.PropertyToID("_FeatherToonShadowDarkness");
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_FeatherToonOutlineEnabled");
        private static readonly int OutlineHeightId = Shader.PropertyToID("_FeatherToonOutlineHeight");
        private static readonly int OutlineDistanceId = Shader.PropertyToID("_FeatherToonOutlineDistance");
        private static readonly int OutlineDistanceScaleId = Shader.PropertyToID("_FeatherToonOutlineDistanceScale");
        private static readonly int ReadyId = Shader.PropertyToID("_FeatherToonReady");

        // ── Падающие тени ─────────────────────────────────────────────────────
        [Header("Падающие тени")]

        [Tooltip("Насколько тени видны. Ноль — падающих теней у тун-материалов нет, остаётся " +
                 "только затенение по углу к источнику")]
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;

        [Tooltip("Оттенок тени, общий на всю игру. В туне тень задаётся цветом, а не темнотой — " +
                 "иначе картинка выцветает в серое. Альфа — сила подмешивания, белый цвет значит " +
                 "«оттенка нет». Материал может добавить к общему тону свой (Shadow Tint, по " +
                 "умолчанию белый): они умножаются, поэтому ржавый сарай остаётся ржавым " +
                 "и в общей синеватой тени")]
        [SerializeField] private Color shadowTint = Color.white;

        [Tooltip("Насколько тень темнит поверхность под собой. Ноль — тень только уводит цвет " +
                 "в оттенок из материала; при ярком окружающем свете этого мало, и тень выходит " +
                 "почти такой же светлой, как свет. Здесь и добавляется глубина: 0.2 даёт контраст " +
                 "примерно как у обычного Lit, 0.5 — заметно резче")]
        [SerializeField, Range(0f, 1f)] private float shadowDarkness = 0f;

        [Tooltip("Где проходит край тени. Мягкие тени URP приходят усреднёнными, и при низком " +
                 "пороге тонкая тень считается светом и пропадает. Выше порог — тень шире")]
        [SerializeField, Range(0f, 1f)] private float shadowThreshold = 0.9f;

        [Tooltip("Характер края, долей от возможного. Ноль — рубленый край, ради которого тун " +
                 "и берут. Единица — самый плавный переход, какой влезает при этом пороге: край " +
                 "тени размывается так же, как у обычного Lit. Полоса перехода за края не " +
                 "вылезает, поэтому любое сочетание с порогом рабочее")]
        [SerializeField, Range(0f, 1f)] private float shadowSoftness = 0.5f;

        [Tooltip("Одна выборка карты теней вместо усреднения по фильтру: край идёт по текселям " +
                 "карты, со ступеньками — вид Lit с жёсткими тенями. Тот же вид даёт Shadow Type = " +
                 "Hard у самого источника света, но там он достанется сразу всем шейдерам")]
        [SerializeField] private bool shadowHardEdge;

        // ── Обводка ───────────────────────────────────────────────────────────
        [Header("Обводка")]

        [Tooltip("Снятая галка гасит контур у всех материалов разом, не трогая их настройки. " +
                 "Проход при этом всё равно вызывается — отменить его может только сам материал, — " +
                 "но оболочка схлопывается в точку и ни один пиксель не считается")]
        [SerializeField] private bool outlineEnabled = true;

        [Tooltip("Высота кадра, для которой в материалах задана толщина в пикселях. Толщина " +
                 "пересчитывается на текущее разрешение, поэтому контур занимает одну и ту же " +
                 "долю экрана и на 1080p, и на 4K")]
        [SerializeField] private float outlineReferenceHeight = 1080f;

        [Tooltip("Потолок толщины: до этой дистанции (м) контур ровно такой, как задан в материале, " +
                 "а дальше худеет вместе с объектом — как будто задан в метрах. Постоянная толщина " +
                 "в пикселях на отлёте выглядит жирной: силуэт усох, а контур нет. Ноль — потолка " +
                 "нет, толщина всегда ровно из материала")]
        [SerializeField] private float outlineFullWidthDistance = 12f;

        [Tooltip("Во сколько раз камера этого рендерера дальше обычной. Затухание в материалах " +
                 "и потолок толщины заданы в метрах под камеру рейда, и у отведённой далеко " +
                 "обзорной камеры — карта, мета — контур пропадает весь разом. Пятёрка значит " +
                 "«считать её впятеро ближе»: те же настройки материалов начинают работать " +
                 "на впятеро большей дистанции. Единица — ничего не меняем")]
        [SerializeField] private float outlineDistanceScale = 1f;


        public override void Create()
        {
            Push();
        }

        // Вызывается на каждую камеру каждый кадр. Выставляем и здесь, а не только в Create:
        // у разных ассетов рендерера свои настройки, и выставить их должен тот, кто рисует.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Push();
        }


        private void Push()
        {
            var shadow = new Vector4(shadowThreshold, shadowSoftness, shadowStrength, shadowHardEdge ? 1f : 0f);

            Shader.SetGlobalVector(ShadowId, shadow);
            Shader.SetGlobalColor(ShadowTintId, ToShaderColor(shadowTint));
            Shader.SetGlobalFloat(ShadowDarknessId, shadowDarkness);
            Shader.SetGlobalFloat(OutlineEnabledId, outlineEnabled ? 1f : 0f);
            Shader.SetGlobalFloat(OutlineHeightId, Mathf.Max(outlineReferenceHeight, 1f));
            Shader.SetGlobalFloat(OutlineDistanceId, Mathf.Max(outlineFullWidthDistance, 0f));
            Shader.SetGlobalFloat(OutlineDistanceScaleId, Mathf.Max(outlineDistanceScale, 0.001f));
            Shader.SetGlobalFloat(ReadyId, 1f);
        }

        // Цвет из инспектора — в то, что ждёт шейдер. Свойства материала Unity переводит
        // в линейное пространство сама, а глобальные значения отдаёт как есть, и оттенок
        // оказался бы заметно светлее выбранного на палитре. Альфа — сила подмешивания,
        // не цвет, и перевода не требует: Color.linear её не трогает.
        private static Color ToShaderColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }
    }
}
