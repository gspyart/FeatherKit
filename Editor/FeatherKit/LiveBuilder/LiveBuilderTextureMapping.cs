using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одна формула развёртки для всех построек: плоские метры грани → UV.
    /// - мировой сдвиг (только World), по модулю плитки — UV не уходит далеко от нуля
    /// - поворот на четверти оборота, потом сдвиг и масштаб подгонки (<see cref="LiveBuilderTextureFit"/>), делённые на метры плитки материала
    /// - всё по умолчанию (сдвига нет, подгонка «как есть», плитка 1 × 1) — ровно плоские метры, как до этапа 5
    /// - Fit: подгонка, при которой одна плитка ложится ровно на грань
    /// </summary>
    public static class LiveBuilderTextureMapping
    {
        // Масштаб ближе к нулю — ошибка ввода: считаем 1, иначе текстура ушла бы в бесконечность.
        private const float MinScale = 0.0001f;

        private static readonly LiveBuilderTextureFit DefaultFit = new();


        // planarMeters — метры по двум осям грани, worldShift — сдвиг этих осей до мира (Object — ноль), metersPerTile — плитка материала.
        public static Vector2 Map(Vector2 planarMeters, Vector2 worldShift, LiveBuilderTextureFit fit, Vector2 metersPerTile)
        {
            fit ??= DefaultFit;

            if (fit.IsDefault && worldShift == Vector2.zero && metersPerTile == LiveBuilderMaterialTiling.DefaultMetersPerTile)
                return planarMeters;

            var period = PeriodOf(fit, metersPerTile);
            var meters = planarMeters + new Vector2(Wrapped(worldShift.x, period.x), Wrapped(worldShift.y, period.y));
            var turned = Turned(meters, fit.QuarterTurns);
            var tile = ScaleOf(fit) * metersPerTile;
            return new Vector2((turned.x - fit.OffsetMeters.x) / tile.x, (turned.y - fit.OffsetMeters.y) / tile.y);
        }


        // Подгонка, при которой одна плитка ровно ложится на прямоугольник грани faceMin…faceMax (плоские метры), с этим поворотом.
        // Масштаб — сначала: от него период, по которому сворачивается мировой сдвиг.
        public static LiveBuilderTextureFit FitToFace(Vector2 faceMin, Vector2 faceMax, Vector2 worldShift, Vector2 metersPerTile, int quarterTurns)
        {
            var size = faceMax - faceMin;
            var turnedSize = quarterTurns % 2 == 0 ? size : new Vector2(size.y, size.x);
            var scale = new Vector2(ScaleFor(turnedSize.x, metersPerTile.x), ScaleFor(turnedSize.y, metersPerTile.y));
            var period = PeriodOf(new LiveBuilderTextureFit(Vector2.zero, scale, quarterTurns), metersPerTile);
            var shift = new Vector2(Wrapped(worldShift.x, period.x), Wrapped(worldShift.y, period.y));
            var first = Turned(faceMin + shift, quarterTurns);
            var second = Turned(faceMax + shift, quarterTurns);
            return new LiveBuilderTextureFit(Vector2.Min(first, second), scale, quarterTurns);
        }

        private static float ScaleFor(float faceMeters, float tileMeters)
        {
            var scale = Mathf.Abs(faceMeters) / Mathf.Max(Mathf.Abs(tileMeters), MinScale);
            return scale < MinScale ? 1f : scale;
        }


        // Через сколько метров по каждой плоской оси грани рисунок повторяется: после поворота ось U грани может стать V текстуры.
        public static Vector2 PeriodOf(LiveBuilderTextureFit fit, Vector2 metersPerTile)
        {
            var tile = ScaleOf(fit ?? DefaultFit) * metersPerTile;
            tile = new Vector2(Mathf.Abs(tile.x), Mathf.Abs(tile.y));
            return (fit ?? DefaultFit).QuarterTurns % 2 == 0 ? tile : new Vector2(tile.y, tile.x);
        }


        // Сдвиг на целую плитку рисунка не меняет — его отбрасываем.
        public static float Wrapped(float shift, float period)
        {
            if (shift == 0f || period <= 0f)
                return shift;

            return shift - period * Mathf.Floor(shift / period);
        }


        // Поворот против часовой стрелки на четверть оборота за шаг.
        private static Vector2 Turned(Vector2 meters, int quarterTurns)
        {
            return quarterTurns switch
            {
                1 => new Vector2(-meters.y, meters.x),
                2 => new Vector2(-meters.x, -meters.y),
                3 => new Vector2(meters.y, -meters.x),
                _ => meters
            };
        }

        private static Vector2 ScaleOf(LiveBuilderTextureFit fit)
        {
            var scale = fit.Scale;
            return new Vector2(Mathf.Abs(scale.x) < MinScale ? 1f : scale.x, Mathf.Abs(scale.y) < MinScale ? 1f : scale.y);
        }
    }
}
