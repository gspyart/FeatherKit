// ═══════════════════════════════════════════════════════════════════════════════════════
// ПАМЯТКА ТОМУ, КТО БУДЕТ ЭТО ПРАВИТЬ — ЧЕЛОВЕКУ ИЛИ АГЕНТУ
//
// ЧТО ЭТО. Земля из четырёх текстур, которые перетекают друг в друга. Какой слой где —
// сказано в ВЕРШИННОМ ЦВЕТЕ меша, его рисуют кистью Polybrush прямо в сцене.
//
// ЧЁРНАЯ ВЕРШИНА — ЭТО БАЗОВЫЙ СЛОЙ. Красный, зелёный и синий подмешивают слои 1, 2 и 3,
// а базовому достаётся остаток от единицы.
//
// БЕЛАЯ ВЕРШИНА — ТОЖЕ БАЗОВЫЙ СЛОЙ. Меш, которому вершинный цвет не красили вовсе,
// приходит белым, и по общему правилу это значило бы «все три слоя разом» — кашу.
// Поэтому белизна читается как «не закрашено»: непокрашенная земля показывает базу
// и выглядит правильно сразу, без обязательной заливки. Но как только красить начали,
// меш надо залить ЧЁРНЫМ: полубелая вершина белизной уже не считается и снова даёт кашу.
//
// АЛЬФА ВЕРШИНЫ НЕ ЧИТАЕТСЯ. Она свободна под будущее (запечённое затемнение у стен).
// Учтите только, что кисть красит все четыре канала одним мазком: любой слой,
// нарисованный поверх, затрёт и то, что лежало в альфе.
//
// РАЗВЁРТКИ НЕТ. UV берутся от мировых X и Z, размер тайла задаётся в метрах. Поэтому
// куски земли стыкуются бесшовно и разворачивать их руками не надо. Обратная сторона:
// на отвесной стене текстура растянется в полосы — для стен нужен обычный Toon Lit.
//
// ВЫСОТА ЛЕЖИТ В АЛЬФЕ ТЕКСТУРЫ СЛОЯ. По ней слои спорят за границу, и она же экономит
// четыре выборки. Нет высоты (альфа единица) — за границу спорят одни веса, и переход
// решают ширина полосы и шум.
//
// СИЛА СЛОЯ — ЭТО ЕГО ВЕС, ПОДНЯТЫЙ ВЫСОТОЙ И РАЗОРВАННЫЙ ШУМОМ. Вес стоит МНОЖИТЕЛЕМ
// у обоих, и это главная страховка: у незакрашенного слоя сила ноль, что бы ни лежало
// в его высоте и в его шуме.
//
// ПОЛОСА СПОРА — ДОЛЯ ОТ ПОБЕДИТЕЛЯ, а не вычитание из него, и это не мелочь. Вычитанием
// порог уходил в МИНУС, едва победитель оказывался слабее самой полосы, — и слой
// с нулевым весом получал видимость. На вершине, где сошлись три слоя по трети, четвёртый,
// которым не водили вовсе, вылезал на десятую часть. Доля отрицательной не бывает,
// поэтому незакрашенный слой теперь не может появиться в принципе.
//
// Заодно ширина перестала зависеть от того, какие числа лежат в высотах и в шуме,
// и читается одной фразой: слой видно, если он в пределах такой-то доли от победителя.
// Единица — обычное плавное смешивание текстур по весам, ноль — рубленый край.
//
// ШУМ ЛОМАЕТ ГРАНИЦУ. Границу слоёв задают вершины, и на редкой сетке она выходит прямой.
// Правит её РАЗРЫВ: он рвёт кромку слоя на островки.
//
// РАЗРЫВ — МНОЖИТЕЛЬ К СИЛЕ СЛОЯ, а не добавка к его высоте. Добавкой его сила зависела
// от того, что лежит в альфе текстур, и предела у неё не было: шум перекрывал и веса,
// и высоты, и кромка ходила пятнами. Множитель ограничен по построению — единица силы
// означает ±половину, — поэтому ползунок можно тянуть далеко, не теряя предсказуемости.
// За двойкой нижняя половина шума упирается в пол, и кромка идёт дырами: это законный
// вид, но уже не «рваный край», а растворение.
//
// СТОРОНЫ у разрыва НЕТ, и это решение, а не недоделка. Мысль напрашивается сама:
// считать шум не от середины, а от края, чтобы кромку грызло только в соседа или только
// в себя. Пробовали — в игре пошли пятна. Односторонняя поправка даёт слою ПОСТОЯННЫЙ
// перевес, а вершинный цвет размазан по всему треугольнику: немножко слоя лежит далеко
// за его мазком, и с перевесом он там побеждает — вдалеке от нарисованной границы.
// Симметричный шум этого не делает, потому что в среднем гасится.
//
// ШУМ РАБОТАЕТ ТОЛЬКО ТАМ, ГДЕ СЛОИ СПОРЯТ. В чистой заливке его не видно вовсе, и это
// не настройка, а следствие того, что доли делятся на сумму: единственный ненулевой слой
// получает единицу при любой своей силе. А вот «чистая заливка» — не то же самое, что
// «нарисовано ровно»: цвет размазан между вершинами, и на редкой сетке в любой точке
// треугольника лежит немножко соседнего слоя. Сильный разрыв в такой точке даёт соседу
// победить, и выглядит это как шум там, где границы не рисовали. Отсюда и предел
// у множителя: он держит кромку возле нарисованной линии.
//
// РАЗРЫВ СВОЙ У КАЖДОГО ЗАКРАШЕННОГО СЛОЯ: своя текстура, свой размер, своя сила.
// Один общий шум на всех вёл бы кромку гравия и кромку песка по одному узору, и это
// видно глазом. База шума не получает намеренно: границу решает РАЗНИЦА сил,
// и сдвинутые разом слои остались бы там же, где были.
//
// ПУСТОЙ СЛОТ ШУМА — ЭТО СЕРАЯ ЗАЛИВКА, то есть ровно ноль: слой просто остаётся
// без разрыва. Но выборка у него всё равно берётся. Не нужен разрыв вовсе — снимайте галку.
//
// ВЕТКУ ВОКРУГ ВЫБОРКИ СТАВИТЬ НЕЛЬЗЯ: выборка внутри ветки теряет производные, и текстура
// рассыпается на швах мип-уровней. Поэтому слои, которые материал использует, читаются все
// подряд, без проверок «а нарисован ли он здесь».
//
// СКОЛЬКО ИХ ЧИТАТЬ — другой вопрос, и решается он НЕ веткой, а ключевым словом: слоты 2 и 3
// отключаются в инспекторе (_TERRAIN_LAYERS_TWO и _TERRAIN_LAYERS_THREE), и тогда их выборок
// в скомпилированном варианте нет вовсе. Производные при этом целы — условие препроцессорное,
// до компиляции. Вес отключённого слоя уходит базовому, чтобы сумма осталась единицей.
// Материал без обоих слов читает все четыре, как раньше.
//
// СВЕТ ОБЩИЙ С Toon Lit — он в featherToonLighting.hlsl. Свойства света объявлены здесь
// по именам из договора в шапке того файла: пропущенное имя даёт ошибку компиляции.
//
// ОБВОДКИ ЗДЕСЬ НЕТ намеренно: контур обвёл бы каждый кусок земли по краю, и карта
// разошлась бы на лоскуты.
// ═══════════════════════════════════════════════════════════════════════════════════════

Shader "FeatherKit/Toon Terrain"
{
    Properties
    {
        // Насыщенность и яркость правят саму текстуру, до света. Своя пара у КАЖДОГО слоя:
        // слои приезжают из разных наборов, и общей правкой их друг к другу не свести —
        // она сдвинула бы все четыре разом. Цвет слоя так не умеет: он только умножает.

        [NoScaleOffset] _Layer0Map ("Base Layer", 2D) = "white" {}
        _Layer0Color ("Base Layer Color", Color) = (1, 1, 1, 1)
        _Layer0Size ("Base Layer Tile (m)", Float) = 2
        _Layer0Saturation ("Base Layer Saturation", Range(0, 2)) = 1
        _Layer0Brightness ("Base Layer Brightness", Range(0, 2)) = 1

        [NoScaleOffset] _Layer1Map ("Layer 1 — Red", 2D) = "white" {}
        _Layer1Color ("Layer 1 Color", Color) = (1, 1, 1, 1)
        _Layer1Size ("Layer 1 Tile (m)", Float) = 2
        _Layer1Saturation ("Layer 1 Saturation", Range(0, 2)) = 1
        _Layer1Brightness ("Layer 1 Brightness", Range(0, 2)) = 1

        [NoScaleOffset] _Layer2Map ("Layer 2 — Green", 2D) = "white" {}
        _Layer2Color ("Layer 2 Color", Color) = (1, 1, 1, 1)
        _Layer2Size ("Layer 2 Tile (m)", Float) = 2
        _Layer2Saturation ("Layer 2 Saturation", Range(0, 2)) = 1
        _Layer2Brightness ("Layer 2 Brightness", Range(0, 2)) = 1

        [NoScaleOffset] _Layer3Map ("Layer 3 — Blue", 2D) = "white" {}
        _Layer3Color ("Layer 3 Color", Color) = (1, 1, 1, 1)
        _Layer3Size ("Layer 3 Tile (m)", Float) = 2
        _Layer3Saturation ("Layer 3 Saturation", Range(0, 2)) = 1
        _Layer3Brightness ("Layer 3 Brightness", Range(0, 2)) = 1

        // Доля от силы победителя, в пределах которой слой ещё виден. Ноль — рубленый
        // край, единица — плавное смешивание текстур по весам.
        _BlendWidth ("Blend Width", Range(0.001, 1)) = 0.15

        // ── Шум: рваная кромка, своя у каждого закрашенного слоя ──────────

        [Toggle(_TERRAIN_NOISE_BREAK)] _NoiseBreakEnabled ("Edge Break Enabled", Float) = 0

        [NoScaleOffset] _Layer1NoiseMap ("Layer 1 Noise", 2D) = "gray" {}
        _Layer1NoiseSize ("Layer 1 Noise Tile (m)", Float) = 4
        _Layer1NoiseBreak ("Layer 1 Edge Break", Range(0, 4)) = 0.35

        [NoScaleOffset] _Layer2NoiseMap ("Layer 2 Noise", 2D) = "gray" {}
        _Layer2NoiseSize ("Layer 2 Noise Tile (m)", Float) = 4
        _Layer2NoiseBreak ("Layer 2 Edge Break", Range(0, 4)) = 0.35

        [NoScaleOffset] _Layer3NoiseMap ("Layer 3 Noise", 2D) = "gray" {}
        _Layer3NoiseSize ("Layer 3 Noise Tile (m)", Float) = 4
        _Layer3NoiseBreak ("Layer 3 Edge Break", Range(0, 4)) = 0.35

        // ── Свет: имена из договора featherToonLighting.hlsl ──────────────

        // ДОБАВКА к общему оттенку тени из рендер-фичи, а не замена ему: они умножаются.
        // Отсюда белый по умолчанию — «своего оттенка нет, беру общий».
        _ShadowTint ("Shadow Tint", Color) = (1, 1, 1, 1)

        // Насколько на ЭТОЙ поверхности видна падающая тень. Единица — как задано в фиче,
        // ноль — падающей тени на земле нет вовсе. У земли это нужнее, чем у модели: кусок
        // ровного пола уходит в тень целиком, и рисунок слоёв на нём перестаёт читаться.
        _CastShadowOpacity ("Cast Shadow Opacity", Range(0, 1)) = 1
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.1
        _RampSmoothness ("Ramp Smoothness", Range(0.001, 0.5)) = 0.05
        _MidToneWidth ("Mid Tone Width", Range(0, 0.5)) = 0.15
        _MidToneStrength ("Mid Tone Strength", Range(0, 1)) = 0.35

        _ToonSpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecGloss ("Specular Size", Range(1, 256)) = 48
        _SpecIntensity ("Specular Intensity", Range(0, 1)) = 0

        // Ободок по умолчанию выключен: у плоской земли угол взгляда одинаков везде,
        // и он подсветил бы не край, а всю поверхность целиком.
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.7
        _RimSmoothness ("Rim Smoothness", Range(0.001, 0.5)) = 0.08
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
            half4 _Layer0Color;
            half4 _Layer1Color;
            half4 _Layer2Color;
            half4 _Layer3Color;
            float _Layer0Size;
            float _Layer1Size;
            float _Layer2Size;
            float _Layer3Size;
            half _Layer0Saturation;
            half _Layer1Saturation;
            half _Layer2Saturation;
            half _Layer3Saturation;
            half _Layer0Brightness;
            half _Layer1Brightness;
            half _Layer2Brightness;
            half _Layer3Brightness;
            half _BlendWidth;
            float _Layer1NoiseSize;
            float _Layer2NoiseSize;
            float _Layer3NoiseSize;
            half _Layer1NoiseBreak;
            half _Layer2NoiseBreak;
            half _Layer3NoiseBreak;
            half4 _ShadowTint;
            half _CastShadowOpacity;
            half _RampThreshold;
            half _RampSmoothness;
            half _MidToneWidth;
            half _MidToneStrength;
            half4 _ToonSpecColor;
            half _SpecGloss;
            half _SpecIntensity;
            half4 _RimColor;
            half _RimAmount;
            half _RimThreshold;
            half _RimSmoothness;
        CBUFFER_END

        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherToonLighting.hlsl"

        // Правка текстуры лежит в общем файле: ею же правятся модели, и разъехаться
        // им нельзя — стык модели с землёй виден сразу.
        #include "Packages/com.gspy.featherkit/Runtime/Shaders/featherToonColor.hlsl"

        TEXTURE2D(_Layer0Map); SAMPLER(sampler_Layer0Map);
        TEXTURE2D(_Layer1Map); SAMPLER(sampler_Layer1Map);
        TEXTURE2D(_Layer2Map); SAMPLER(sampler_Layer2Map);
        TEXTURE2D(_Layer3Map); SAMPLER(sampler_Layer3Map);
        TEXTURE2D(_Layer1NoiseMap); SAMPLER(sampler_Layer1NoiseMap);
        TEXTURE2D(_Layer2NoiseMap); SAMPLER(sampler_Layer2NoiseMap);
        TEXTURE2D(_Layer3NoiseMap); SAMPLER(sampler_Layer3NoiseMap);

        // Сколько верхних слоёв материал вообще использует. Слоты, до которых дело
        // не дошло, не читаются: четыре выборки на каждый пиксель земли — самая дорогая
        // часть этого шейдера, а земля занимает пол-экрана.
        //
        // Ключевых слов ДВА, а не три: «четыре слоя» — это отсутствие обоих. Материал,
        // который про них ничего не знает, ведёт себя ровно как раньше.
        #if defined(_TERRAIN_LAYERS_TWO)
            #define TERRAIN_LAYER_COUNT 2
        #elif defined(_TERRAIN_LAYERS_THREE)
            #define TERRAIN_LAYER_COUNT 3
        #else
            #define TERRAIN_LAYER_COUNT 4
        #endif

        // Разрыв кромки одного слоя: поправка к его силе, вокруг нуля. Читается только
        // красный канал: цветной шум здесь не нужен, у каждого слоя своя текстура.
        // Серая заливка даёт ровно ноль, поэтому пустой слот оставляет слой без разрыва.
        //
        // Сила складывается сразу здесь, а не у места применения: тогда «сколько шума»
        // и «насколько он влияет» лежат в одном выражении, а наружу выходит готовая поправка.
        //
        // Отсчёт идёт РОВНО ОТ СЕРЕДИНЫ, и сдвигать её нечем намеренно. Сдвинутая середина
        // даёт слою постоянный перевес, а вершинный цвет размазан по всему треугольнику:
        // немножко слоя лежит далеко за его мазком, и с перевесом он там побеждает пятнами.
        half SampleEdgeBreak(TEXTURE2D_PARAM(noiseMap, noiseSampler),
                             float2 positionXZ, float tileSize, half amount)
        {
            float2 uv = positionXZ / max(tileSize, 0.001);

            return (SAMPLE_TEXTURE2D(noiseMap, noiseSampler, uv).r - 0.5h) * amount;
        }

        // Одна выборка слоя. UV берутся от мировых X и Z, поэтому размер тайла задаётся
        // в метрах и соседние куски земли продолжают рисунок друг друга.
        // В rgb цвет, в альфе высота.
        half4 SampleTerrainLayer(TEXTURE2D_PARAM(layerMap, layerSampler),
                                 float2 positionXZ, float tileSize,
                                 half saturation, half brightness)
        {
            float2 uv = positionXZ / max(tileSize, 0.001);
            half4 layer = SAMPLE_TEXTURE2D(layerMap, layerSampler, uv);

            // Правится только цвет: в альфе лежит высота, по ней слои спорят за границу.
            layer.rgb = ToonAdjustTextureColor(layer.rgb, saturation, brightness);

            return layer;
        }

        // Вес отключённых слоёв отдаём базовому. Просто занулить его нельзя: сумма весов
        // должна остаться единицей, иначе закрашенные места потемнеют. И нельзя занулять
        // вершинный цвет ДО расчёта весов — белая вершина означает «не крашено», и после
        // зануления она перестала бы такой быть.
        half4 TerrainUsedLayers(half4 weights)
        {
        #if TERRAIN_LAYER_COUNT < 4
            weights.x += weights.w;
            weights.w = 0.0h;
        #endif

        #if TERRAIN_LAYER_COUNT < 3
            weights.x += weights.z;
            weights.z = 0.0h;
        #endif

            return weights;
        }

        // Вес каждого слоя из вершинного цвета. Базовому достаётся остаток от единицы,
        // поэтому чёрная вершина — это чистый базовый слой, и красить нужно только то,
        // что лежит поверх него.
        //
        // Белая вершина тоже даёт базу: меш без вершинных цветов Unity отдаёт единицей
        // во всех каналах, и без этой оговорки непокрашенная земля была бы кашей
        // из трёх слоёв разом. Порог узкий и с запасом — цепляет только саму белизну.
        half4 TerrainLayerWeights(half4 vertexColor)
        {
            half unpainted = smoothstep(2.9h, 2.995h,
                                        vertexColor.r + vertexColor.g + vertexColor.b);

            half3 layers = vertexColor.rgb * (1.0h - unpainted);
            half painted = layers.r + layers.g + layers.b;

            return half4(saturate(1.0h - painted), layers);
        }

        // Сила слоя — то, чем он спорит за кромку: ВЕС, поднятый высотой и разорванный шумом.
        //
        // Вес стоит МНОЖИТЕЛЕМ у обоих, а не слагаемым, и это главная страховка:
        // у незакрашенного слоя вес ноль, и ни карта высот, ни шум его не поднимут.
        // Слагаемым слой с высокой картой вылезал бы там, где его не рисовали вовсе.
        //
        // У множителя разрыва есть ПОЛ, и ноль там нельзя. На силе больше двойки нижняя
        // половина шума уводила множитель в минус, и в ЧИСТОЙ заливке обнулялись все силы
        // разом, а с ними и сумма: пиксель уходил в чёрный на четверти площади шума.
        // С полом слой остаётся виден сам по себе, а в споре всё так же проигрывает соседу
        // начисто — дырой в кромке.
        half4 TerrainLayerStrength(half4 weights, half4 heights, half4 edgeBreak)
        {
            return weights * (1.0h + heights) * max(1.0h + edgeBreak, 0.001h);
        }

        // Кто виден на границе слоёв. Усреднение дало бы мыльную полосу поперёк перехода,
        // поэтому побеждает тот, чья сила больше: гравий пробивается сквозь траву
        // отдельными островками, и граница остаётся живой даже на редкой сетке вершин.
        //
        // Порог — ДОЛЯ от победителя. Вычитанием он уходил в минус, едва победитель
        // оказывался слабее самой полосы, и слой с нулевым весом получал видимость; доля
        // отрицательной не бывает. Она же делает ширину независимой от того, какие числа
        // пришли в силах.
        //
        // ГДЕ СЛОИ НЕ СПОРЯТ, ШУМ НЕ ВИДЕН ВОВСЕ, и это не настройка, а следствие деления
        // на сумму: единственный ненулевой слой получает единицу при любой своей силе.
        half4 TerrainEdgeBlend(half4 strength, half width)
        {
            half top = max(max(strength.x, strength.y), max(strength.z, strength.w));

            half4 visible = max(strength - top * (1.0h - width), 0.0h);
            half total = visible.x + visible.y + visible.z + visible.w;

            return visible / max(total, 0.0001h);
        }
        ENDHLSL

        // ------------------------------------------------------------------
        // Основной проход: смешивание слоёв и ступенчатый свет
        // ------------------------------------------------------------------
        Pass
        {
            Name "TerrainForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma target 3.0

            // Набор ключевых слов тот же, что у Toon Lit: лайтмапов, кук и SSAO
            // в проекте нет, а лишнее объявление удваивает число вариантов шейдера.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _TERRAIN_NOISE_BREAK

            // Сколько слоёв читать. Только фрагмент — раскладка Varyings от этого
            // не зависит, поэтому ГРАБЛИ 1 (расхождение этапов) здесь не грозят.
            #pragma shader_feature_local_fragment _ _TERRAIN_LAYERS_TWO _TERRAIN_LAYERS_THREE

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Тип ниже обязан остаться таким, какой он есть. Polybrush не разбирает
                // шейдер, а ищет в его ТЕКСТЕ объявление ровно этого типа с семантикой
                // цвета — регуляркой по файлу. Смените его на half4, и кисть начнёт
                // ругаться, что материал не поддерживает вершинный цвет. Красить при этом
                // можно, но человек поверит предупреждению и уйдёт искать поломку.
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Координата тумана едет в четвёртом компоненте мировой позиции, а не отдельным
            // интерполятором: слот всё равно передаётся целиком, а пустой w — это четверть
            // слота впустую на каждой вершине земли.
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionWSAndFog : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4 color       : TEXCOORD2;
                half3 ambient     : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings TerrainVertex(Attributes input)
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
                output.color = input.color;
                output.ambient = SampleSH(normals.normalWS);

                return output;
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = input.positionWSAndFog.xyz;
                float2 positionXZ = positionWS.xz;

                half4 layer0 = SampleTerrainLayer(TEXTURE2D_ARGS(_Layer0Map, sampler_Layer0Map),
                                                  positionXZ, _Layer0Size,
                                                  _Layer0Saturation, _Layer0Brightness);
                half4 layer1 = SampleTerrainLayer(TEXTURE2D_ARGS(_Layer1Map, sampler_Layer1Map),
                                                  positionXZ, _Layer1Size,
                                                  _Layer1Saturation, _Layer1Brightness);

                half4 layer2 = half4(0.0h, 0.0h, 0.0h, 0.0h);
                half4 layer3 = half4(0.0h, 0.0h, 0.0h, 0.0h);

            #if TERRAIN_LAYER_COUNT > 2
                layer2 = SampleTerrainLayer(TEXTURE2D_ARGS(_Layer2Map, sampler_Layer2Map),
                                            positionXZ, _Layer2Size,
                                            _Layer2Saturation, _Layer2Brightness);
            #endif

            #if TERRAIN_LAYER_COUNT > 3
                layer3 = SampleTerrainLayer(TEXTURE2D_ARGS(_Layer3Map, sampler_Layer3Map),
                                            positionXZ, _Layer3Size,
                                            _Layer3Saturation, _Layer3Brightness);
            #endif

                half4 weights = TerrainUsedLayers(TerrainLayerWeights(input.color));
                half4 heights = half4(layer0.a, layer1.a, layer2.a, layer3.a);

                // База разрыва не получает: границу решает РАЗНИЦА сил, и сдвинутые
                // разом слои остались бы там же, где были.
                half4 edgeBreak = half4(0.0h, 0.0h, 0.0h, 0.0h);

            #ifdef _TERRAIN_NOISE_BREAK
                edgeBreak.y = SampleEdgeBreak(TEXTURE2D_ARGS(_Layer1NoiseMap, sampler_Layer1NoiseMap),
                                              positionXZ, _Layer1NoiseSize, _Layer1NoiseBreak);

                #if TERRAIN_LAYER_COUNT > 2
                edgeBreak.z = SampleEdgeBreak(TEXTURE2D_ARGS(_Layer2NoiseMap, sampler_Layer2NoiseMap),
                                              positionXZ, _Layer2NoiseSize, _Layer2NoiseBreak);
                #endif

                #if TERRAIN_LAYER_COUNT > 3
                edgeBreak.w = SampleEdgeBreak(TEXTURE2D_ARGS(_Layer3NoiseMap, sampler_Layer3NoiseMap),
                                              positionXZ, _Layer3NoiseSize, _Layer3NoiseBreak);
                #endif
            #endif

                half4 strength = TerrainLayerStrength(weights, heights, edgeBreak);
                half4 blend = TerrainEdgeBlend(strength, _BlendWidth);

                half3 albedo = layer0.rgb * _Layer0Color.rgb * blend.x
                             + layer1.rgb * _Layer1Color.rgb * blend.y
                             + layer2.rgb * _Layer2Color.rgb * blend.z
                             + layer3.rgb * _Layer3Color.rgb * blend.w;

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionWS));

                half3 color = ToonLighting(albedo, input.ambient, positionWS, normalWS, viewDirWS,
                                           GetNormalizedScreenSpaceUV(input.positionCS));

                color = MixFog(color, input.positionWSAndFog.w);

                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Служебные проходы. Свои, а не готовые из URP: те тянут за собой _BaseMap
        // с обрезкой по альфе, а у земли ни того, ни другого нет.
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;

                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 DepthFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    CustomEditor "FeatherKit.EditorTools.ToonTerrainShaderGUI"
    FallBack "Universal Render Pipeline/Lit"
}
