// Объёмный туман по высоте одним полноэкранным проходом.
//
// Как это работает. Глубина кадра говорит, где стоит поверхность под каждым пикселем, —
// отсюда восстанавливается луч от камеры до неё. По лучу туман проходится ШАГАМИ: в каждой
// точке густота берётся от высоты над полом и от 3D-текстуры клубов, и каждый шаг закрывает
// собой всё, что за ним. Так густой клуб впереди прячет то, что лежит дальше, и туман
// читается объёмом, а не узором, нарисованным поверх кадра.
//
// ГУСТОТА ПО ВЫСОТЕ. Выше пола туман редеет по экспоненте, ниже пола — ровный, той же
// густоты, что на полу. Расти вниз без предела ему нельзя: тогда пол, поднятый над землёй,
// не сдвигал бы туман, а сгущал его в разы, и ручка «где туман» работала бы как «сколько».
//
// КАКОЙ КУСОК ЛУЧА ШАГАЕМ. Только тот, что внутри слоя: от места, где взгляд опустился
// ниже потолка слоя, и не дальше дальности от этого места. Потолок — несколько толщин
// над полом, выше густоты почти нет, и шаги, потраченные там, пропали бы зря. Отсчёт
// дальности от входа в слой, а не от камеры, нужен для камеры, поднятой высоко над туманом:
// пустой воздух под ней дальности не съедает. Ближе дистанции начала тумана нет вовсе.
//
// Небо шагается тем же куском, что и земля дальше дальности, — поэтому на краю геометрии,
// где земля сменяется небом, шва нет.
//
// ПОЛОСЫ ОТ ШАГОВ. Шаги на равных расстояниях дали бы слоистые полосы. Поэтому начало
// шагов у соседних пикселей сдвинуто на разную долю шага, а растяжение пониженного
// разрешения в проходе 2 размывает этот сдвиг в ровную пелену.
//
// ЦЕНА — число шагов, каждый шаг — одна выборка 3D-текстуры. Послабления:
//   — проходы 1 и 2: туман считается в своей текстуре пониженного разрешения и растягивается
//     поверх кадра. Туман низкочастотный, на растяжении это не читается, а пикселей
//     считается вчетверо (или в шестнадцать раз) меньше;
//   — шаги кончаются раньше, когда туман уже закрыл всё: дальше смотреть не на что.
//
// Проходов три, и различаются они только смешиванием:
//   0 — прямо в кадр, обычным альфа-блендом. Полное разрешение;
//   1 — в свою текстуру, цвет умножен на непрозрачность. Пониженное разрешение;
//   2 — растянуть посчитанное поверх кадра, слегка размыв.
//
// Умножение на непрозрачность в проходе 1 не украшение: билинейное растяжение смешивает
// соседние тексели, и там, где туман прозрачен, его цвет затёк бы в чистые места кадра.
Shader "FeatherKit/Height Fog"
{
    // Свойств нет намеренно: все числа приходят из Volume через блок свойств прохода,
    // и материал у эффекта служебный — руками его никто не настраивает.
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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _HeightFogColor;         // цвет тумана, уже в линейном пространстве
        float4 _HeightFogLayer;         // x плотность у пола, y уровень пола (Y в мире), z 1/толщина слоя, w дальность
        float4 _HeightFogNoise;         // x масштаб клубов, y сила в густоте, z красить ли небо, w сила в яркости
        float4 _HeightFogDepthTap;      // xy смещение до угла блока кадра в uv, ноль — блока нет
        float4 _HeightFogWind;          // xyz куда и с какой скоростью плывут клубы, м/с
        float4 _HeightFogTexelSize;     // xy размер текселя своей текстуры тумана, для размытия
        float _HeightFogStartDistance;  // ближе этого расстояния от камеры тумана нет, м
        float _HeightFogStepCount;      // сколько шагов по лучу

        TEXTURE3D(_HeightFogNoiseVolume);
        SAMPLER(sampler_HeightFogNoiseVolume);

        // Потолок слоя — столько толщин над полом. Густота там меньше пятидесятой доли
        // от пола, и шагать выше незачем.
        #define HEIGHT_FOG_CEILING_THICKNESSES 4.0

        // Шаги кончаются, когда сквозь туман видно меньше этой доли.
        #define HEIGHT_FOG_OPAQUE_ENOUGH 0.005

        // ── Густота в точке ───────────────────────────────────────────────

        // Густота по высоте: ровная ниже пола, редеет по экспоненте выше.
        float FogHeightDensity(float height)
        {
            float aboveFloor = max(height - _HeightFogLayer.y, 0.0);

            return _HeightFogLayer.x * exp(-aboveFloor * _HeightFogLayer.z);
        }

        // Клубы в мировой точке, от нуля до единицы, в среднем половина. Без текстуры
        // клубов нет — туман ровный.
        float SampleFogNoise(float3 worldPoint)
        {
            #ifdef _HEIGHT_FOG_NOISE_VOLUME
                float3 uvw = (worldPoint - _HeightFogWind.xyz * _Time.y) * _HeightFogNoise.x;

                return SAMPLE_TEXTURE3D_LOD(_HeightFogNoiseVolume, sampler_HeightFogNoiseVolume, uvw, 0).r;
            #else
                return 0.5;
            #endif
        }

        // ── Кусок луча внутри слоя ────────────────────────────────────────

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

        // Цвет тумана и его непрозрачность в этом пикселе.
        half4 ComputeHeightFog(float2 uv, float2 pixel)
        {
            float rawDepth = SampleBlockDepth(uv);

            #if UNITY_REVERSED_Z
                bool isSky = rawDepth < 1e-6;
            #else
                bool isSky = rawDepth > 1.0 - 1e-6;
            #endif

            // Небо решили не красить — выходим до всех расчётов.
            if (isSky && _HeightFogNoise.z < 0.5)
                return half4(0.0, 0.0, 0.0, 0.0);

            float3 cameraPosition = _WorldSpaceCameraPos;
            float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            float3 ray = scenePosition - cameraPosition;
            float rayLength = length(ray);
            float3 rayDirection = ray / max(rayLength, 1e-5);

            // У неба поверхности нет — луч упирается только в дальность.
            float surfaceDistance = isSky ? 1e7 : rayLength;
            float2 span = FindFogSpan(cameraPosition, rayDirection, surfaceDistance);

            if (span.y <= span.x)
                return half4(0.0, 0.0, 0.0, 0.0);

            int stepCount = max((int)_HeightFogStepCount, 1);
            float stepLength = (span.y - span.x) / stepCount;
            float jitter = FogStepJitter(pixel);

            // Сколько света ещё проходит сквозь туман и сколько тумана уже видно. Яркость
            // клубов копится тем же весом, что и их непрозрачность.
            float transmittance = 1.0;
            float brightness = 0.0;

            [loop]
            for (int i = 0; i < stepCount; i++)
            {
                float3 stepPoint = cameraPosition + rayDirection * (span.x + (i + jitter) * stepLength);
                float noiseOffset = SampleFogNoise(stepPoint) * 2.0 - 1.0;

                float density = FogHeightDensity(stepPoint.y) * max(1.0 + _HeightFogNoise.y * noiseOffset, 0.0);
                float stepOpacity = 1.0 - exp(-density * stepLength);

                brightness += (1.0 + _HeightFogNoise.w * noiseOffset) * transmittance * stepOpacity;
                transmittance *= 1.0 - stepOpacity;

                if (transmittance < HEIGHT_FOG_OPAQUE_ENOUGH)
                    break;
            }

            float fog = 1.0 - transmittance;
            float3 fogColor = _HeightFogColor.rgb * brightness / max(fog, 1e-4);

            return half4(fogColor, saturate(fog));
        }
        ENDHLSL

        Pass
        {
            Name "HeightFog"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentHeightFog
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_NOISE_VOLUME

            half4 FragmentHeightFog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return ComputeHeightFog(input.texcoord, input.positionCS.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HeightFogOffscreen"

            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentHeightFogOffscreen
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_NOISE_VOLUME

            half4 FragmentHeightFogOffscreen(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 fog = ComputeHeightFog(input.texcoord, input.positionCS.xy);

                return half4(fog.rgb * fog.a, fog.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HeightFogComposite"

            // Цвет уже умножен на непрозрачность в проходе 1, поэтому здесь его добавляем
            // как есть, а из кадра вычитаем ровно ту долю, которую туман закрыл.
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentHeightFogComposite

            // Четыре билинейные выборки по углам текселя — мягкое размытие на 3×3 текселя.
            // Оно и растворяет сдвиг шагов между соседними пикселями в ровную пелену.
            half4 FragmentHeightFogComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 offset = _HeightFogTexelSize.xy * 0.5;

                half4 fog = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-offset.x, -offset.y))
                          + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( offset.x, -offset.y))
                          + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-offset.x,  offset.y))
                          + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + float2( offset.x,  offset.y));

                return fog * 0.25;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
