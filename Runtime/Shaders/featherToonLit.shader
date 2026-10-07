// ═══════════════════════════════════════════════════════════════════════════════════════
// ПАМЯТКА ТОМУ, КТО БУДЕТ ЭТО ПРАВИТЬ — ЧЕЛОВЕКУ ИЛИ АГЕНТУ
//
// СТРУКТУРА. Пять проходов, у каждого своя роль в теге LightMode:
//   UniversalForward  — заливка со ступенчатым светом
//   SRPDefaultUnlit   — контур вывернутой оболочкой
//   ShadowCaster      — отбрасывание тени
//   DepthOnly         — карта глубины
//   DepthNormals      — глубина с нормалями, на неё опираются эффекты URP
//
// Движок рисует проход не по имени Name, а по РОЛИ. URP ищет ровно три роли:
// SRPDefaultUnlit, UniversalForward, UniversalForwardOnly. Возьмёте свою роль — проход
// не нарисуется, пока не заведёте под него рендер-фичу. Одна роль = один проход:
// берётся ПЕРВЫЙ подходящий, второй с той же ролью молча игнорируется.
//
// ГРАБЛИ 1: shader_feature_local_fragment. Проходы URP (ShadowCasterPass, DepthOnlyPass)
// смотрят _ALPHATEST_ON и в ВЕРШИННОМ шейдере — там от него зависит раскладка Varyings.
// Суффикс _fragment делает слово невидимым для вершинного этапа, этапы расходятся,
// и проход отваливается целиком. Тени просто исчезают. Только shader_feature_local.
//
// ГРАБЛИ 2: материал может выключить проход. SetShaderPassEnabled живёт В МАТЕРИАЛЕ
// и смену шейдера переживает. Материал от чужого тун-шейдера приезжает с выключенным
// SRPDefaultUnlit — галка обводки стоит, контура нет. Чистит это FMaterialUtils.Sanitize.
//
// ГРАБЛИ 3: CBUFFER. В UnityPerMaterial кладём ТОЛЬКО свойства материала. Чужое там
// ломает SRP Batcher молча — просадка по производительности без единой ошибки.
//
// НОВОЕ СВОЙСТВО добавляется в трёх местах: Properties, CBUFFER и ToonLitShaderGUI
// (там же секция и подсказка). Пропустите инспектор — свойство в нём не появится.
//
// ГДЕ ЛЕЖИТ СВЕТ. Сам расчёт — в featherToonLighting.hlsl рядом. Тем же светом освещается
// земля (FeatherKit/Toon Terrain), а две копии одного света однажды разъедутся, и на стыке
// модель окажется светлее пола. Здесь остаётся только своё: текстура, обводка, прозрачность,
// вспышка урона. СВОЙСТВА света при этом объявляет каждый шейдер сам — общий файл читает их
// по именам, и список этих имён лежит в его шапке.
//
// МОДЕЛЬ ОСВЕЩЕНИЯ. Окружающий свет — общий уровень, солнце к нему ПРИБАВЛЯЕТСЯ, как
// в Lit; ступенька решает только, прибавлять его или нет. Подмена одного цвета другим
// (цвет тени ↔ цвет солнца) выглядит опрятнее на заранее выставленной сцене, но у неё
// два неизлечимых симптома: слабый источник начинает затемнять поверхность, а при ярком
// окружении тени оказываются светлее света. Поэтому сложение. Отсюда и требование к
// сцене: окружающий свет держим в 0.2…0.5, иначе освещённая сторона уезжает в белое.
//
// ПАДАЮЩИЕ ТЕНИ И ОБЩИЕ НАСТРОЙКИ ОБВОДКИ живут не в материале, а в рендер-фиче
// ToonLitSettingsFeature: это настройки проекта, а не отдельной поверхности. Свойство из
// Properties ВСЕГДА перебивает глобальное значение с тем же именем, поэтому таких свойств
// у материала быть не должно — иначе фича будет крутиться впустую. Пока фича не выставила
// метку _FeatherToonReady, шейдер берёт значения по умолчанию из GetToonShadowSettings.
//
// От теней у материала осталось двое, и оба фиче не мешают: _CastShadowOpacity — насколько
// падающая тень видна ИМЕННО ЗДЕСЬ, и _ShadowTint — добавка к общему оттенку, по умолчанию
// белая. Имена у них свои, так что глобальных значений они не перекрывают.
//
// _HitFlash не выведен в инспектор намеренно: его анимирует в рантайме компонент
// FeatherKit.Feedback.HitFlash в свою копию материала, выставленное руками будет затёрто.
// ═══════════════════════════════════════════════════════════════════════════════════════

// Тун-шейдер под URP в духе мобильных казуалок: вместо плавного затухания света —
// две-три ступени по NdotL, тень задаётся не темнотой, а оттенком (_ShadowTint),
// плюс контурная обводка вторым проходом.
//
// В библиотеке он лежит не ради стиля, а ради HitFlash: тому нужны свойства _HitFlash
// и _HitFlashColor, и без шейдера, который их понимает, вспышка при попадании просто
// не показывалась бы. Свой стиль игра делает своим шейдером — лишь бы эти два свойства
// в нём были.
//
// Почему не ShaderGraph: обводка «вывернутой оболочкой» — это отдельный проход с Cull Front,
// а ShaderGraph в Unity 6 многопроходные шейдеры не умеет. В графе пришлось бы всё равно
// писать HLSL в Custom Function и городить второй материал под контур. Здесь оба прохода
// живут в одном файле, и настройки контура правятся на том же материале, что и заливка.
Shader "FeatherKit/Toon Lit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // Правка самой текстуры, до света. Base Color для этого не годится: он умножает,
        // поэтому приглушить может, а поднять яркость или убрать насыщенность — нет.
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1

        // Два режима, как у URP/Lit. Само поле ничего не рисует — оно говорит, каким
        // сделать смешивание и очередь.
        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface Type", Float) = 0

        // Вырезание по альфе — независимая галка, работает в обоих режимах. Для листвы,
        // травы и решёток: объект остаётся непрозрачным, часть пикселей выбрасывается.
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        // Прозрачность решётом: пиксели выбрасываются по регулярному узору, доля
        // выброшенных равна прозрачности. Материал остаётся непрозрачным, поэтому пишет
        // глубину, отбрасывает тень и не спорит с другими прозрачными за порядок
        // отрисовки. Тем и хорош для растворяющихся объектов и для препятствия
        // между камерой и игроком.
        [Toggle(_DITHER_ON)] _Dither ("Dithering (Opaque)", Float) = 0

        // Растворение поверх альфы Base Color, а не вместо неё. Отдельным свойством
        // потому, что его крутит КОД — препятствие между камерой и игроком, — а альфа
        // остаётся за художником. Одно поле на двоих они бы отбирали друг у друга.
        _DitherFade ("Dither Fade", Range(0, 1)) = 0

        // Служебное: выставляется из режима, руками не трогаем.
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("Z Write", Float) = 1

        // ДОБАВКА к общему оттенку тени из рендер-фичи, а не замена ему: они умножаются.
        // Отсюда белый по умолчанию — «своего оттенка нет, беру общий». Альфа — сила
        // подмешивания. Цвет умножается на окружающий свет, а не подменяет его: яркость
        // тени остаётся за освещением сцены, отсюда идёт только оттенок.
        _ShadowTint ("Shadow Tint", Color) = (1, 1, 1, 1)
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.1
        _RampSmoothness ("Ramp Smoothness", Range(0.001, 0.5)) = 0.05
        _MidToneWidth ("Mid Tone Width", Range(0, 0.5)) = 0.15
        _MidToneStrength ("Mid Tone Strength", Range(0, 1)) = 0.35

        // Порога, мягкости, силы и глубины падающей тени здесь нет намеренно: они одни для
        // всех материалов и живут в рендер-фиче ToonLitSettingsFeature. Свойство материала
        // перебило бы глобальное значение, и фича перестала бы работать.

        // А это — про ЭТУ поверхность: насколько на ней видна падающая тень. Единица — как
        // задано в фиче, ноль — падающей тени на объекте нет вовсе, остаётся только
        // затенение по углу к источнику. Нужно там, где общая тень портит один конкретный
        // объект, а переставлять из-за него свет не хочется.
        _CastShadowOpacity ("Cast Shadow Opacity", Range(0, 1)) = 1

        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.12
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.7
        _RimSmoothness ("Rim Smoothness", Range(0.001, 0.5)) = 0.08

        _ToonSpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecGloss ("Specular Size", Range(1, 256)) = 48
        _SpecIntensity ("Specular Intensity", Range(0, 1)) = 0

        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Outline Enabled", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.1, 0.08, 0.15, 1)
        // Толщина в пикселях при ОПОРНОМ разрешении — его задаёт рендер-фича, по умолчанию
        // 1080. Не в метрах: контур не толстеет, когда камера подъезжает. И не в настоящих
        // пикселях: иначе на 4K тот же контур занимал бы вдвое меньшую долю экрана.
        _OutlineWidth ("Outline Width (px)", Range(0, 20)) = 3
        // Второй способ раздувания — от опоры объекта, минуя нормали. Нужен там, где
        // нормали жёсткие и разнонаправленные: у низкополигонального персонажа оболочка
        // по нормалям сворачивается внутрь тела и не видна вовсе.
        _OutlineScale ("Outline Scale", Range(1, 1.2)) = 1
        // Сдвиг оболочки по глубине. Спасает от мерцания там, где она совпадает с моделью.
        _OutlineDepthOffset ("Outline Depth Offset", Range(0, 0.01)) = 0
        // Обводка живёт по свету вместе с заливкой: в тени темнеет, на солнце берёт его
        // оттенок. Без этого контур одного цвета по всей сцене и читается как наклейка.
        [Toggle(_OUTLINE_LIT)] _OutlineLit ("Outline Reacts To Light", Float) = 0
        _OutlineLightInfluence ("Outline Light Influence", Range(0, 1)) = 0.7
        _OutlineFadeStart ("Outline Fade Start", Float) = 40
        _OutlineFadeEnd ("Outline Fade End", Float) = 70

        _HitFlashColor ("Hit Flash Color", Color) = (1, 1, 1, 1)
        // Гонится из HitFlash в собственную копию материала — руками в материале
        // не трогаем, это поле рантайма.
        _HitFlash ("Hit Flash", Range(0, 1)) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Saturation;
            half _Brightness;
            half _Cutoff;
            half _Surface;
            half _DitherFade;
            half4 _ShadowTint;
            half _CastShadowOpacity;
            half _RampThreshold;
            half _RampSmoothness;
            half _MidToneWidth;
            half _MidToneStrength;
            half4 _RimColor;
            half _RimAmount;
            half _RimThreshold;
            half _RimSmoothness;
            half4 _ToonSpecColor;
            half _SpecGloss;
            half _SpecIntensity;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineScale;
            half _OutlineDepthOffset;
            half _OutlineLightInfluence;
            float _OutlineFadeStart;
            float _OutlineFadeEnd;
            half4 _HitFlashColor;
            half _HitFlash;
        CBUFFER_END

        // В общем блоке, а не в проходе: служебные проходы URP (тень, глубина) берут
        // отсюда и саму текстуру, и функции выборки альфы для обрезки.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        // Тун-свет лежит в общем файле: им же освещается слоистая земля, и разъехаться
        // им нельзя — стык модели с землёй виден сразу. Там же договор по именам свойств
        // и глобальные значения из рендер-фичи. Включать только после CBUFFER_END.
        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherToonLighting.hlsl"

        // Правка текстуры лежит в общем файле: ею же правится земля, и разъехаться
        // им нельзя — стык модели с землёй виден сразу.
        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherToonColor.hlsl"

        // Порядковое размытие 4 на 4, матрица Байера. Регулярный узор, а не случайный шум:
        // случайный мерцает между кадрами, а этот стоит на месте и читается как сетка.
        // Пороги сдвинуты на полшага: 0.5/16 … 15.5/16, а не 0/16 … 15/16. Иначе на нулевой
        // прозрачности самый нижний порог даёт clip(0 - 0), а это ровно граница отбрасывания —
        // и на полностью растворённом объекте остаётся сетка точек.
        static const half DitherPattern[16] =
        {
            0.5h / 16, 8.5h / 16, 2.5h / 16, 10.5h / 16,
            12.5h / 16, 4.5h / 16, 14.5h / 16, 6.5h / 16,
            3.5h / 16, 11.5h / 16, 1.5h / 16, 9.5h / 16,
            15.5h / 16, 7.5h / 16, 13.5h / 16, 5.5h / 16
        };

        // Порог берём по позиции на ЭКРАНЕ, а не по UV: узор должен стоять в кадре,
        // а не ездить вместе с моделью.
        half DitherThreshold(float2 positionSS)
        {
            uint x = (uint)positionSS.x & 3;
            uint y = (uint)positionSS.y & 3;

            return DitherPattern[y * 4 + x];
        }

        // Итоговая непрозрачность для решета. Растворение и альфа материала перемножаются,
        // а не спорят: полупрозрачная модель, которую вдобавок растворяют, гаснет сильнее.
        half DitherOpacity(half alpha)
        {
            return alpha * (1.0h - _DitherFade);
        }

        float ToonOutlineReferenceHeight()
        {
            return _FeatherToonReady > 0.5 ? max(_FeatherToonOutlineHeight, 1.0) : 1080.0;
        }

        // Без фичи обводка включена: это её обычное состояние, а метки может не быть вовсе.
        bool ToonOutlineEnabled()
        {
            return _FeatherToonReady < 0.5 || _FeatherToonOutlineEnabled > 0.5;
        }

        // Расстояние до камеры ГЛАЗАМИ ОБВОДКИ. Метры затухания и потолка толщины заданы под
        // обычную камеру, и у отведённой в разы дальше — обзорной карты, меты — контур пропадал
        // бы весь разом. Масштаб из фичи приводит её дистанцию к привычной, поэтому настройки
        // материалов работают в обеих сценах одинаково и править их не приходится.
        float ToonOutlineDistance(float3 positionWS)
        {
            float distanceToCamera = length(GetCameraPositionWS() - positionWS);
            float scale = _FeatherToonReady > 0.5 ? max(_FeatherToonOutlineDistanceScale, 0.001) : 1.0;

            return distanceToCamera / scale;
        }

        ENDHLSL

        // ------------------------------------------------------------------
        // Основной проход: заливка со ступенчатым светом
        // ------------------------------------------------------------------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma target 3.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DITHER_ON

            // Только те ключевые слова, что реально включены в проекте: лайтмапов, кук
            // и SSAO здесь нет, а каждое лишнее объявление удваивает число вариантов
            // шейдера — на мобилке это время компиляции и вес билда.
            // Качество мягких теней в пайплайне стоит High, поэтому набор _SHADOWS_SOFT_*
            // нужен целиком: без нужного варианта Unity молча возьмёт ближайший.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Координата тумана едет в четвёртом компоненте мировой позиции, а не отдельным
            // интерполятором: слот всё равно передаётся целиком, а пустой w — это четверть
            // слота впустую на каждой вершине кадра.
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 positionWSAndFog : TEXCOORD2;
                half3 ambient     : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWSAndFog = float4(positions.positionWS,
                                                 ComputeFogFactor(positions.positionCS.z));
                output.normalWS = normals.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // Окружающий свет — в вершине, а не в пикселе: развёртка сферических гармоник
                // это десятки операций на каждый пиксель экрана, а на модель их приходится
                // столько, сколько у неё вершин. Потери нет: у плоского и градиентного
                // окружающего света результат тот же, у зондов — чуть мягче переход.
                output.ambient = SampleSH(normals.normalWS);

                return output;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a;

            #ifdef _ALPHATEST_ON
                // Выбрасываем прозрачные пиксели до всего остального: считать для них свет
                // и контур незачем.
                clip(alpha - _Cutoff);
            #endif

            #ifdef _DITHER_ON
                clip(DitherOpacity(alpha) - DitherThreshold(input.positionCS.xy));
            #endif

                half3 albedo = ToonAdjustTextureColor(baseSample.rgb, _Saturation, _Brightness)
                             * _BaseColor.rgb;

                float3 positionWS = input.positionWSAndFog.xyz;

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionWS));

                half3 color = ToonLighting(albedo, input.ambient, positionWS, normalWS, viewDirWS,
                                           GetNormalizedScreenSpaceUV(input.positionCS));

                // Вспышка урона — последней, поверх всего освещения: попадание должно
                // читаться одинаково ярко и на солнечной стороне, и в тени.
                color = lerp(color, _HitFlashColor.rgb, saturate(_HitFlash));

                color = MixFog(color, input.positionWSAndFog.w);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Контур: вывернутая оболочка. Модель рисуется второй раз с Cull Front,
        // вершины раздвинуты по нормали — наружу торчит только ободок.
        // ------------------------------------------------------------------
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma target 3.0
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _OUTLINE_LIT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DITHER_ON
            #pragma multi_compile_instancing

            // Ключевые слова теней нужны освещённой обводке. Оборачивать их в #if по
            // _OUTLINE_LIT незачем: pragma Unity собирает до препроцессора, и условие
            // на неё всё равно не действует.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Всё, что нужно только освещённому контуру, объявлено под его ключевым словом:
            // у обычного контура эти три интерполятора не занимают ни байта. Условие
            // на _OUTLINE_LIT безопасно — слово объявлено без суффикса _fragment, поэтому
            // вершинный и фрагментный этапы видят его ОДИНАКОВО (см. ГРАБЛИ 1 в шапке).
            // С _DITHER_ON так нельзя, и потому uv остаются безусловными.
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fade         : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            #ifdef _OUTLINE_LIT
                float3 positionWS : TEXCOORD2;
                half3 normalWS    : TEXCOORD3;
                half3 ambient     : TEXCOORD4;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Раздуваем дважды. Масштаб от опоры объекта даёт оболочку даже там, где
                // нормали негодные, а смещение по нормалям выравнивает её толщину.
                float3 positionOS = input.positionOS.xyz * _OutlineScale;

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

            #ifdef _OUTLINE_ON
                // Общий выключатель из рендер-фичи. Сам проход отменить нечем — это умеет
                // только материал через SetShaderPassEnabled, а материалы фича не трогает.
                // Поэтому схлопываем оболочку в точку: треугольник нулевой площади не даёт
                // ни одного пикселя, и от прохода остаётся только вершинный шейдер.
                if (!ToonOutlineEnabled())
                {
                    output.positionCS = float4(0, 0, 0, 1);
                    return output;
                }

                // Раздвигаем не в мире, а на экране: тогда толщина не зависит ни от
                // дистанции, ни от FOV.
                //
                // Сторона «наружу» — это не нормаль в clip space, а её перспективная
                // производная: экранная точка равна xy/w, поэтому направление зависит ещё
                // и от места вершины в кадре. Без поправки контур к краям экрана уезжает,
                // и толщина по кадру гуляет. На w не делим намеренно: нужна только сторона,
                // а не длина, — и заодно не ловим ноль в знаменателе у вершин за камерой.
                float4 normalCS = mul(UNITY_MATRIX_VP, float4(normalWS, 0.0));
                float2 direction = normalCS.xy * positionCS.w - positionCS.xy * normalCS.w;
                float directionLengthSq = dot(direction, direction);

                // Нормаль смотрит ровно в камеру: на силуэте такой вершины нет, раздувать её
                // незачем. А нормализовать ноль — это NaN и полоса через весь кадр.
                direction = directionLengthSq > 1e-10 ? direction * rsqrt(directionLengthSq) : 0;

                // На дальних объектах контур гасим — иначе мелочь на горизонте
                // превращается в кашу из тёмных точек.
                float distanceToCamera = ToonOutlineDistance(positionWS);
                float fade = 1.0 - saturate((distanceToCamera - _OutlineFadeStart) /
                                            max(_OutlineFadeEnd - _OutlineFadeStart, 0.001));

                // Толщина в пикселях текущего кадра: заданные в материале пиксели опорного
                // разрешения, пересчитанные на нынешнее. Так контур занимает одну и ту же долю
                // экрана на любом разрешении и при любом Render Scale.
                float pixels = _OutlineWidth * _ScreenParams.y / ToonOutlineReferenceHeight();

                // Потолок толщины. Постоянные пиксели означают, что на отлёте контур занимает
                // всё большую долю усохшего силуэта — отсюда и «жирно». Поэтому дальше опорной
                // дистанции толщина падает как 1/дистанция, то есть ведёт себя как заданная
                // в метрах: объект и его контур усыхают вместе, доля силуэта не меняется.
                // Ближе опорной дистанции — ровно те пиксели, что заданы в материале.
                if (_FeatherToonOutlineDistance > 0.0)
                    pixels *= min(1.0, _FeatherToonOutlineDistance / max(distanceToCamera, 0.001));

                // Из пикселей в clip space: по высоте кадра это доля, по X её делим на
                // соотношение сторон, иначе контур растянется вместе с кадром.
                float thickness = 2.0 * pixels / _ScreenParams.y;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                positionCS.xy += direction * float2(thickness / aspect, thickness) * fade * positionCS.w;

                // Отодвигаем оболочку от камеры. У Unity обратный Z, поэтому «дальше» —
                // это меньшее значение.
            #if UNITY_REVERSED_Z
                positionCS.z -= _OutlineDepthOffset;
            #else
                positionCS.z += _OutlineDepthOffset;
            #endif

                output.fade = fade;
            #else
                // Обводки у материала нет. Проход всё равно вызывается — отменить его умеет
                // только сам материал, — но рисовать модель второй раз незачем: схлопываем
                // оболочку в точку, как и при общем выключателе.
                output.positionCS = float4(0, 0, 0, 1);
                return output;
            #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

            #ifdef _OUTLINE_LIT
                output.positionWS = positionWS;

                // Оболочка — это ЗАДНИЕ грани, их нормали смотрят от нас. Для света берём
                // обратную: тогда контур освещается так же, как соседняя лицевая поверхность,
                // а не наоборот.
                half3 litNormalWS = -normalWS;

                output.normalWS = litNormalWS;

                // Окружающий свет — в вершине, ровно как в основном проходе: развёртка
                // сферических гармоник это десятки операций, и платить их за каждый пиксель
                // контура незачем. Результат тот же.
                output.ambient = SampleSH(litNormalWS);
            #endif

                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
            #ifdef _OUTLINE_ON
                clip(input.fade - 0.001);

            // Обрезке и решету нужна одна и та же альфа, поэтому текстура читается ОДИН раз
            // на обе проверки: две выборки одного и того же тексела отличались бы только
            // ценой.
            #if defined(_ALPHATEST_ON) || defined(_DITHER_ON)
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a;
            #endif

            #ifdef _ALPHATEST_ON
                // Без этого у травы и листвы контур обводит прямоугольник карты, а не сам лист.
                clip(alpha - _Cutoff);
            #endif

            #ifdef _DITHER_ON
                // Контур растворяем вместе с телом: сплошная рамка вокруг полурастворённого
                // объекта выглядит как ошибка.
                clip(DitherOpacity(alpha) - DitherThreshold(input.positionCS.xy));
            #endif

                half3 outline = _OutlineColor.rgb;

            #ifdef _OUTLINE_LIT
                half3 normalWS = normalize(input.normalWS);

                ToonShadowSettings shadowSettings = GetToonShadowSettings();

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight();

                // Тот же расчёт, что и у заливки: иначе контур и поверхность уходят в тень
                // на разных пикселях, и по силуэту идёт рваная кайма.
                half attenuation = ToonMainLightShadow(shadowCoord, input.positionWS, shadowSettings);
                half shade = ToonShade(attenuation, shadowSettings);

                half ndotl = saturate(dot(normalWS, mainLight.direction));

                // На свету контур забирает оттенок солнца, в тени — оттенок теней заливки.
                // Множителем, а не заменой: свой цвет обводки должен оставаться узнаваемым.
                // Складываем так же, как заливка, — иначе при слабом солнце контур уходит
                // в чёрный, пока поверхность рядом светлеет, и по силуэту идёт разнобой.
                half3 ambientLevel = input.ambient;

                half3 lit = outline * (ambientLevel + mainLight.color * lerp(0.6h, 1.0h, ndotl));
                half3 dark = outline * ambientLevel * ToonShadowTint(shadowSettings);

                outline = lerp(outline, lerp(dark, lit, shade), _OutlineLightInfluence);
                outline *= ToonShadowDarkening(shade, shadowSettings);
            #endif

                // Контур гасим в цвет вспышки вместе с заливкой: иначе на белом силуэте
                // остаётся тёмная рамка и удар читается как смена текстуры, а не как вспышка.
                return half4(lerp(outline, _HitFlashColor.rgb, saturate(_HitFlash)), _OutlineColor.a);
            #else
                clip(-1);
                return 0;
            #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Служебные проходы: без них объект не отбрасывает тень и выпадает
        // из depth/normals, на которые опираются эффекты URP.
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma target 3.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma target 3.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"

    // Свой инспектор: он выставляет смешивание, запись глубины и очередь по режиму
    // поверхности. Без него поле Surface Type переключается впустую.
    CustomEditor "FeatherKit.EditorTools.ToonLitShaderGUI"
}
