// Вихрь по маске для UI-картинки: белое в маске крутится вокруг центра быстрее, серое медленнее,
// чёрное стоит. Зациклено без скручивания: два слоя проворачиваются на малый угол и по очереди
// сбрасываются, пока их не видно (приём flow map). Поверх — радужный край, свечение ядра, блик.
// Время берёт у UpdateRunner (_FeatherUnscaledTime): встроенное _Time обнуляется при загрузке сцены.
Shader "FeatherKit/UI/Masked Swirl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Картинка", 2D) = "white" {}
        _Color ("Оттенок", Color) = (1, 1, 1, 1)

        [Header(Swirl Mask)]
        [NoScaleOffset] _SwirlMask ("Маска: белое крутится, чёрное стоит", 2D) = "black" {}
        _MaskFloor ("Ниже этого не крутится", Range(0, 1)) = 0
        _MaskCeiling ("Выше этого — в полную силу", Range(0, 1)) = 1
        _MaskCurve ("Изгиб: больше 1 — серое слабее", Range(0.2, 4)) = 1
        _MaskBlur ("Размытие маски (нужны мипы)", Range(0, 6)) = 1

        [Header(Swirl)]
        _PivotX ("Центр по ширине", Range(0, 1)) = 0.5
        _PivotY ("Центр по высоте", Range(0, 1)) = 0.5
        _Stretch ("Форма: ширина к высоте (1 — круг)", Range(0.3, 3)) = 1
        _Speed ("Скорость, град/с (минус — против часовой)", Range(-180, 180)) = 35
        _Pull ("Затягивание к центру", Range(-1, 1)) = 0.2
        _CycleDuration ("Цикл, сек: дольше — шире размах и двоение", Range(0.3, 6)) = 1.4
        _PhaseJitter ("Разброс цикла по экрану", Range(0, 1)) = 0.45
        _JitterScale ("Размер пятен разброса", Range(0.5, 12)) = 3

        [Header(Chromatic Edge)]
        [Toggle(_SWIRL_CHROMA)] _Chroma ("Включить", Float) = 1
        _ChromaAngle ("Разлёт цветов, град", Range(0, 10)) = 1.2

        [Header(Core Glow)]
        [HDR] _GlowColor ("Цвет свечения", Color) = (1, 0.75, 1, 1)
        _GlowStrength ("Сила свечения", Range(0, 3)) = 0.15
        _GlowRadius ("Радиус, доли высоты", Range(0.01, 1)) = 0.1
        _GlowPulseAmount ("Сила пульса", Range(0, 1)) = 0.35
        _GlowPulseRate ("Пульсов в секунду", Range(0, 4)) = 0.5

        [Header(Arm Shimmer)]
        _ShimmerStrength ("Сила блика", Range(0, 2)) = 0.4
        [IntRange] _ShimmerArms ("Сколько рукавов", Range(1, 8)) = 3
        _ShimmerTwist ("Закрутка рукавов", Range(-12, 12)) = 4
        _ShimmerSharpness ("Узость блика", Range(1, 16)) = 5
        _ShimmerSpeed ("Оборотов блика в секунду", Range(0, 2)) = 0.2

        [Header(Setup)]
        _MaskPreview ("Показать маску и центр", Range(0, 1)) = 0

        // Их выставляет сама UI-система, когда картинка попадает в маску или в список
        // отсечения. Не объявишь — Unity ругается на материал.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MaskedSwirl"

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _SWIRL_CHROMA

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _SwirlMask;
            fixed4 _Color;
            float4 _ClipRect;

            float _MaskFloor;
            float _MaskCeiling;
            float _MaskCurve;
            float _MaskBlur;

            float _PivotX;
            float _PivotY;
            float _Stretch;
            float _Speed;
            float _Pull;
            float _CycleDuration;
            float _PhaseJitter;
            float _JitterScale;

            float _ChromaAngle;

            float4 _GlowColor;
            float _GlowStrength;
            float _GlowRadius;
            float _GlowPulseAmount;
            float _GlowPulseRate;

            float _ShimmerStrength;
            float _ShimmerArms;
            float _ShimmerTwist;
            float _ShimmerSharpness;
            float _ShimmerSpeed;

            float _MaskPreview;

            // Реальное время от UpdateRunner, общее на всю игру.
            float _FeatherUnscaledTime;


            // ── Время и место ─────────────────────────────────────────────

            // Раннера нет только в редакторе вне игры — тогда крутим по встроенному, чтобы превью жило.
            float SwirlTime()
            {
                return _FeatherUnscaledTime > 0.0 ? _FeatherUnscaledTime : _Time.y;
            }

            // Ширина картинки на экране в долях её высоты: без поправки круг на вытянутом экране стал бы овалом.
            float ScreenAspect(float2 uv)
            {
                float2 alongWidth = float2(ddx(uv.x), ddy(uv.x));
                float2 alongHeight = float2(ddx(uv.y), ddy(uv.y));

                return length(alongHeight) / max(length(alongWidth), 1e-6);
            }


            // ── Маска ─────────────────────────────────────────────────────

            // Размытие через мипы: резкий край маски даёт резкий перепад скорости, и на нём картинка рвётся.
            float SampleSwirlMask(float2 uv)
            {
                float raw = tex2Dlod(_SwirlMask, float4(uv, 0.0, _MaskBlur)).r;
                float level = saturate((raw - _MaskFloor) / max(_MaskCeiling - _MaskFloor, 1e-3));

                return pow(level, _MaskCurve);
            }


            // ── Слои вихря ────────────────────────────────────────────────

            float2 Rotate(float2 position, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);

                return float2(cosine * position.x - sine * position.y, sine * position.x + cosine * position.y);
            }

            // Градиенты — от исходной точки: у сдвинутой они рвутся на сбросе слоя, и мип дал бы шов.
            fixed4 SampleTurned(float2 offset, float angle, float2 pivot, float2 shape, float2 dx, float2 dy)
            {
                float2 uv = Rotate(offset, angle) / shape + pivot;

                return tex2Dgrad(_MainTex, uv, dx, dy);
            }

            // Один слой: картинка, провёрнутая и подтянутая к центру на столько, сколько слой уже живёт.
            fixed4 SampleSwirlLayer(float2 offset, float angle, float scale, float chroma,
                                    float2 pivot, float2 shape, float2 dx, float2 dy)
            {
                float2 pulled = offset * scale;
                fixed4 color = SampleTurned(pulled, angle, pivot, shape, dx, dy);

            #ifdef _SWIRL_CHROMA
                color.r = SampleTurned(pulled, angle + chroma, pivot, shape, dx, dy).r;
                color.b = SampleTurned(pulled, angle - chroma, pivot, shape, dx, dy).b;
            #endif

                return color;
            }

            float Hash(float2 cell)
            {
                cell = frac(cell * float2(123.34, 456.21));
                cell += dot(cell, cell + 45.32);

                return frac(cell.x * cell.y);
            }

            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 inside = frac(position);
                float2 blend = inside * inside * (3.0 - 2.0 * inside);

                float bottom = lerp(Hash(cell), Hash(cell + float2(1.0, 0.0)), blend.x);
                float top = lerp(Hash(cell + float2(0.0, 1.0)), Hash(cell + 1.0), blend.x);

                return lerp(bottom, top, blend.y);
            }

            fixed4 SampleSwirl(float2 offset, float mask, float time, float2 pivot, float2 shape,
                               float2 dx, float2 dy)
            {
                // Разброс фазы пятнами: иначе сброс слоёв шёл бы по всему экрану разом, и вихрь «дышал».
                float cycle = time / _CycleDuration + ValueNoise(offset * _JitterScale + 17.0) * _PhaseJitter;
                float phaseA = frac(cycle);
                float phaseB = frac(cycle + 0.5);

                // Секунды от минус до плюс полцикла: в пике веса слой не сдвинут, сброс приходится на нулевой вес.
                float livedA = (phaseA - 0.5) * _CycleDuration;
                float livedB = (phaseB - 0.5) * _CycleDuration;

                float turnSpeed = radians(_Speed) * mask;
                float pullSpeed = _Pull * mask;
                float chroma = radians(_ChromaAngle) * mask;

                fixed4 layerA = SampleSwirlLayer(offset, turnSpeed * livedA, exp(pullSpeed * livedA), chroma,
                                                 pivot, shape, dx, dy);
                fixed4 layerB = SampleSwirlLayer(offset, turnSpeed * livedB, exp(pullSpeed * livedB), chroma,
                                                 pivot, shape, dx, dy);

                // Вес по квадрату синуса, а не по прямой: слой дольше стоит чётким и быстрее проходит двоение.
                float weightA = sin(UNITY_PI * phaseA);

                return lerp(layerB, layerA, weightA * weightA);
            }


            // ── Украшения ─────────────────────────────────────────────────

            // Угол плюс закрутка по логарифму радиуса — это и есть логарифмическая спираль, как у рукавов.
            float ArmShimmer(float2 offset, float radius, float time)
            {
                float arms = round(_ShimmerArms);
                float turn = UNITY_TWO_PI * frac(_ShimmerSpeed * time) * sign(_Speed);
                float angle = atan2(offset.y, offset.x);

                float wave = 0.5 + 0.5 * cos(arms * (angle + turn) + _ShimmerTwist * log(max(radius, 1e-4)));

                // У самой середины спираль сходится в точку и рябит — там блик гасим.
                return pow(wave, _ShimmerSharpness) * smoothstep(0.0, 0.08, radius);
            }

            float CoreGlow(float radius, float time)
            {
                float pulse = 1.0 + _GlowPulseAmount * sin(UNITY_TWO_PI * frac(_GlowPulseRate * time));
                float falloff = exp(-(radius * radius) / (_GlowRadius * _GlowRadius));

                return falloff * _GlowStrength * pulse;
            }

            float3 DrawMaskPreview(float3 color, float mask, float radius)
            {
                float marker = 1.0 - smoothstep(0.006, 0.01, radius);
                float3 preview = lerp(mask.xxx, float3(1.0, 0.1, 0.2), marker);

                return lerp(color, preview, _MaskPreview);
            }


            // ── Проходы ───────────────────────────────────────────────────

            Varyings Vertex(Attributes input)
            {
                Varyings output;

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;

                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);

                // В этом пространстве овал вихря — круг: и поворот, и свечение считаются по нему.
                float2 shape = float2(ScreenAspect(uv) / _Stretch, 1.0);
                float2 pivot = float2(_PivotX, _PivotY);
                float2 offset = (uv - pivot) * shape;
                float radius = length(offset);

                float time = SwirlTime();
                float mask = SampleSwirlMask(uv);

                fixed4 color = SampleSwirl(offset, mask, time, pivot, shape, dx, dy) * input.color;

                color.rgb *= 1.0 + _ShimmerStrength * mask * ArmShimmer(offset, radius, time);
                color.rgb += _GlowColor.rgb * CoreGlow(radius, time);
                color.rgb = DrawMaskPreview(color.rgb, mask, radius);

            #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
            #endif

            #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
            #endif

                return color;
            }
            ENDCG
        }
    }
}
