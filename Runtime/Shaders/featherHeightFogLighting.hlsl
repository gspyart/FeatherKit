// Освещение тумана: общая подсветка, солнце и лампы — с тенями или без.
//
// ЦВЕТ ТУМАНА — КАК ЦВЕТ СТЕНЫ. Капля отражает свет, который на неё падает, только
// не в одну сторону, а во все. Поэтому видимый цвет тумана — его собственный цвет,
// умноженный на свет в этой точке: общую подсветку плюс солнце и лампы, если они
// туда достают. В тёмной сцене туман тёмный, в светлой — светлый. Подсветку можно
// убавить отдельно: без неё туман виден только в лучах солнца и ламп.
//
// СВЕТЛЕЕ ПРОТИВ СВЕТА. Мелкие капли отражают свет в основном вперёд, по ходу луча:
// сквозь туман на лампу — яркий ореол, лампа за спиной — туман тусклее. Это формула
// Хеньи — Гринстейна с одним числом «насколько вперёд»; ноль — во все стороны поровну.
//
// В ЕДИНИЦАХ СТЕН. У Unity стена, повёрнутая к свету, светится ровно цветом света.
// Туман, рассеивающий во все стороны поровну, отдаёт в каждую четверть этого — множитель π
// в формуле переводит её в эти единицы. Общая подсветка идёт со всех сторон сразу, и для
// неё туман и стена равны.
//
// ЛАМПЫ — СВОИМИ ШАГАМИ. Свет лампы занимает метры, а шаги общего тумана бывают по двадцать
// метров: лампа попадала бы между шагами, и каждый пиксель то ловил её, то нет — по туману
// шла бы точечная рябь. Поэтому свет ламп считается отдельными шагами, только на том куске
// луча, что проходит через свет ламп, а обычные шаги считают лишь подсветку и солнце.
// Шаги ламп только добавляют свет — сколько тумана впереди их закрывает, решают обычные.
//
// ШАГИ ЛАМП ГУЩЕ У ЛАМПЫ. Свет лампы падает с квадратом расстояния: почти весь ореол —
// в паре метров от неё. Ровные шаги тратились бы на тусклые края, а в ярком центре
// оставляли бы штриховку. Поэтому большая часть шагов ставится по равным УГЛАМ, под
// которыми их видно из лампы, — у лампы часто, вдали редко (так делают в кино-рендерах:
// equi-angular sampling). Сгущаются они у главной лампы луча — той, что светит на него
// ярче всех; остальная часть шагов ровная, чтобы и другие лампы на луче не остались без
// шагов. Способ один на все пиксели, поэтому на границе света соседней лампы шва нет.
//
// ОТСЕВ ЛАМП НА ПИКСЕЛЬ. Перед шагами пиксель один раз проверяет, через свет каких ламп
// проходит его луч, и запоминает их битами в маске. Лампа в соседней комнате этому
// пикселю не стоит ничего, а внутри своего света лампа читает тень, только если она есть.
//
// Ключевые слова теней URP переводятся из своих в шейдере тумана — до подключения
// библиотек URP, см. featherHeightFog.shader.
#ifndef FEATHER_HEIGHT_FOG_LIGHTING_INCLUDED
#define FEATHER_HEIGHT_FOG_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

#if defined(_HEIGHT_FOG_LIT_LAMPS) || defined(_HEIGHT_FOG_LIT_LAMP_SHADOWS)
    #define HEIGHT_FOG_HAS_LAMPS
#endif

#define HEIGHT_FOG_MAX_LAMPS 8

// Ближе этого к лампе свет в тумане не растёт, метров. По закону квадрата расстояния
// он уходил бы в бесконечность, и шаг, попавший вплотную к лампе, вспыхивал бы точкой.
#define HEIGHT_FOG_LAMP_NEAREST 0.25

// Какая доля шагов ламп сгущается у главной лампы. Остальные идут ровно — для прочих ламп.
#define HEIGHT_FOG_LAMP_FOCUS 0.8

float4 _HeightFogAmbient;       // xyz общая подсветка, в среднем со всех сторон
float4 _HeightFogSunColor;      // xyz цвет солнца вместе с яркостью в тумане, ноль — солнца нет
float4 _HeightFogSunDirection;  // xyz направление на солнце
float _HeightFogLampCount;
float4 _HeightFogLamps[HEIGHT_FOG_MAX_LAMPS];        // x номер лампы в данных URP, y множитель яркости, z даёт ли тень, w яркость целиком
float4 _HeightFogLampSpheres[HEIGHT_FOG_MAX_LAMPS];  // xyz где лампа, w докуда светит, м

// Как разложены шаги ламп на луче.
struct LampStepsLayout
{
    float spanStart;       // где на луче начинаются шаги ламп
    float spanLength;      // сколько метров луча они покрывают
    float anchorAlong;     // где на луче место, ближайшее к главной лампе
    float anchorDistance;  // как близко луч подходит к главной лампе
    float startAngle;      // под каким углом из главной лампы видно начало шагов
    float angleRange;      // сколько угла покрывают шаги
    float stepCount;
};

// ── Свечение против света ─────────────────────────────────────────────

// Какая доля света уходит в сторону камеры, уже в единицах стен. На вход — косинус угла
// между взглядом и направлением на свет (единица — смотрим прямо на свет) и то,
// насколько туман светится против света.
float HeightFogScattering(float cosAngle, float glow)
{
    float glowSquared = glow * glow;
    float spread = 1.0 + glowSquared - 2.0 * glow * cosAngle;

    return (1.0 - glowSquared) / (4.0 * spread * sqrt(spread));
}

// ── Какие лампы на луче ───────────────────────────────────────────────

// Через свет каких ламп проходит кусок луча от start до end — каждая лампа битом в маске.
// Свет лампы — шар её дальности: луч, который его не задевает, лампу не видит.
// span — кусок луча, где светит хоть одна лампа, внутри start…end: там идут шаги ламп.
// anchor — главная лампа луча, та, что светит на него ярче всех: x где на луче место,
// ближайшее к ней, y как близко луч к ней подходит.
uint FindLampsOnRay(float3 cameraPosition, float3 rayDirection, float start, float end, out float2 span, out float2 anchor)
{
    uint lampMask = 0u;
    float anchorImportance = 0.0;
    span = float2(1e7, 0.0);
    anchor = float2(0.0, 1.0);

    #if defined(HEIGHT_FOG_HAS_LAMPS)
        int lampCount = (int)_HeightFogLampCount;

        [loop]
        for (int i = 0; i < lampCount; i++)
        {
            float4 sphere = _HeightFogLampSpheres[i];
            float3 toLamp = sphere.xyz - cameraPosition;
            float along = dot(toLamp, rayDirection);
            float missSquared = max(dot(toLamp, toLamp) - along * along, 0.0);
            float rangeSquared = sphere.w * sphere.w;

            if (missSquared > rangeSquared)
                continue;

            float halfChord = sqrt(rangeSquared - missSquared);
            float enter = max(along - halfChord, start);
            float exit = min(along + halfChord, end);

            if (exit <= enter)
                continue;

            lampMask |= 1u << i;
            span = float2(min(span.x, enter), max(span.y, exit));

            // Как ярко лампа светит на луч: от её яркости и того, как близко луч к ней подходит.
            float importance = _HeightFogLamps[i].w / (missSquared + HEIGHT_FOG_LAMP_NEAREST);

            if (importance > anchorImportance)
            {
                anchorImportance = importance;
                anchor = float2(along, sqrt(missSquared));
            }
        }
    #endif

    return lampMask;
}

// ── Шаги ламп ─────────────────────────────────────────────────────────

LampStepsLayout LayOutLampSteps(float2 span, float2 anchor, int stepCount)
{
    LampStepsLayout layout;
    layout.spanStart = span.x;
    layout.spanLength = span.y - span.x;
    layout.anchorAlong = anchor.x;
    layout.anchorDistance = max(anchor.y, HEIGHT_FOG_LAMP_NEAREST * 0.2);
    layout.startAngle = atan((span.x - anchor.x) / layout.anchorDistance);
    layout.angleRange = atan((span.y - anchor.x) / layout.anchorDistance) - layout.startAngle;
    layout.stepCount = stepCount;

    return layout;
}

// Где на луче шаг ламп с этим номером и какую длину луча он представляет. Положение —
// смесь двух раскладок: по равным углам из главной лампы и ровной; обе идут от начала
// куска до конца, поэтому и смесь идёт по порядку, без перехлёстов.
float FindLampStepDistance(LampStepsLayout layout, float step, out float stepLength)
{
    float progress = step / layout.stepCount;
    float angle = layout.startAngle + progress * layout.angleRange;
    float angleCos = cos(angle);

    float focusedDistance = layout.anchorAlong + layout.anchorDistance * tan(angle);
    float evenDistance = layout.spanStart + progress * layout.spanLength;

    float focusedLength = layout.anchorDistance * layout.angleRange / max(angleCos * angleCos, 1e-4);

    stepLength = lerp(layout.spanLength, focusedLength, HEIGHT_FOG_LAMP_FOCUS) / layout.stepCount;

    return lerp(evenDistance, focusedDistance, HEIGHT_FOG_LAMP_FOCUS);
}

// ── Свет в точке ──────────────────────────────────────────────────────

// Тень солнца в точке, единица — света ничто не закрывает. Без теней выборка
// вырезается при сборке шейдера.
float SampleSunShadow(float3 position)
{
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(position));

        // У края дальности теней тень гаснет плавно, как на стенах, — иначе в тумане
        // была бы видна черта, где тени кончаются.
        return lerp(shadow, 1.0, GetMainLightShadowFade(position));
    #else
        return 1.0;
    #endif
}

// Свет ламп из маски в точке. Каждая лампа — свой свет и своя тень, ровно те же, что у стен.
float3 SampleLampsLight(float3 position, float3 rayDirection, float glow, uint lampMask)
{
    float3 light = 0.0;

    #if defined(HEIGHT_FOG_HAS_LAMPS)
        int lampCount = (int)_HeightFogLampCount;

        [loop]
        for (int i = 0; i < lampCount; i++)
        {
            if ((lampMask & (1u << i)) == 0u)
                continue;

            float4 lamp = _HeightFogLamps[i];
            int urpIndex = (int)lamp.x;
            Light lampLight = GetAdditionalPerObjectLight(urpIndex, position);
            float attenuation = min(lampLight.distanceAttenuation, 1.0 / (HEIGHT_FOG_LAMP_NEAREST * HEIGHT_FOG_LAMP_NEAREST));

            // Вне света лампы ни тени, ни свечения не считаем.
            UNITY_BRANCH
            if (attenuation <= 0.0)
                continue;

            float shadow = 1.0;

            #if defined(_HEIGHT_FOG_LIT_LAMP_SHADOWS)
                UNITY_BRANCH
                if (lamp.z > 0.5)
                    shadow = AdditionalLightRealtimeShadow(urpIndex, position, lampLight.direction);
            #endif

            light += lampLight.color * (attenuation * shadow * lamp.y * HeightFogScattering(dot(rayDirection, lampLight.direction), glow));
        }
    #endif

    return light;
}

// Во сколько раз туман в точке ярче своего цвета — от подсветки и солнца, без ламп: у них
// свои шаги. lightingSettings: x насколько туман освещается, y свечение против света,
// w сила подсветки. С нулём «насколько освещается» — ровно единица, туман светится своим цветом.
float3 ComputeFogLight(float3 position, float3 rayDirection, float4 lightingSettings)
{
    float sunScattering = HeightFogScattering(dot(rayDirection, _HeightFogSunDirection.xyz), lightingSettings.y);
    float3 light = _HeightFogAmbient.rgb * lightingSettings.w + _HeightFogSunColor.rgb * (sunScattering * SampleSunShadow(position));

    return lerp(1.0, light, lightingSettings.x);
}

// Свет ламп, который туман в точке отдаёт камере, — в долях своего цвета. lightingSettings:
// x насколько туман освещается, y свечение против света, z сила ламп.
float3 ComputeFogLampLight(float3 position, float3 rayDirection, float4 lightingSettings, uint lampMask)
{
    float strength = lightingSettings.x * lightingSettings.z;

    if (strength <= 0.0)
        return 0.0;

    return SampleLampsLight(position, rayDirection, lightingSettings.y, lampMask) * strength;
}

#endif
