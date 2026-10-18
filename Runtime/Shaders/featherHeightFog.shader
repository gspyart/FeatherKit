// Объёмный туман одним полноэкранным проходом: общий туман по высоте и области тумана.
//
// Как это работает. Глубина кадра говорит, где стоит поверхность под каждым пикселем, —
// отсюда восстанавливается луч от камеры до неё. По лучу туман проходится ШАГАМИ: в каждой
// точке берётся густота, клубы и свет, и каждый шаг закрывает собой всё, что за ним. Так
// густой клуб впереди прячет то, что лежит дальше, и туман читается объёмом, а не узором,
// нарисованным поверх кадра.
//
// ОБЩИЙ ТУМАН — СЛОЙ ПО ВЫСОТЕ. Выше пола он редеет по экспоненте, ниже пола — ровный,
// той же густоты, что на полу. Расти вниз без предела ему нельзя: тогда пол, поднятый над
// землёй, не сдвигал бы туман, а сгущал его в разы, и ручка «где туман» работала бы как
// «сколько».
//
// КАКОЙ КУСОК ЛУЧА ШАГАЕМ В СЛОЕ. Только тот, что внутри слоя: от места, где взгляд
// опустился ниже потолка слоя, и не дальше дальности от этого места. Потолок — несколько
// толщин над полом, выше густоты почти нет, и шаги, потраченные там, пропали бы зря. Отсчёт
// дальности от входа в слой, а не от камеры, нужен для камеры, поднятой высоко над туманом:
// пустой воздух под ней дальности не съедает. Ближе дистанции начала тумана нет вовсе.
// Небо шагается тем же куском, что и земля дальше дальности, — поэтому на краю геометрии,
// где земля сменяется небом, шва нет.
//
// ОБЛАСТИ ТУМАНА шагаются своими шагами по своему куску луча — от входа в первую область
// до выхода из последней. Иначе маленькая комната на луче длиной в сотни метров попала бы
// между двумя шагами и пропала. Шаги слоя, областей и ламп идут вперемешку, от ближнего
// к дальнему, — так ближний туман закрывает дальний, чей бы он ни был. Настройки у слоя
// и у каждой области свои. Подробности — featherHeightFogAreas.hlsl.
//
// КЛУБЫ — featherHeightFogClubs.hlsl, СВЕТ И ШАГИ ЛАМП — featherHeightFogLighting.hlsl.
//
// СВЕТИТ И ЗАКРЫВАЕТ — РАЗНЫЕ РУЧКИ. Свет тумана копится как в настоящем тумане: густой
// туман впереди гасит свет того, что за ним, и яркость не растёт без предела. А сколько
// туман закрывает кадр за собой, решает отдельная ручка «насколько закрывает»: при нуле
// кадр виден весь, и туман только светится там, где на него падает свет.
//
// ВСЁ НЕОБЯЗАТЕЛЬНОЕ — ОТДЕЛЬНЫЕ ЧАСТИ ШЕЙДЕРА: клубы, мелкие клубы, области, свет, лампы,
// тени. Выключенная часть вырезается при сборке, а не пропускается на ходу, поэтому не
// стоит ничего. Части, которые без другой не нужны, сложены в один переключатель.
//
// ПОЛОСЫ ОТ ШАГОВ. Шаги на равных расстояниях дали бы слоистые полосы. Поэтому начало
// шагов у соседних пикселей сдвинуто на разную долю шага, а растяжение в проходе 1
// размывает этот сдвиг в ровную пелену — даже в полном разрешении.
//
// ЦЕНА — число шагов, на каждом шаге клубы и свет. Послабления:
//   — туман считается в своей текстуре, по желанию пониженного разрешения, и растягивается
//     поверх кадра. Туман низкочастотный, на растяжении это не читается, а пикселей
//     считается вчетверо (или в шестнадцать раз) меньше;
//   — короткому лучу — меньше шагов: до стены в трёх метрах не нужно столько же шагов,
//     сколько до горизонта;
//   — каждый пиксель заранее отбирает области и лампы, которые задевает его луч;
//   — пустые шаги между областями пропускают клубы и свет;
//   — клубы вдали и на длинных шагах читаются из уменьшенной копии текстуры, а где
//     усреднились почти в ноль — не читаются вовсе (featherHeightFogClubs.hlsl);
//   — шаги, которых уже почти не видно за туманом впереди, не освещаются;
//   — шаги кончаются раньше, когда туман уже закрыл всё: дальше смотреть не на что.
//
// Проходов два:
//   0 — посчитать туман в свою текстуру: свет тумана и сколько кадра он закрывает;
//   1 — растянуть посчитанное поверх кадра, слегка размыв, но не через края предметов.
//
// Свет в текстуре уже «сколько добавить к кадру», а не цвет тумана: билинейное растяжение
// смешивает соседние тексели, и там, где туман прозрачен, его цвет затёк бы в чистые места.
Shader "FeatherKit/Height Fog"
{
    // Свойств нет намеренно: все числа приходят из Volume и областей через блок свойств
    // прохода, и материал у эффекта служебный — руками его никто не настраивает.
    SubShader
    {
        Tags
        {
            "RenderType" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        // URP читает тени только под своими ключевыми словами. Переводим в них свои — до
        // подключения библиотек: так тени включены ровно тогда, когда их просит туман,
        // а не всякий раз, когда URP рисует тени для стен.
        #if defined(_HEIGHT_FOG_SUN_SHADOWS_CASCADE)
            #define _MAIN_LIGHT_SHADOWS_CASCADE
        #elif defined(_HEIGHT_FOG_SUN_SHADOWS)
            #define _MAIN_LIGHT_SHADOWS
        #endif

        #if defined(_HEIGHT_FOG_LIT_LAMP_SHADOWS)
            #define _ADDITIONAL_LIGHT_SHADOWS
        #endif

        #if defined(_HEIGHT_FOG_LIT) || defined(_HEIGHT_FOG_LIT_LAMPS) || defined(_HEIGHT_FOG_LIT_LAMP_SHADOWS)
            #define HEIGHT_FOG_IS_LIT
        #endif

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherHeightFogClubs.hlsl"
        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherHeightFogAreas.hlsl"

        #if defined(HEIGHT_FOG_IS_LIT)
            #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherHeightFogLighting.hlsl"
        #endif

        float4 _HeightFogColor;         // xyz цвет общего тумана, уже в линейном пространстве; w насколько закрывает то, что за ним
        float4 _HeightFogLayer;         // x плотность у пола (ноль — общего тумана нет), y уровень пола (Y в мире), z 1/толщина слоя, w дальность
        float4 _HeightFogNoise;         // x масштаб клубов, y сила в густоте, z красить ли небо, w сила в яркости
        float4 _HeightFogDetail;        // x масштаб мелких клубов, y их сила
        float4 _HeightFogWind;          // xyz куда и с какой скоростью плывут клубы, м/с
        float4 _HeightFogLighting;      // x насколько общий туман освещается, y свечение против света, z сила ламп, w сила подсветки
        float4 _HeightFogDepthTap;      // xy смещение до угла блока кадра в uv, ноль — блока нет
        float4 _HeightFogTexelSize;     // xy размер текселя своей текстуры тумана, для размытия
        float4 _HeightFogStepCounts;    // x сколько шагов по лучу — у слоя и у областей своих; y сколько шагов у ламп
        float _HeightFogStartDistance;  // ближе этого расстояния от камеры общего тумана нет, м

        // Потолок слоя — столько толщин над полом. Густота там меньше пятидесятой доли
        // от пола, и шагать выше незачем.
        #define HEIGHT_FOG_CEILING_THICKNESSES 4.0

        // Короче этого шаг не делаем, метров: луч до близкой стены получает меньше шагов.
        #define HEIGHT_FOG_SHORTEST_STEP 0.5

        // Шаги кончаются, когда сквозь туман видно меньше этой доли.
        #define HEIGHT_FOG_OPAQUE_ENOUGH 0.005

        // Шаг, который добавляет к кадру меньше этой доли, не читает тень — её на нём не видно.
        #define HEIGHT_FOG_INVISIBLE_STEP 0.001

        // Туман одного шага: где он, сколько его и какой он.
        struct FogStepSample
        {
            float3 position;
            float stepLength;
            float density;        // сколько тумана — от неё и свет, и то, как туман гасит свой же свет
            float hidingDensity;  // сколько из него закрывает кадр за собой
            float3 albedo;
            float4 lighting;   // x насколько освещается, y свечение против света, z сила ламп, w подсветки
        };

        // ── Общий туман: густота в точке ──────────────────────────────────

        // Густота по высоте: ровная ниже пола, редеет по экспоненте выше.
        float FogHeightDensity(float height)
        {
            float aboveFloor = max(height - _HeightFogLayer.y, 0.0);

            return _HeightFogLayer.x * exp(-aboveFloor * _HeightFogLayer.z);
        }

        // Общий туман в точке: густота по высоте, вырезы областей, клубы.
        FogStepSample SampleGlobalFog(float3 position, float stepLength, float clubsFootprint, uint cutMask)
        {
            FogStepSample result;
            result.position = position;
            result.stepLength = stepLength;
            result.density = FogHeightDensity(position.y);
            result.hidingDensity = 0.0;
            result.albedo = _HeightFogColor.rgb;
            result.lighting = _HeightFogLighting;

            #if defined(_HEIGHT_FOG_AREAS_CUT)
                result.density *= SampleAreaCuts(position, cutMask);
            #endif

            // Вырезанный или пустой шаг — клубов не читаем.
            if (result.density <= 0.0)
                return result;

            float clubs = 0.0;

            // Клубы, которые ничего не меняют, не читаем.
            UNITY_BRANCH
            if (_HeightFogNoise.y > 0.0 || _HeightFogNoise.w != 0.0)
                clubs = SampleFogClubs(position, _HeightFogWind.xyz, _HeightFogNoise.x, _HeightFogDetail.x, _HeightFogDetail.y, clubsFootprint);

            result.density *= max(1.0 + _HeightFogNoise.y * clubs, 0.0);
            result.hidingDensity = result.density * _HeightFogColor.w;
            result.albedo *= 1.0 + _HeightFogNoise.w * clubs;

            return result;
        }

        // ── Общий туман: кусок луча внутри слоя ───────────────────────────

        // Расстояния от камеры, между которыми луч идёт внутри тумана: x вход, y выход.
        // Выход не дальше входа — тумана на луче нет.
        float2 FindFogSpan(float3 cameraPosition, float3 rayDirection, float surfaceDistance)
        {
            float ceiling = _HeightFogLayer.y + HEIGHT_FOG_CEILING_THICKNESSES / _HeightFogLayer.z;
            float enter = _HeightFogStartDistance;
            float exit = surfaceDistance;

            if (abs(rayDirection.y) > 1e-5)
            {
                float ceilingDistance = (ceiling - cameraPosition.y) / rayDirection.y;

                // Вверх луч покидает слой на потолке, вниз — входит в него там же.
                if (rayDirection.y > 0.0)
                    exit = min(exit, ceilingDistance);
                else
                    enter = max(enter, ceilingDistance);
            }
            else if (cameraPosition.y > ceiling)
            {
                return float2(0.0, 0.0);
            }

            exit = min(exit, enter + _HeightFogLayer.w);

            return float2(enter, exit);
        }

        // ── Области тумана: шаг ───────────────────────────────────────────

        FogStepSample SampleAreasFog(float3 position, float stepLength, float clubsFootprint, uint fogMask)
        {
            FogAreasSample areas = SampleFogAreas(position, fogMask, clubsFootprint);

            FogStepSample result;
            result.position = position;
            result.stepLength = stepLength;
            result.density = areas.density;
            result.hidingDensity = areas.hidingDensity;
            result.albedo = areas.albedo;
            result.lighting = areas.lighting;

            return result;
        }

        // ── Шаг ламп: весь туман в точке ──────────────────────────────────

        // Весь туман в точке — общий и областей вместе, цвет и свет средние с весом по густоте.
        // Шагам ламп нужен любой туман, какой там есть: у них свой кусок луча, и общий слой
        // учитывается только внутри своего куска.
        FogStepSample SampleAllFog(float3 position, float distance, float stepLength, float2 layerSpan, uint cutMask, uint fogMask)
        {
            FogStepSample result;
            result.position = position;
            result.stepLength = 0.0;
            result.density = 0.0;
            result.hidingDensity = 0.0;
            result.albedo = 0.0;
            result.lighting = 0.0;

            float clubsFootprint = FogClubsFootprint(distance, stepLength);

            if (layerSpan.y > layerSpan.x && distance >= layerSpan.x && distance <= layerSpan.y)
            {
                FogStepSample layer = SampleGlobalFog(position, 0.0, clubsFootprint, cutMask);

                if (layer.density > 0.0)
                {
                    result.density = layer.density;
                    result.hidingDensity = layer.hidingDensity;
                    result.albedo = layer.albedo * layer.density;
                    result.lighting = layer.lighting * layer.density;
                }
            }

            #if defined(HEIGHT_FOG_HAS_AREAS)
                if (fogMask != 0u)
                {
                    FogAreasSample areas = SampleFogAreas(position, fogMask, clubsFootprint);

                    result.density += areas.density;
                    result.hidingDensity += areas.hidingDensity;
                    result.albedo += areas.albedo * areas.density;
                    result.lighting += areas.lighting * areas.density;
                }
            #endif

            float weight = 1.0 / max(result.density, 1e-5);

            result.albedo *= weight;
            result.lighting *= weight;

            return result;
        }

        // ── Общие помощники ───────────────────────────────────────────────

        // Сколько шагов отдать куску луча: короткому — меньше, но не меньше двух.
        int CountSteps(float spanLength, int maxSteps, float shortestStep)
        {
            return clamp((int)ceil(spanLength / shortestStep), 2, max(maxSteps, 2));
        }

        // Сдвиг начала шагов для этого пикселя, от нуля до единицы. Соседние пиксели получают
        // далёкие друг от друга доли — после размытия от полос не остаётся следа.
        float FogStepJitter(float2 pixel)
        {
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        // Ближайшая из двух глубин по диагонали блока кадра: решето растворения пишет глубину
        // через пиксель, и одной выборки на блок хватало то на объект, то на фон за ним.
        float SampleBlockDepth(float2 uv)
        {
            float2 corner = _HeightFogDepthTap.xy;

            if (corner.x <= 0.0)
                return SampleSceneDepth(uv);

            float upperLeft = SampleSceneDepth(uv - corner);
            float lowerRight = SampleSceneDepth(uv + corner);

            #if UNITY_REVERSED_Z
                return max(upperLeft, lowerRight);
            #else
                return min(upperLeft, lowerRight);
            #endif
        }

        // ── Туман в пикселе ───────────────────────────────────────────────

        // Свет тумана, который надо добавить к кадру (rgb), и сколько кадра туман закрыл (a).
        half4 ComputeHeightFog(float2 uv, float2 pixel)
        {
            float rawDepth = SampleBlockDepth(uv);

            #if UNITY_REVERSED_Z
                bool isSky = rawDepth < 1e-6;
            #else
                bool isSky = rawDepth > 1.0 - 1e-6;
            #endif

            float3 cameraPosition = _WorldSpaceCameraPos;
            float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            float3 ray = scenePosition - cameraPosition;
            float rayLength = length(ray);
            float3 rayDirection = ray / max(rayLength, 1e-5);

            // У неба поверхности нет — луч упирается только в дальность.
            float surfaceDistance = isSky ? 1e7 : rayLength;

            // Общий туман пропускаем, если его нет или это небо, которое решили не красить.
            // Области при этом остаются: облако тумана на фоне неба видно всегда.
            float2 layerSpan = float2(0.0, 0.0);

            if (_HeightFogLayer.x > 0.0 && !(isSky && _HeightFogNoise.z < 0.5))
                layerSpan = FindFogSpan(cameraPosition, rayDirection, surfaceDistance);

            float2 areasSpan = float2(0.0, 0.0);
            uint fogMask = 0u;
            uint cutMask = 0u;

            #if defined(HEIGHT_FOG_HAS_AREAS)
                FindAreasOnRay(cameraPosition, rayDirection, surfaceDistance, areasSpan, fogMask, cutMask);
            #endif

            int maxSteps = (int)_HeightFogStepCounts.x;
            int layerSteps = layerSpan.y > layerSpan.x ? CountSteps(layerSpan.y - layerSpan.x, maxSteps, HEIGHT_FOG_SHORTEST_STEP) : 0;
            int areaSteps = areasSpan.y > areasSpan.x ? CountSteps(areasSpan.y - areasSpan.x, maxSteps, HEIGHT_FOG_SHORTEST_STEP) : 0;

            if (layerSteps + areaSteps == 0)
                return half4(0.0, 0.0, 0.0, 0.0);

            float layerStepLength = (layerSpan.y - layerSpan.x) / max(layerSteps, 1);
            float areaStepLength = (areasSpan.y - areasSpan.x) / max(areaSteps, 1);
            float jitter = FogStepJitter(pixel);

            // Шаги ламп — только там, где луч идёт сквозь их свет и сквозь туман.
            int lampSteps = 0;

            #if defined(HEIGHT_FOG_HAS_LAMPS)
                float marchStart = min(layerSteps > 0 ? layerSpan.x : 1e7, areaSteps > 0 ? areasSpan.x : 1e7);
                float marchEnd = max(layerSteps > 0 ? layerSpan.y : 0.0, areaSteps > 0 ? areasSpan.y : 0.0);
                LampStepsOnRay lampStepsOnRay = FindLampStepsOnRay(cameraPosition, rayDirection, marchStart, marchEnd,
                                                                   (int)_HeightFogStepCounts.y, jitter);

                lampSteps = lampStepsOnRay.totalSteps;
            #endif

            // Сколько света тумана ещё доходит до камеры, сколько кадра ещё видно за туманом
            // и какой свет туман уже отдал камере.
            float transmittance = 1.0;
            float sceneVisibility = 1.0;
            float3 fogLight = 0.0;
            int layerStep = 0;
            int areaStep = 0;
            float marchedDistance = 0.0;  // докуда луч уже прошли шаги слоя и областей

            [loop]
            for (int i = 0; i < layerSteps + areaSteps + lampSteps; i++)
            {
                // Из трёх очередных шагов — слоя, областей и ламп — берём ближний.
                float layerDistance = layerStep < layerSteps ? layerSpan.x + (layerStep + jitter) * layerStepLength : 1e8;
                float areaDistance = areaStep < areaSteps ? areasSpan.x + (areaStep + jitter) * areaStepLength : 1e8;
                float lampDistance = 1e8;
                int lamp = -1;

                #if defined(HEIGHT_FOG_HAS_LAMPS)
                    lamp = FindNearestLampStep(lampStepsOnRay, lampDistance);
                #endif

                if (lamp >= 0 && lampDistance < layerDistance && lampDistance < areaDistance)
                {
                    // Шаг лампы только добавляет её свет: что его закрывает, уже посчитали шаги ближе.
                    #if defined(HEIGHT_FOG_HAS_LAMPS)
                        float lampStepLength = TakeLampStep(lampStepsOnRay, lamp, jitter);
                        float3 lampPoint = cameraPosition + rayDirection * lampDistance;
                        FogStepSample fogHere = SampleAllFog(lampPoint, lampDistance, lampStepLength, layerSpan, cutMask, fogMask);

                        // Туман между концом последнего шага слоя или областей и шагом лампы тоже
                        // гасит её свет: шаги слоя бывают по нескольку метров.
                        float lampTransmittance = transmittance * exp(-fogHere.density * max(lampDistance - marchedDistance, 0.0));

                        // Доля — линейная, без насыщения: длина шага лампы не настоящий кусок луча,
                        // а его вес, и на хвостах доходит до десятков метров — 1 − exp там упиралась бы
                        // в единицу, и ореол в густом тумане выходил бы ярче и рябее, чем есть.
                        UNITY_BRANCH
                        if (fogHere.density > 0.0 && lampTransmittance > HEIGHT_FOG_OPAQUE_ENOUGH)
                        {
                            float lampShare = lampTransmittance * fogHere.density * lampStepLength;

                            fogLight += fogHere.albedo * ComputeFogLampLight(lampPoint, rayDirection, fogHere.lighting, lamp) * lampShare;
                        }
                    #endif

                    continue;
                }

                FogStepSample fogStep;

                if (layerDistance <= areaDistance)
                {
                    fogStep = SampleGlobalFog(cameraPosition + rayDirection * layerDistance, layerStepLength,
                                              FogClubsFootprint(layerDistance, layerStepLength), cutMask);
                    marchedDistance = layerDistance + (1.0 - jitter) * layerStepLength;
                    layerStep++;
                }
                else
                {
                    fogStep = SampleAreasFog(cameraPosition + rayDirection * areaDistance, areaStepLength,
                                             FogClubsFootprint(areaDistance, areaStepLength), fogMask);
                    marchedDistance = areaDistance + (1.0 - jitter) * areaStepLength;
                    areaStep++;
                }

                // Пустой шаг — ни света, ни непрозрачности.
                if (fogStep.density <= 0.0)
                    continue;

                float stepTransmittance = exp(-fogStep.density * fogStep.stepLength);
                float visibleShare = transmittance * (1.0 - stepTransmittance);
                float3 stepColor = fogStep.albedo;

                // Свет считаем у каждого шага, а тень — только у заметного: тень самое дорогое на
                // шаге. Пропусти шаг целиком — тонкий туман у стен пропадал бы вовсе, и на
                // расстоянии, где шаги становятся длиннее, проступало бы кольцо.
                #if defined(HEIGHT_FOG_IS_LIT)
                    UNITY_BRANCH
                    if (fogStep.lighting.x > 0.0)
                        stepColor *= ComputeFogLight(fogStep.position, rayDirection, fogStep.lighting, visibleShare > HEIGHT_FOG_INVISIBLE_STEP);
                #endif

                fogLight += stepColor * visibleShare;

                transmittance *= stepTransmittance;
                sceneVisibility *= exp(-fogStep.hidingDensity * fogStep.stepLength);

                // Свет тумана дальше не доходит. Кадр за полупрозрачным туманом этим чуть
                // недозакрыт — на глаз это не читается.
                if (transmittance < HEIGHT_FOG_OPAQUE_ENOUGH)
                    break;
            }

            return half4(fogLight, saturate(1.0 - sceneVisibility));
        }

        // ── Растяжение поверх кадра ───────────────────────────────────────

        // Насколько выборка тумана годится для этого пикселя: чем дальше её глубина от глубины
        // пикселя, тем меньше. Иначе туман дальнего фона заползал бы на край близкого предмета
        // ореолом, и наоборот.
        float CalculateDepthWeight(float sampleDepth, float pixelDepth)
        {
            float difference = abs(sampleDepth - pixelDepth) / max(pixelDepth, 1e-3);

            return 1.0 / (difference * 20.0 + 0.05);
        }
        ENDHLSL

        Pass
        {
            Name "HeightFogOffscreen"

            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentHeightFogOffscreen
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_NOISE_VOLUME _HEIGHT_FOG_NOISE_DETAIL
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_AREAS _HEIGHT_FOG_AREAS_CUT
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_LIT _HEIGHT_FOG_LIT_LAMPS _HEIGHT_FOG_LIT_LAMP_SHADOWS
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_SUN_SHADOWS _HEIGHT_FOG_SUN_SHADOWS_CASCADE

            half4 FragmentHeightFogOffscreen(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return ComputeHeightFog(input.texcoord, input.positionCS.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HeightFogComposite"

            // Свет тумана добавляем как есть, а из кадра вычитаем ровно ту долю,
            // которую туман закрыл.
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentHeightFogComposite

            // Четыре билинейные выборки по углам текселя — мягкое размытие на 3×3 текселя.
            // Оно и растворяет сдвиг шагов между соседними пикселями в ровную пелену.
            // Каждая выборка весит тем меньше, чем дальше её глубина от глубины пикселя.
            half4 FragmentHeightFogComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 offset = _HeightFogTexelSize.xy * 0.5;
                float pixelDepth = LinearEyeDepth(SampleSceneDepth(input.texcoord), _ZBufferParams);

                float2 corners[4] =
                {
                    float2(-offset.x, -offset.y),
                    float2( offset.x, -offset.y),
                    float2(-offset.x,  offset.y),
                    float2( offset.x,  offset.y),
                };

                half4 fog = 0.0;
                float totalWeight = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 uv = input.texcoord + corners[i];
                    float weight = CalculateDepthWeight(LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams), pixelDepth);

                    fog += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * weight;
                    totalWeight += weight;
                }

                return fog / totalWeight;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
