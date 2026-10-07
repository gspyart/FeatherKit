// Туман по высоте одним полноэкранным проходом.
//
// Как это работает. Глубина кадра говорит, где стоит поверхность под каждым пикселем, —
// отсюда восстанавливается мировая точка и луч от камеры до неё. Плотность тумана падает
// вверх по экспоненте, и сколько его набралось вдоль луча, считается формулой, а не шагами
// по лучу: рейтмарч в четыре-восемь выборок стоит вчетверо дороже и на слое ровной плотности
// даёт ровно тот же результат.
//
// Считается отрезок ОТ ПОВЕРХНОСТИ к камере, а не от камеры к поверхности, и это не мелочь.
// Дальность режет его с дальнего от поверхности конца — остаются последние метры луча перед
// землёй, — а плотность берётся у нижнего конца, где туман гуще всего. Так камера может
// отъезжать сколько угодно: под ней обрезается пустой воздух, а слой у земли остаётся целым,
// и туман на земле не зависит ни от высоты камеры, ни от того, стоит герой в низине или
// на холме. Считать от камеры значило перемножать ничтожную плотность у неё с астрономическим
// ростом вдоль луча, и зажимы от переполнения глушили туман, стоило камере подняться выше
// сорока толщин слоя над землёй — для тонкого слоя это несколько метров отъезда.
//
// Дальность отсюда получается сама: чем длиннее кусок луча внутри слоя, тем гуще пелена.
// Отдельной «дистанционной дымки» заводить не нужно.
//
// Пятна берутся не рейтмарчем, а ВЫБОРКАМИ НА ПЛОСКОСТЯХ СЛОЯ — по одной на слой пятен.
// Точка на плоскости стоит в мире, и это главное её свойство: середина пути до поверхности
// ползла бы вместе с камерой, и пелена выглядела бы приклеенной к экрану.
//
// Одной плоскости мало: пятна тогда лежат ковром по земле и туман читается плоским. Вторая
// плоскость у верхней кромки слоя уезжает от первой тем дальше, чем более полог взгляд, —
// этим и получается объём, ценой ещё одной выборки вместо целого марша по лучу.
//
// ЦЕНА. Дороже всего здесь пятна: сама формула тумана — это десятки операций, а два
// слоя процедурного шума — под сотню. Поэтому послаблений три, от самого крупного к мелкому:
//   — текстура пятен вместо формулы: одна выборка вместо всей арифметики;
//   — проходы 1 и 2: туман считается в своей текстуре пониженного разрешения и растягивается
//     поверх кадра. Туман низкочастотный, на растяжении это не читается, а пикселей
//     считается вчетверо (или в шестнадцать раз) меньше;
//   — второй слой пятен отключается ключевым словом, и это ровно половина их стоимости.
//
// Проходов поэтому три, и различаются они только смешиванием:
//   0 — прямо в кадр, обычным альфа-блендом. Полное разрешение;
//   1 — в свою текстуру, цвет умножен на непрозрачность. Пониженное разрешение;
//   2 — растянуть посчитанное поверх кадра.
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

        float4 _HeightFogColor;     // цвет тумана, уже в линейном пространстве
        float4 _HeightFogLayer;     // x плотность у пола, y уровень пола (Y в мире), z 1/толщина слоя, w дальность
        float4 _HeightFogNoise;     // x масштаб пятен, y сила в густоте, z красить ли небо, w сила в яркости
        float4 _HeightFogDepthTap;  // xy смещение до угла блока кадра в uv, ноль — блока нет
        float4 _HeightFogWind;      // xyz куда и с какой скоростью плывут пятна, м/с

        TEXTURE2D(_HeightFogNoiseTexture);
        SAMPLER(sampler_HeightFogNoiseTexture);

        // Разброс из трёх чисел без текстуры: на пятна тумана хватает, а лишней выборки
        // и лишнего ассета в пакете не заводит.
        float FogHash(float3 cell)
        {
            cell = frac(cell * float3(0.1031, 0.1030, 0.0973));
            cell += dot(cell, cell.yxz + 33.33);

            return frac((cell.x + cell.y) * cell.z);
        }

        float FogValueNoise(float3 position)
        {
            float3 cell = floor(position);
            float3 offset = frac(position);

            // Сглаживание перехода между клетками. Без него видны грани кубиков сетки.
            offset = offset * offset * (3.0 - 2.0 * offset);

            float bottom = lerp(lerp(FogHash(cell + float3(0, 0, 0)), FogHash(cell + float3(1, 0, 0)), offset.x),
                                lerp(FogHash(cell + float3(0, 1, 0)), FogHash(cell + float3(1, 1, 0)), offset.x), offset.y);

            float top = lerp(lerp(FogHash(cell + float3(0, 0, 1)), FogHash(cell + float3(1, 0, 1)), offset.x),
                             lerp(FogHash(cell + float3(0, 1, 1)), FogHash(cell + float3(1, 1, 1)), offset.x), offset.y);

            return lerp(bottom, top, offset.z);
        }

        // Один слой пятен в мировой точке. Задана текстура — берём её выборкой по XZ,
        // нет — считаем формулой. Сколько таких слоёв взять и где, решает вызывающий.
        float SampleFogNoise(float3 worldPoint, float scale, float time)
        {
            #ifdef _HEIGHT_FOG_NOISE_TEXTURE
                float2 uv = (worldPoint.xz - _HeightFogWind.xz * time) * scale;

                return SAMPLE_TEXTURE2D(_HeightFogNoiseTexture, sampler_HeightFogNoiseTexture, uv).r;
            #else
                float3 position = (worldPoint - _HeightFogWind.xyz * time) * scale;

                return FogValueNoise(position);
            #endif
        }

        // Где луч пересекает горизонтальную плоскость слоя на заданной высоте. Точка СТОИТ
        // В МИРЕ: при движении камеры она смещается ровно настолько, насколько уезжает сам
        // мир на экране, и пятна остаются на своих местах. Точка на середине пути до
        // поверхности этим свойством не обладала — она ехала вместе с камерой, и туман
        // выглядел приклеенным к экрану.
        //
        // Луч, который до плоскости не достаёт или уходит от неё прочь, берёт конец пути:
        // иначе все такие пиксели получили бы шум одной точки у камеры.
        float3 FogLayerPoint(float height, float3 cameraPosition, float3 rayDirection, float rayLength)
        {
            float travel = rayLength;

            if (abs(rayDirection.y) > 1e-3)
            {
                float distanceToLayer = (height - cameraPosition.y) / rayDirection.y;
                travel = distanceToLayer > 0.0 ? min(distanceToLayer, rayLength) : rayLength;
            }

            return cameraPosition + rayDirection * travel;
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

        // Цвет тумана и его непрозрачность в этом пикселе.
        half4 ComputeHeightFog(float2 uv)
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
            float reach = _HeightFogLayer.w;

            // У неба поверхности нет, и лететь лучу некуда — отмеряем ему всю дальность
            // от камеры, иначе горизонт остался бы вырезанным из пелены.
            if (isSky)
            {
                rayLength = reach;
                scenePosition = cameraPosition + rayDirection * reach;
            }

            // Считаем ПОСЛЕДНИЕ метры луча перед поверхностью, а не первые от камеры:
            // дальность режет отрезок с дальнего от поверхности конца. Камера, отъехавшая
            // за черту, теряет пустой воздух под собой, а не густой слой у земли.
            float travel = min(rayLength, reach);
            float startHeight = scenePosition.y - rayDirection.y * travel;

            float falloff = _HeightFogLayer.z;

            // Плотность у НИЖНЕГО конца отрезка. От него и считаем: вверх плотность только
            // падает, показатель экспоненты всегда отрицательный, и ни огромных, ни ничтожных
            // чисел в формуле не возникает. Зажим остался один и бьёт только в саму глубь:
            // нижний конец на сорок толщин НИЖЕ пола — это давно сплошная пелена.
            float lowerEndAboveFloor = min(startHeight, scenePosition.y) - _HeightFogLayer.y;
            float densityAtLowerEnd = _HeightFogLayer.x * exp(-max(lowerEndAboveFloor * falloff, -40.0));

            // Сколько тумана набралось вдоль отрезка. Интеграл экспоненты берётся в лоб,
            // но у почти горизонтального луча знаменатель уходит в ноль — там плотность
            // по пути всё равно постоянна, и хватает простого умножения на длину.
            float rise = abs(rayDirection.y) * falloff;
            float fogAmount;

            if (rise > 1e-4)
                fogAmount = densityAtLowerEnd * (1.0 - exp(-rise * travel)) / rise;
            else
                fogAmount = densityAtLowerEnd * travel;

            float3 fogColor = _HeightFogColor.rgb;

            // Пятна — самая дорогая часть прохода, и при нулевой силе они не считаются вовсе.
            // Ветка одинаковая для всего кадра, поэтому расходиться внутри группы ей негде.
            if (_HeightFogNoise.y > 0.0 || _HeightFogNoise.w != 0.0)
            {
                float thickness = 1.0 / falloff;
                float scale = _HeightFogNoise.x;

                // Точки берутся на луче ДО САМОЙ поверхности, а не до обрезанного дальностью
                // конца: обрезанный конец висит на сфере вокруг камеры и ехал бы вместе с ней.
                // Нижний слой пятен — там, где тумана больше всего.
                float3 lowerPoint = FogLayerPoint(_HeightFogLayer.y + thickness * 0.35, cameraPosition, rayDirection, rayLength);
                float noise = SampleFogNoise(lowerPoint, scale, _Time.y);

                #ifdef _HEIGHT_FOG_VOLUMETRIC_SPOTS
                    // Второй слой у верхней кромки. Он и даёт ОБЪЁМ: одна плоскость пятен
                    // читается ковром, разложенным по земле, а две разнесённые по высоте
                    // уезжают друг от друга тем сильнее, чем полож взгляд, — и туман
                    // перестаёт быть плоским. Масштаб и скорость у него свои: совпади они,
                    // слои сложились бы в один и разницы бы не было.
                    float3 upperPoint = FogLayerPoint(_HeightFogLayer.y + thickness * 1.6, cameraPosition, rayDirection, rayLength);
                    noise = noise * 0.6 + SampleFogNoise(upperPoint, scale * 0.7, _Time.y * 1.7) * 0.4;
                #endif

                fogAmount *= 1.0 + _HeightFogNoise.y * (noise * 2.0 - 1.0);

                // Второй след тех же пятен — в яркости. Густота в плотной пелене насыщается:
                // где туман закрыл всё, что 90%, что 99% выглядят одинаково, и рисунок
                // пропадает. Яркость не насыщается, и по ней пятна читаются и в сплошной
                // пелене. Выборка та же, стоит это одно умножение.
                fogColor *= 1.0 + _HeightFogNoise.w * (noise * 2.0 - 1.0);
            }

            // Накопленная плотность — в непрозрачность. Насыщается сама, поэтому густоту
            // в Volume можно задирать без опаски получить переполнение.
            float fog = 1.0 - exp(-max(fogAmount, 0.0));

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
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_VOLUMETRIC_SPOTS
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_NOISE_TEXTURE

            half4 FragmentHeightFog(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return ComputeHeightFog(input.texcoord);
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
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_VOLUMETRIC_SPOTS
            #pragma multi_compile_local_fragment _ _HEIGHT_FOG_NOISE_TEXTURE

            half4 FragmentHeightFogOffscreen(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 fog = ComputeHeightFog(input.texcoord);

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

            half4 FragmentHeightFogComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
