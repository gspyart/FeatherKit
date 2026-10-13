using UnityEngine;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Уровень качества тумана. Пресет задаёт всё, от чего зависит цена, разом;
    /// <see cref="Custom"/> — каждое число руками.
    /// </summary>
    public enum HeightFogQuality
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Custom = 3,
    }

    /// <summary>
    /// Во что обходится туман: разрешение, число шагов по лучу, сколько ламп светит в тумане,
    /// сколько шагов у их света и какие в тумане тени. Отвечает за то, что значит каждый
    /// пресет качества.
    /// </summary>
    internal readonly struct HeightFogQualitySettings
    {
        public readonly HeightFogDownscale Downscale;
        public readonly int StepCount;
        public readonly int MaxLamps;
        public readonly int LampStepCount;
        public readonly bool SunShadows;
        public readonly bool LampShadows;


        public HeightFogQualitySettings(HeightFogDownscale downscale, int stepCount, int maxLamps, int lampStepCount,
                                        bool sunShadows, bool lampShadows)
        {
            Downscale = downscale;
            StepCount = Mathf.Max(stepCount, 1);
            MaxLamps = Mathf.Clamp(maxLamps, 0, HeightFogLampsCollector.MaxLamps);
            LampStepCount = Mathf.Max(lampStepCount, 1);
            SunShadows = sunShadows;
            LampShadows = lampShadows;
        }


        // ── Пресеты ───────────────────────────────────────────────────────────

        // Низкое режет дорогое первым: тени ламп и разрешение. Высокое — полное
        // разрешение: на нём туман стоит вчетверо дороже среднего.
        public static HeightFogQualitySettings ForPreset(HeightFogQuality quality)
        {
            switch (quality)
            {
                case HeightFogQuality.Low:
                    return new HeightFogQualitySettings(HeightFogDownscale.X4, 8, 2, 4, true, false);

                case HeightFogQuality.High:
                    return new HeightFogQualitySettings(HeightFogDownscale.X1, 16, 8, 10, true, true);

                default:
                    return new HeightFogQualitySettings(HeightFogDownscale.X2, 12, 4, 6, true, true);
            }
        }
    }
}
