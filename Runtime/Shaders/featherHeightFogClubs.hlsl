// Клубы тумана: 3D-текстура шума, которая плывёт по ветру и делает туман то гуще,
// то реже. Крупные клубы — одна выборка, мелкие поверх них — ещё одна, по желанию.
//
// Общий туман и каждая область тумана клубятся по-своему: масштаб, ветер и сила
// приходят снаружи, текстура — одна на всех.
#ifndef FEATHER_HEIGHT_FOG_CLUBS_INCLUDED
#define FEATHER_HEIGHT_FOG_CLUBS_INCLUDED

#if defined(_HEIGHT_FOG_NOISE_VOLUME) || defined(_HEIGHT_FOG_NOISE_DETAIL)
    #define HEIGHT_FOG_HAS_CLUBS
#endif

TEXTURE3D(_HeightFogNoiseVolume);
SAMPLER(sampler_HeightFogNoiseVolume);

// Клубы в точке: от −1 до 1, в среднем ноль. Без текстуры клубов нет — туман ровный.
//   wind — куда плывут клубы, м/с; scale — масштаб крупных клубов;
//   detailScale и detailStrength — масштаб и сила мелких клубов поверх.
float SampleFogClubs(float3 worldPoint, float3 wind, float scale, float detailScale, float detailStrength)
{
    #if defined(HEIGHT_FOG_HAS_CLUBS)
        float3 drifted = worldPoint - wind * _Time.y;
        float clubs = SAMPLE_TEXTURE3D_LOD(_HeightFogNoiseVolume, sampler_HeightFogNoiseVolume, drifted * scale, 0).r * 2.0 - 1.0;

        #if defined(_HEIGHT_FOG_NOISE_DETAIL)
            // Мелкие клубы просят не все: у кого их нет, выборку не тратим.
            UNITY_BRANCH
            if (detailStrength > 0.0)
            {
                float detail = SAMPLE_TEXTURE3D_LOD(_HeightFogNoiseVolume, sampler_HeightFogNoiseVolume, drifted * detailScale, 0).r * 2.0 - 1.0;

                clubs += detail * detailStrength;
            }
        #endif

        return clubs;
    #else
        return 0.0;
    #endif
}

#endif
