#ifndef FEATHER_TOON_LIGHTING_INCLUDED
#define FEATHER_TOON_LIGHTING_INCLUDED

// ═══════════════════════════════════════════════════════════════════════════════════════
// Общий тун-свет: ступенчатое освещение, падающая тень, блик и контровой ободок.
// Отсюда его берут все шейдеры кита, которым нужен один и тот же вид: заливка персонажа
// и слоистая земля должны быть освещены одинаково, иначе стык модели с землёй виден.
//
// ЧЕГО ЗДЕСЬ НЕТ. Всё, что относится к конкретной поверхности: текстуры, обводка,
// вспышка урона, прозрачность. Здесь только свет.
//
// ТРЕБОВАНИЯ К ТОМУ, КТО ВКЛЮЧАЕТ. Файл читает свойства материала по именам, поэтому:
//   1. включать ПОСЛЕ CBUFFER_END, иначе имён ещё не существует;
//   2. в CBUFFER(UnityPerMaterial) должны лежать все тринадцать:
//      _ShadowTint, _CastShadowOpacity,
//      _RampThreshold, _RampSmoothness, _MidToneWidth, _MidToneStrength,
//      _ToonSpecColor, _SpecGloss, _SpecIntensity,
//      _RimColor, _RimAmount, _RimThreshold, _RimSmoothness.
// Забытое имя — ошибка компиляции с этим именем в тексте, а не тихая поломка.
//
// ОТТЕНОК ТЕНИ СКЛАДЫВАЕТСЯ ИЗ ДВУХ. Общий на проект приходит из рендер-фичи, свой
// у материала лежит в _ShadowTint и по умолчанию белый. Они УМНОЖАЮТСЯ, а не подменяют
// друг друга: один раз выставленный тон держит всю сцену в одном ключе, а материалу
// остаётся право добавить к нему своё — ржавчину, зелень подлеска, — не отменяя общего.
//
// ПРОЗРАЧНОСТЬ ПАДАЮЩЕЙ ТЕНИ (_CastShadowOpacity) — наоборот, только у материала. Сила,
// край и глубина тени одни на проект: тень принадлежит сцене. А «на этой поверхности
// тень слабее» — свойство самой поверхности, и общей настройкой его не выразить.
//
// НОВОЕ СВОЙСТВО СВЕТА добавляется в каждый шейдер, который сюда включается: в Properties
// и в CBUFFER. Список выше — тот самый договор, его и надо править вместе с кодом.
// ═══════════════════════════════════════════════════════════════════════════════════════

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ── Проектные настройки: приходят из рендер-фичи ──────────────────────
// Вне UnityPerMaterial: это глобальные значения, у материала их нет. В CBUFFER
// материала им нельзя — там только свойства материала, чужое ломает SRP Batcher.
float4 _FeatherToonShadow;         // x порог, y мягкость (доля), z сила, w жёсткий край
float4 _FeatherToonShadowTint;     // общий оттенок тени: rgb цвет, w сила подмешивания
float _FeatherToonShadowDarkness;  // насколько падающая тень темнит поверхность
float _FeatherToonOutlineEnabled;  // 0 — обводки нет нигде в проекте
float _FeatherToonOutlineHeight;   // опорная высота кадра для толщины обводки
float _FeatherToonOutlineDistance; // до этой дистанции толщина полная, 0 — без ограничения
float _FeatherToonOutlineDistanceScale; // во сколько раз камера дальше обычной: делит её дистанцию
float _FeatherToonReady;           // 1 — настройки выставлены, иначе брать их нельзя

struct ToonShadowSettings
{
    half threshold;
    half softness;
    half strength;
    half hardEdge;
    half darkness;
    half3 tint;     // общий оттенок из фичи, уже приведённый к множителю
};


// ── Падающая тень ────────────────────────────────────────────────────

// Мягкость приходит долей, а не готовой шириной. Полоса перехода не должна выходить
// за 0..1: при пороге 0.9 и ширине 0.5 она стала бы 0.4…1.4, и даже полностью
// освещённый пиксель оказался бы на треть в тени. Доля берёт то, что влезает:
// единица — самый плавный переход, возможный при этом пороге, то есть вид Lit.
half ToonShadowSoftness(half threshold, half fraction)
{
    half room = min(threshold, 1.0h - threshold);

    return max(fraction * room, 0.001h);
}

// Цвет с силой в альфе — в обычный множитель. Белый значит «оттенка нет», поэтому
// не выставленное глобальное значение — одни нули, альфа в том числе — само выходит
// нейтральным и молча ничего не красит.
half3 ToonTintFactor(half4 tint)
{
    return lerp(half3(1.0h, 1.0h, 1.0h), tint.rgb, tint.a);
}

// Без фичи в рендерере глобальные значения — нули, а нулевой порог отдал бы весь свет:
// падающие тени исчезли бы молча. Поэтому без метки берём свои значения по умолчанию.
ToonShadowSettings GetToonShadowSettings()
{
    ToonShadowSettings settings;

    if (_FeatherToonReady > 0.5)
    {
        settings.threshold = _FeatherToonShadow.x;
        settings.softness = ToonShadowSoftness(_FeatherToonShadow.x, _FeatherToonShadow.y);
        settings.strength = _FeatherToonShadow.z;
        settings.hardEdge = _FeatherToonShadow.w;
        settings.darkness = _FeatherToonShadowDarkness;
        settings.tint = ToonTintFactor(_FeatherToonShadowTint);
    }
    else
    {
        settings.threshold = 0.9h;
        settings.softness = ToonShadowSoftness(0.9h, 0.5h);
        settings.strength = 1.0h;
        settings.hardEdge = 0.0h;
        settings.darkness = 0.0h;
        settings.tint = half3(1.0h, 1.0h, 1.0h);
    }

    // Прозрачность — единственное, что в падающей тени осталось за материалом. Ослабляем
    // ею и ступеньку, и затемнение: иначе тень пропадала бы, а её след продолжал темнить.
    settings.strength *= _CastShadowOpacity;
    settings.darkness *= _CastShadowOpacity;

    return settings;
}

// Затухание от падающей тени главного света. Считаем сами, а не берём готовое
// GetMainLight(shadowCoord), по двум причинам: жёсткий край — это одна выборка вместо
// усреднения по фильтру, и на границе дистанции теней нужен плавный сход, которого
// в короткой версии URP нет — там тень обрубается по прямой.
// Запечённые тени (shadow mask) не учитываем: лайтмапов в проекте нет.
half ToonMainLightShadow(float4 shadowCoord, float3 positionWS, ToonShadowSettings settings)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return 1.0h;
#else
    half attenuation;

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    // Экранные тени считает отдельный проход, влезать в его фильтр нечем.
    attenuation = MainLightRealtimeShadow(shadowCoord);
#else
    if (settings.hardEdge > 0.5h)
    {
        attenuation = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,
                                              sampler_LinearClampCompare, shadowCoord.xyz);
        attenuation = LerpWhiteTo(attenuation, GetMainLightShadowParams().x);
        attenuation = BEYOND_SHADOW_FAR(shadowCoord) ? 1.0h : attenuation;
    }
    else
    {
        attenuation = MainLightRealtimeShadow(shadowCoord);
    }
#endif

    return lerp(attenuation, 1.0h, GetMainLightShadowFade(positionWS));
#endif
}

// Ступенька падающей тени. Сила — множителем поверх ступеньки: ноль означает
// «падающих теней нет», а не «всё в тени».
half ToonShade(half shadowAttenuation, ToonShadowSettings settings)
{
    half shade = smoothstep(settings.threshold - settings.softness,
                            settings.threshold + settings.softness, shadowAttenuation);

    return lerp(1.0h, shade, settings.strength);
}

// Насколько падающая тень темнит то, что под ней. Отдельно от оттенка: оттенок
// задаёт ЦВЕТ неосвещённой стороны, а это — глубину именно падающей тени. Без него
// при ярком окружающем свете тень выходит почти такой же светлой, как свет: её
// яркость берётся от ambient, а не от доли солнца, как у Lit.
half ToonShadowDarkening(half shade, ToonShadowSettings settings)
{
    return lerp(1.0h - settings.darkness, 1.0h, shade);
}

// Оттенок тени: общий из фичи, помноженный на свой у материала. Умножением и с силой
// в альфе, а не подменой цвета. Яркость тени остаётся за окружающим светом сцены — если
// она не меняется, смотри Environment Lighting: с плоским цветом вместо скайбокса
// окружающий свет одинаков везде.
half3 ToonShadowTint(ToonShadowSettings settings)
{
    return settings.tint * ToonTintFactor(_ShadowTint);
}


// ── Ступени света ────────────────────────────────────────────────────

// Ступенька света с мягким краем. Отдельная средняя ступень (_MidToneWidth)
// нужна, чтобы силуэт не разваливался на два плоских пятна — именно она даёт
// ту самую «пластилиновую» объёмность, ради которой этот стиль и берут.
half ToonRamp(half ndotl, half shade, half distanceAttenuation)
{
    half core = smoothstep(_RampThreshold - _RampSmoothness,
                           _RampThreshold + _RampSmoothness, ndotl);

    half mid = smoothstep(_RampThreshold - _RampSmoothness - _MidToneWidth,
                          _RampThreshold + _RampSmoothness - _MidToneWidth, ndotl);

    half ramp = saturate(core + mid * _MidToneStrength * (1.0 - core));

    // Тень — отдельной ступенькой, а НЕ через ndotl * attenuation. В тени URP
    // отдаёт не ноль, а (1 - shadowStrength): у горизонтальной земли ndotl≈0.77,
    // и произведение 0.77*0.25 остаётся выше порога — тень проглатывается
    // целиком. Отдельный порог по затуханию от этого не зависит.
    // Затухание по дальности — множителем, без ступеньки: точечный свет должен
    // угасать плавно, иначе он превращается в диск с рубленым краем.
    return ramp * shade * distanceAttenuation;
}

half ToonLightRamp(Light light, half3 normalWS, ToonShadowSettings settings)
{
    half ndotl = dot(normalWS, light.direction);
    half shade = ToonShade(light.shadowAttenuation, settings);

    return ToonRamp(ndotl, shade, light.distanceAttenuation);
}

half3 ToonSpecular(Light light, half3 normalWS, half3 viewDirWS, half ramp)
{
    if (_SpecIntensity <= 0.0)
        return 0;

    half3 halfVector = SafeNormalize(light.direction + viewDirWS);
    half ndoth = saturate(dot(normalWS, halfVector));
    half specRaw = pow(ndoth, _SpecGloss);
    half specStep = smoothstep(0.45, 0.55, specRaw) * ramp;

    return specStep * _SpecIntensity * _ToonSpecColor.rgb * light.color;
}


// ── Сколько света на самом деле ──────────────────────────────────────

// Ступени света смотрят только на его НАПРАВЛЕНИЕ, а направление есть и у света
// с нулевой интенсивностью. Пока это не учитывалось, погашенное солнце продолжало
// лепить форму: теневая сторона крашеная оттенком, освещённая чистая, и падающая
// тень темнила как ни в чём не бывало. Этим числом всё такое и глушится.
//
// Максимум по каналам, а не воспринимаемая яркость: синий фонарь — такой же свет,
// как белый, просто цветной, а Luminance списала бы ему две трети силы.
half ToonLightPresence(half3 lightColor)
{
    return saturate(Max3(lightColor.r, lightColor.g, lightColor.b));
}


// ── Свет целиком ─────────────────────────────────────────────────────

// Готовый цвет поверхности: ступени главного света, падающая тень, добавочные
// источники, блик и ободок. Своего у поверхности остаётся только альбедо —
// откуда оно взялось, из одной текстуры или из четырёх смешанных слоёв,
// свету безразлично.
//
// Окружающий свет приходит готовым, посчитанным в вершине: развёртка сферических
// гармоник это десятки операций на каждый пиксель экрана, а на модель их приходится
// столько, сколько у неё вершин.
half3 ToonLighting(half3 albedo, half3 ambient, float3 positionWS, half3 normalWS, half3 viewDirWS,
                   float2 screenUV)
{
    ToonShadowSettings shadowSettings = GetToonShadowSettings();

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight();
    mainLight.shadowAttenuation = ToonMainLightShadow(shadowCoord, positionWS, shadowSettings);

    half mainPresence = ToonLightPresence(mainLight.color);
    half mainShade = ToonShade(mainLight.shadowAttenuation, shadowSettings);
    half mainRamp = ToonRamp(dot(normalWS, mainLight.direction), mainShade,
                             mainLight.distanceAttenuation);

    // Гаснет свет — мельчает и его тень: темнить в полную силу от источника,
    // которого нет, неоткуда.
    shadowSettings.darkness *= mainPresence;

    // Окружающий свет — общий уровень, от него считается и тень, и свет.
    half3 ambientLevel = albedo * ambient;

    // Оттенок красит ТЕНЬ. На свету он не участвует, иначе светлый тон уезжал
    // бы в синеву вместе с тенью и два тона перестали бы читаться раздельно.
    // Сила оттенка идёт за светом: без света нет и стороны, которую он затеняет.
    half3 shadowSide = ambientLevel * lerp(half3(1.0h, 1.0h, 1.0h),
                                           ToonShadowTint(shadowSettings), mainPresence);
    half3 litSide = ambientLevel + albedo * mainLight.color;

    half3 color = lerp(shadowSide, litSide, mainRamp);

    // Падающая тень темнит поверх ступеней — тем и приводится к виду Lit,
    // где тень это доля от прямого света, а не отдельный светлый цвет.
    color *= ToonShadowDarkening(mainShade, shadowSettings);

    half3 specular = ToonSpecular(mainLight, normalWS, viewDirWS, mainRamp);

    #ifdef _ADDITIONAL_LIGHTS
    // Через LIGHT_LOOP_*, а не for по GetAdditionalLightsCount: под Forward+ (кластерный
    // цикл) счётчик всегда 0, источники перебираются по кластеру пикселя. Макросу нужен
    // inputData с позицией и экранной точкой, остальное ему безразлично.
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;

    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light extra = GetAdditionalLight(lightIndex, positionWS);
        half extraRamp = ToonLightRamp(extra, normalWS, shadowSettings);

        color += albedo * extra.color * extraRamp;
        specular += ToonSpecular(extra, normalWS, viewDirWS, extraRamp);
    LIGHT_LOOP_END
    #endif

    color += specular;

    // Контровой ободок по краю силуэта: отделяет персонажа от фона, когда
    // на экране толпа.
    half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
    half rim = smoothstep(_RimThreshold - _RimSmoothness,
                          _RimThreshold + _RimSmoothness, fresnel);
    // Гасим ободок в тени: подсвеченный край на неосвещённой стороне читается
    // как ошибка, а не как контровой свет. По той же причине он гаснет и вместе
    // с самим светом — контровой ободок без источника контрового света бессмыслен.
    color += rim * mainRamp * mainPresence * _RimAmount * _RimColor.rgb;

    return color;
}

#endif
