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
// У КАЖДОЙ ЛАМПЫ СВОИ ШАГИ, ГУЩЕ У НЕЁ. Свет лампы падает с квадратом расстояния: почти
// весь ореол — в паре метров от неё. Ровные шаги тратились бы на тусклые края, а в ярком
// центре оставляли бы штриховку. Поэтому шаги ставятся по равным УГЛАМ, под которыми их
// видно из лампы, — у лампы часто, вдали редко (так делают в кино-рендерах: equi-angular
// sampling). Свои шаги у каждой лампы на луче, и каждый шаг считает свет только своей
// лампы. Раньше шаги сгущались у одной, самой яркой лампы луча, а соседней доставались
// редкие ровные: где главной становилась дальняя лампа, свет ближней пропадал, и вокруг
// дальней виднелся тёмный рябой купол.
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

// Больше стольких шагов у одной лампы не бывает: счётчики шагов ламп сложены по четыре бита.
#define HEIGHT_FOG_MAX_STEPS_PER_LAMP 15

// Короче этого шаг лампы не делаем, метров: короткому куску луча через свет лампы — меньше шагов.
#define HEIGHT_FOG_SHORTEST_LAMP_STEP 0.25

float4 _HeightFogAmbient;       // xyz общая подсветка, в среднем со всех сторон
float4 _HeightFogSunColor;      // xyz цвет солнца вместе с яркостью в тумане, ноль — солнца нет
float4 _HeightFogSunDirection;  // xyz направление на солнце
float _HeightFogLampCount;
float4 _HeightFogLamps[HEIGHT_FOG_MAX_LAMPS];        // x номер лампы в данных URP, y множитель яркости, z даёт ли тень
float4 _HeightFogLampSpheres[HEIGHT_FOG_MAX_LAMPS];  // xyz где лампа, w докуда светит, м

// Шаги ламп на луче пикселя: у каждой лампы свои, и идут они вперемешку, от ближнего к дальнему.
struct LampStepsOnRay
{
    // x где на луче место, ближайшее к лампе; y как близко луч к ней подходит;
    // z под каким углом из лампы видно первый шаг; w сколько угла покрывают шаги
    float4 layouts[HEIGHT_FOG_MAX_LAMPS];
    float nextDistances[HEIGHT_FOG_MAX_LAMPS];  // где очередной шаг каждой лампы, 1e8 — шаги кончились
    uint stepCounts;                            // сколько шагов у каждой лампы, по четыре бита
    uint stepsTaken;                            // сколько уже сделано, так же
    uint mask;                                  // через свет каких ламп идёт луч
    int totalSteps;
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

// ── Шаги ламп на луче ─────────────────────────────────────────────────

#if defined(HEIGHT_FOG_HAS_LAMPS)

// Сколько шагов у лампы или сколько из них сделано — свои четыре бита у каждой лампы.
uint ReadLampStepCounter(uint packedCounters, int lamp)
{
    return (packedCounters >> (lamp * 4)) & 15u;
}

// Где на луче шаг лампы с этим номером и какую длину луча он представляет. Шаги идут по
// равным углам из лампы: место = ближайшая к лампе точка + близость · tan(угол). Длина шага —
// метры луча на его долю угла, близость / cos², только записанная через смещение, а не через
// cos: когда луч смотрит почти прямо на лампу, cos² ничтожно мал, и ограничитель занижал длину
// шага в разы — лампа за стеной выглядела на стене чёрной точкой.
float FindLampStepDistance(float4 layout, float step, float stepCount, out float stepLength)
{
    float angle = layout.z + step / stepCount * layout.w;
    float offset = layout.y * tan(angle);

    stepLength = layout.w / stepCount * (layout.y * layout.y + offset * offset) / layout.y;

    return layout.x + offset;
}

// Через свет каких ламп проходит кусок луча от start до end и где у каждой шаги. Свет
// лампы — шар её дальности: луч, который его не задевает, лампу не видит. У каждой лампы
// шаги на её собственном куске луча, не больше maxStepsPerLamp; короткому куску — меньше.
LampStepsOnRay FindLampStepsOnRay(float3 cameraPosition, float3 rayDirection, float start, float end,
                                  int maxStepsPerLamp, float jitter)
{
    LampStepsOnRay steps;
    steps.stepCounts = 0u;
    steps.stepsTaken = 0u;
    steps.mask = 0u;
    steps.totalSteps = 0;

    int lampCount = (int)_HeightFogLampCount;
    int stepLimit = clamp(maxStepsPerLamp, 2, HEIGHT_FOG_MAX_STEPS_PER_LAMP);

    // Цикл развёрнут, номера ламп в нём постоянные, а записи — выбором, а не ветками:
    // так массивы остаются в регистрах, и компилятор не спотыкается о запись по номеру.
    [unroll]
    for (int i = 0; i < HEIGHT_FOG_MAX_LAMPS; i++)
    {
        float4 sphere = _HeightFogLampSpheres[i];
        float3 toLamp = sphere.xyz - cameraPosition;
        float along = dot(toLamp, rayDirection);
        float missSquared = max(dot(toLamp, toLamp) - along * along, 0.0);
        float rangeSquared = sphere.w * sphere.w;

        float halfChord = sqrt(max(rangeSquared - missSquared, 0.0));
        float enter = max(along - halfChord, start);
        float exit = min(along + halfChord, end);
        bool isOnRay = i < lampCount && missSquared < rangeSquared && exit > enter;

        // Ближе HEIGHT_FOG_LAMP_NEAREST свет лампы не растёт, и шаги туда не сгущаем: иначе почти
        // все они легли бы в этот ровный пятачок, а на яркий хвост вокруг остался бы один — то
        // попадающий, то нет, и у самой лампы шла бы тёмная рябь.
        float nearness = max(sqrt(missSquared), HEIGHT_FOG_LAMP_NEAREST);
        float startAngle = atan((enter - along) / nearness);
        float4 layout = float4(along, nearness, startAngle, atan((exit - along) / nearness) - startAngle);
        int stepCount = isOnRay ? clamp((int)ceil((exit - enter) / HEIGHT_FOG_SHORTEST_LAMP_STEP), 2, stepLimit) : 0;
        float unusedLength;
        float firstDistance = FindLampStepDistance(layout, jitter, max(stepCount, 1), unusedLength);

        steps.layouts[i] = isOnRay ? layout : float4(0.0, 1.0, 0.0, 0.0);
        steps.nextDistances[i] = isOnRay ? firstDistance : 1e8;
        steps.stepCounts |= (uint)stepCount << (i * 4);
        steps.mask |= isOnRay ? 1u << i : 0u;
        steps.totalSteps += stepCount;
    }

    return steps;
}

// Чей шаг следующий — лампы, у которой очередной шаг ближе всех к камере; -1 — шаги кончились.
int FindNearestLampStep(LampStepsOnRay steps, out float distance)
{
    int nearest = -1;
    distance = 1e8;

    [unroll]
    for (int i = 0; i < HEIGHT_FOG_MAX_LAMPS; i++)
    {
        if (steps.nextDistances[i] < distance)
        {
            distance = steps.nextDistances[i];
            nearest = i;
        }
    }

    return nearest;
}

// Делает очередной шаг лампы: отдаёт длину луча, которую этот шаг представляет, и ставит
// следующий шаг лампы.
float TakeLampStep(inout LampStepsOnRay steps, int lamp, float jitter)
{
    // Раскладку лампы достаём выбором по постоянным номерам — так массив остаётся в регистрах.
    float4 layout = float4(0.0, 1.0, 0.0, 0.0);

    [unroll]
    for (int i = 0; i < HEIGHT_FOG_MAX_LAMPS; i++)
        layout = i == lamp ? steps.layouts[i] : layout;

    float stepCount = max(ReadLampStepCounter(steps.stepCounts, lamp), 1u);
    float taken = ReadLampStepCounter(steps.stepsTaken, lamp);
    float stepLength;
    float unusedLength;

    FindLampStepDistance(layout, taken + jitter, stepCount, stepLength);

    taken += 1.0;
    steps.stepsTaken += 1u << (lamp * 4);

    float nextDistance = taken < stepCount ? FindLampStepDistance(layout, taken + jitter, stepCount, unusedLength) : 1e8;

    [unroll]
    for (int j = 0; j < HEIGHT_FOG_MAX_LAMPS; j++)
        steps.nextDistances[j] = j == lamp ? nextDistance : steps.nextDistances[j];

    return stepLength;
}

#endif

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

// Свет одной лампы в точке — ровно тот же, что у стен, с той же тенью.
float3 SampleLampLight(float3 position, float3 rayDirection, float glow, int lamp)
{
    float4 lampSettings = _HeightFogLamps[lamp];
    int urpIndex = (int)lampSettings.x;
    Light lampLight = GetAdditionalPerObjectLight(urpIndex, position);
    float attenuation = min(lampLight.distanceAttenuation, 1.0 / (HEIGHT_FOG_LAMP_NEAREST * HEIGHT_FOG_LAMP_NEAREST));

    float3 light = 0.0;

    // Вне света лампы ни тени, ни свечения не считаем.
    UNITY_BRANCH
    if (attenuation > 0.0)
    {
        float shadow = 1.0;

        #if defined(_HEIGHT_FOG_LIT_LAMP_SHADOWS)
            UNITY_BRANCH
            if (lampSettings.z > 0.5)
            {
                // У края дальности теней тень гаснет плавно, как на стенах.
                shadow = AdditionalLightRealtimeShadow(urpIndex, position, lampLight.direction);
                shadow = lerp(shadow, 1.0, GetAdditionalLightShadowFade(position));
            }
        #endif

        light = lampLight.color * (attenuation * shadow * lampSettings.y * HeightFogScattering(dot(rayDirection, lampLight.direction), glow));
    }

    return light;
}

// Во сколько раз туман в точке ярче своего цвета — от подсветки и солнца, без ламп: у них
// свои шаги. lightingSettings: x насколько туман освещается, y свечение против света,
// w сила подсветки. С нулём «насколько освещается» — ровно единица, туман светится своим цветом.
// Без readsShadow тень солнца не читается — для шагов, которых почти не видно.
float3 ComputeFogLight(float3 position, float3 rayDirection, float4 lightingSettings, bool readsShadow)
{
    float sunScattering = HeightFogScattering(dot(rayDirection, _HeightFogSunDirection.xyz), lightingSettings.y);
    float sunShadow = 1.0;

    UNITY_BRANCH
    if (readsShadow)
        sunShadow = SampleSunShadow(position);

    float3 light = _HeightFogAmbient.rgb * lightingSettings.w + _HeightFogSunColor.rgb * (sunScattering * sunShadow);

    return lerp(1.0, light, lightingSettings.x);
}

// Свет лампы, который туман в точке отдаёт камере, — в долях своего цвета. lightingSettings:
// x насколько туман освещается, y свечение против света, z сила ламп.
float3 ComputeFogLampLight(float3 position, float3 rayDirection, float4 lightingSettings, int lamp)
{
    float strength = lightingSettings.x * lightingSettings.z;

    if (strength <= 0.0)
        return 0.0;

    return SampleLampLight(position, rayDirection, lightingSettings.y, lamp) * strength;
}

#endif
