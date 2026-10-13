// Области тумана: коробки и шары, в которых стоит свой туман, — или в которых тумана нет.
//
// Изнутри области она — куб или шар от −0.5 до 0.5: матрица из мира учитывает её место,
// поворот и размер разом. Поэтому и вход луча в область, и густота в точке считаются
// для одной единичной фигуры, какой бы ни была область в мире.
//
// ОТСЕВ НА ПИКСЕЛЬ. Перед шагами пиксель один раз проверяет, какие области задевает его
// луч, и запоминает их битами в маске. На шагах он смотрит только их: комната сбоку
// от луча не стоит этому пикселю ничего, сколько бы шагов он ни делал.
//
// РЕЖИМЫ. Add — свой туман поверх общего. Replace — свой туман, а общий внутри вырезан.
// Clear — своего нет, общий вырезан: чистая комната. Вырезается только общий туман,
// другие области — нет.
#ifndef FEATHER_HEIGHT_FOG_AREAS_INCLUDED
#define FEATHER_HEIGHT_FOG_AREAS_INCLUDED

#if defined(_HEIGHT_FOG_AREAS) || defined(_HEIGHT_FOG_AREAS_CUT)
    #define HEIGHT_FOG_HAS_AREAS
#endif

#define HEIGHT_FOG_MAX_AREAS 8

// Режимы — как FogAreaBlendMode.
#define HEIGHT_FOG_AREA_REPLACE 1
#define HEIGHT_FOG_AREA_CLEAR 2

float _HeightFogAreaCount;
float4x4 _HeightFogAreaWorldToLocal[HEIGHT_FOG_MAX_AREAS];  // из мира внутрь области
float4 _HeightFogAreaColor[HEIGHT_FOG_MAX_AREAS];     // xyz цвет, w плотность на метр — уже с затуханием вдали, у чистой ноль
float4 _HeightFogAreaShape[HEIGHT_FOG_MAX_AREAS];     // x форма: 0 коробка, 1 шар; y мягкость краёв; z как быстро редеет вверх; w режим
float4 _HeightFogAreaClubs[HEIGHT_FOG_MAX_AREAS];     // x масштаб клубов, y сила в густоте, z сила в яркости, w сила мелких клубов
float4 _HeightFogAreaWind[HEIGHT_FOG_MAX_AREAS];      // xyz ветер, м/с; w масштаб мелких клубов
float4 _HeightFogAreaLighting[HEIGHT_FOG_MAX_AREAS];  // x насколько освещается, y свечение против света, z сила ламп, w сила подсветки
float4 _HeightFogAreaVisibility[HEIGHT_FOG_MAX_AREAS];  // x насколько закрывает то, что за ней, y насколько видна вдали

// Туман всех областей в одной точке.
struct FogAreasSample
{
    float density;     // густота всех областей вместе, с клубами
    float hidingDensity;  // сколько из неё закрывает кадр за собой
    float3 albedo;     // цвет — средний по областям, с весом по густоте, с яркостью клубов
    float4 lighting;   // как освещается — тоже средний: x насколько, y свечение против света, z сила ламп, w подсветки
};

// ── Фигура области ────────────────────────────────────────────────────

// Расстояния от камеры, на которых луч входит в область и выходит из неё: x вход, y выход.
// Выход не дальше входа — луч область не задевает.
float2 IntersectFogArea(int index, float3 cameraPosition, float3 rayDirection)
{
    float4x4 worldToArea = _HeightFogAreaWorldToLocal[index];
    float3 origin = mul(worldToArea, float4(cameraPosition, 1.0)).xyz;

    // Направление не нормируем: тогда расстояние вдоль луча внутри области то же, что в мире.
    float3 direction = mul((float3x3)worldToArea, rayDirection);

    if (_HeightFogAreaShape[index].x > 0.5)
    {
        // Шар: точки луча на расстоянии 0.5 от середины — корни квадратного уравнения.
        float a = dot(direction, direction);
        float b = dot(origin, direction);
        float c = dot(origin, origin) - 0.25;
        float discriminant = b * b - a * c;

        if (discriminant < 0.0)
            return float2(0.0, 0.0);

        float root = sqrt(discriminant);

        return float2((-b - root) / a, (-b + root) / a);
    }

    // Коробка: луч внутри, пока он между гранями по всем трём осям сразу.
    float3 safeDirection = abs(direction) > 1e-6 ? direction : 1e-6;
    float3 toLowerFaces = (-0.5 - origin) / safeDirection;
    float3 toUpperFaces = (0.5 - origin) / safeDirection;
    float3 enters = min(toLowerFaces, toUpperFaces);
    float3 exits = max(toLowerFaces, toUpperFaces);

    return float2(max(max(enters.x, enters.y), enters.z), min(min(exits.x, exits.y), exits.z));
}

// Насколько точка в глубине области: единица в середине, ноль на краю, снаружи — меньше нуля.
float AreaDepthInside(float3 local, float shape)
{
    if (shape > 0.5)
        return 1.0 - 2.0 * length(local);

    return 1.0 - 2.0 * max(max(abs(local.x), abs(local.y)), abs(local.z));
}

// Насколько область «включена» в точке с учётом мягких краёв: от нуля до единицы.
float AreaEdgeFade(float depthInside, float softness)
{
    return smoothstep(0.0, max(softness, 1e-3), depthInside);
}

// ── Какие области на луче ─────────────────────────────────────────────

// Какие области задевает луч — каждая битом в маске. fogMask — области со своим туманом,
// cutMask — вырезающие общий туман. span — кусок луча от входа в первую туманную область
// до выхода из последней, не дальше поверхности: за стеной туман не виден.
void FindAreasOnRay(float3 cameraPosition, float3 rayDirection, float surfaceDistance,
                    out float2 span, out uint fogMask, out uint cutMask)
{
    span = float2(1e7, 0.0);
    fogMask = 0u;
    cutMask = 0u;

    int areaCount = (int)_HeightFogAreaCount;

    [loop]
    for (int i = 0; i < areaCount; i++)
    {
        float2 hit = IntersectFogArea(i, cameraPosition, rayDirection);

        hit.x = max(hit.x, 0.0);
        hit.y = min(hit.y, surfaceDistance);

        if (hit.y <= hit.x)
            continue;

        uint bit = 1u << i;
        float mode = _HeightFogAreaShape[i].w;

        if (mode > HEIGHT_FOG_AREA_REPLACE - 0.5)
            cutMask |= bit;

        if (mode < HEIGHT_FOG_AREA_CLEAR - 0.5)
        {
            fogMask |= bit;
            span = float2(min(span.x, hit.x), max(span.y, hit.y));
        }
    }
}

// ── Туман областей в точке ────────────────────────────────────────────

// Сколько общего тумана остаётся в точке: единица — весь, ноль — вырезан областью.
// Вдали область тает — и вырезает меньше.
float SampleAreaCuts(float3 position, uint cutMask)
{
    float remaining = 1.0;
    int areaCount = (int)_HeightFogAreaCount;

    [loop]
    for (int i = 0; i < areaCount; i++)
    {
        if ((cutMask & (1u << i)) == 0u)
            continue;

        float3 local = mul(_HeightFogAreaWorldToLocal[i], float4(position, 1.0)).xyz;
        float4 shape = _HeightFogAreaShape[i];
        float depthInside = AreaDepthInside(local, shape.x);

        if (depthInside <= 0.0)
            continue;

        remaining *= 1.0 - AreaEdgeFade(depthInside, shape.y) * _HeightFogAreaVisibility[i].y;
    }

    return remaining;
}

// Туман всех областей из маски в точке. Где две области перекрываются, цвет и свет
// берутся средними с весом по густоте — туман окрашен той, что гуще.
FogAreasSample SampleFogAreas(float3 position, uint fogMask)
{
    FogAreasSample result;
    result.density = 0.0;
    result.hidingDensity = 0.0;
    result.albedo = 0.0;
    result.lighting = 0.0;

    int areaCount = (int)_HeightFogAreaCount;

    [loop]
    for (int i = 0; i < areaCount; i++)
    {
        if ((fogMask & (1u << i)) == 0u)
            continue;

        float3 local = mul(_HeightFogAreaWorldToLocal[i], float4(position, 1.0)).xyz;
        float4 shape = _HeightFogAreaShape[i];
        float depthInside = AreaDepthInside(local, shape.x);

        if (depthInside <= 0.0)
            continue;

        float4 color = _HeightFogAreaColor[i];

        // Мягкие края и слой у пола: низ области — y = −0.5.
        float density = color.w * AreaEdgeFade(depthInside, shape.y) * exp(-(local.y + 0.5) * shape.z);

        float4 clubsSettings = _HeightFogAreaClubs[i];
        float4 windSettings = _HeightFogAreaWind[i];
        float clubs = 0.0;

        // Клубы, которые ничего не меняют, не читаем.
        UNITY_BRANCH
        if (clubsSettings.y > 0.0 || clubsSettings.z != 0.0)
            clubs = SampleFogClubs(position, windSettings.xyz, clubsSettings.x, windSettings.w, clubsSettings.w);

        density *= max(1.0 + clubsSettings.y * clubs, 0.0);

        if (density <= 0.0)
            continue;

        result.density += density;
        result.hidingDensity += density * _HeightFogAreaVisibility[i].x;
        result.albedo += color.rgb * (1.0 + clubsSettings.z * clubs) * density;
        result.lighting += _HeightFogAreaLighting[i] * density;
    }

    float weight = 1.0 / max(result.density, 1e-5);

    result.albedo *= weight;
    result.lighting *= weight;

    return result;
}

#endif
