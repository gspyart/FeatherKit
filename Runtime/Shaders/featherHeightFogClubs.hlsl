// Клубы тумана: 3D-текстура шума, которая плывёт по ветру и делает туман то гуще,
// то реже. Крупные клубы — одна выборка, мелкие поверх них — ещё одна, по желанию.
//
// Общий туман и каждая область тумана клубятся по-своему: масштаб, ветер и сила
// приходят снаружи, текстура — одна на всех.
//
// Вдали — уменьшенная копия текстуры: выборка стоит за кусок мира размером с пиксель
// и с шаг по лучу, и клубы мельче этого куска усредняются, а не рябят. Где клубы
// усреднились почти в ноль, текстура не читается вовсе.
#ifndef FEATHER_HEIGHT_FOG_CLUBS_INCLUDED
#define FEATHER_HEIGHT_FOG_CLUBS_INCLUDED

#if defined(_HEIGHT_FOG_NOISE_VOLUME) || defined(_HEIGHT_FOG_NOISE_DETAIL)
    #define HEIGHT_FOG_HAS_CLUBS
#endif

// С этой копии клубы почти ровные: контраст в ней меньше десятой доли полного.
// На копию раньше клубы плавно гаснут — без кольца там, где выборка пропадает.
#define HEIGHT_FOG_CLUBS_FLAT_MIP 6.0

TEXTURE3D(_HeightFogNoiseVolume);
SAMPLER(sampler_HeightFogNoiseVolume);

float _HeightFogNoiseTexels;        // сколько текселей у текстуры клубов по стороне
float4 _HeightFogClubsFootprint;    // x размер пикселя на метр расстояния, y доля длины шага, z размер пикселя у ортокамеры, м

// Сколько метров мира стоит за одной выборкой клубов: пиксель на этом расстоянии
// или доля шага по лучу — что больше.
float FogClubsFootprint(float distance, float stepLength)
{
    return max(distance * _HeightFogClubsFootprint.x + _HeightFogClubsFootprint.z,
               stepLength * _HeightFogClubsFootprint.y);
}

// Один слой клубов, от −1 до 1, уже с копией под размер выборки.
float SampleFogClubsLayer(float3 drifted, float scale, float footprint)
{
    float mip = log2(max(footprint * scale * _HeightFogNoiseTexels, 1.0));
    float visibility = saturate(HEIGHT_FOG_CLUBS_FLAT_MIP - mip);
    float clubs = 0.0;

    UNITY_BRANCH
    if (visibility > 0.0)
        clubs = SAMPLE_TEXTURE3D_LOD(_HeightFogNoiseVolume, sampler_HeightFogNoiseVolume, drifted * scale, mip).r * 2.0 - 1.0;

    return clubs * visibility;
}

// Клубы в точке: от −1 до 1, в среднем ноль. Без текстуры клубов нет — туман ровный.
//   wind — куда плывут клубы, м/с; scale — масштаб крупных клубов;
//   detailScale и detailStrength — масштаб и сила мелких клубов поверх;
//   footprint — сколько метров мира стоит за выборкой, см. FogClubsFootprint.
float SampleFogClubs(float3 worldPoint, float3 wind, float scale, float detailScale, float detailStrength, float footprint)
{
    #if defined(HEIGHT_FOG_HAS_CLUBS)
        float3 drifted = worldPoint - wind * _Time.y;
        float clubs = SampleFogClubsLayer(drifted, scale, footprint);

        #if defined(_HEIGHT_FOG_NOISE_DETAIL)
            // Мелкие клубы просят не все: у кого их нет, выборку не тратим.
            UNITY_BRANCH
            if (detailStrength > 0.0)
                clubs += SampleFogClubsLayer(drifted, detailScale, footprint) * detailStrength;
        #endif

        return clubs;
    #else
        return 0.0;
    #endif
}

#endif
