using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Текстура в подписях построек — материальная половина: от неё меш заливается заново, поверхности и решатель не пересчитываются.
    /// - подгонки, выравнивание World и плитки материалов — только не по умолчанию: подписи старых построек прежние
    /// - мировой ключ — сдвиг постройки от мира по модулю плитки, в конце подписи до вырезов: сдвиг на целую плитку не пересобирает,
    ///   а черновая сборка его не видит — пересобирает только чистовая после отпускания
    /// </summary>
    public static class LiveBuilderTextureSignature
    {
        private const string WorldKeyPrefix = "|w";

        // Начало части вырезов в подписи (<see cref="GeometryCutterLookup"/>): мировой ключ стоит перед ней.
        private const string CutterKeyPrefix = "|x";

        // Шаг округления сдвигов, м — как у остальных подписей: 0,1 мм.
        private const float QuantizationStep = 0.0001f;


        // ── Материальная половина ────────────────────────────────────────

        // Подгонка «как есть» или её нет — один и тот же 0.
        public static void AppendFit(ref Hash128 hash, LiveBuilderTextureFit fit)
        {
            if (fit == null || fit.IsDefault)
            {
                hash.Append(0);
                return;
            }

            hash.Append(1);
            LiveBuilderStableHash.AppendQuantized(ref hash, fit.OffsetMeters.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, fit.OffsetMeters.y);
            LiveBuilderStableHash.AppendQuantized(ref hash, fit.Scale.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, fit.Scale.y);
            hash.Append(fit.QuarterTurns);
        }


        // Список подгонок постройки — только если не пуст.
        public static void AppendFits<TPlace>(ref Hash128 hash, LiveBuilderTextureFitList<TPlace> list) where TPlace : struct, System.Enum
        {
            if (list == null || list.IsEmpty)
                return;

            foreach (var entry in list.Entries)
            {
                hash.Append(System.Convert.ToInt32(entry.Place));
                AppendFit(ref hash, entry.Fit);
            }
        }


        // Выравнивание — только World: у Object подпись прежняя.
        public static void AppendAlignment(ref Hash128 hash, LiveBuilderTextureAlignment alignment)
        {
            if (alignment != LiveBuilderTextureAlignment.Object)
                hash.Append(1000 + (int)alignment);
        }


        // Плитка материала — только если её задали в настройках проекта.
        public static void AppendTile(ref Hash128 hash, Material material)
        {
            if (!LiveBuilderMaterialTiling.HasOwnSize(material))
                return;

            var size = LiveBuilderMaterialTiling.SizeOf(material);
            hash.Append(BoxModelSideMaterials.KeyOf(material));
            LiveBuilderStableHash.AppendQuantized(ref hash, size.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, size.y);
        }


        // ── Мировой ключ ─────────────────────────────────────────────────

        // Чистовая — нынешний сдвиг; черновая — тот, по которому собрана постройка: перенос в жесте её не пересобирает.
        public static string WorldKeyFor(LiveBuilderBuildQuality quality, string builtSignature, System.Func<string> currentKey)
        {
            return quality == LiveBuilderBuildQuality.Final ? currentKey() : WorldKeyIn(builtSignature);
        }


        // Мировой ключ внутри готовой подписи; нет — пусто.
        public static string WorldKeyIn(string signature)
        {
            if (string.IsNullOrEmpty(signature))
                return "";

            var start = signature.IndexOf(WorldKeyPrefix, System.StringComparison.Ordinal);

            if (start < 0)
                return "";

            var end = signature.IndexOf(CutterKeyPrefix, start, System.StringComparison.Ordinal);
            return end >= 0 ? signature.Substring(start, end - start) : signature.Substring(start);
        }


        // Сдвиги постройки по модулю каждого периода рисунка; Object — пусто, подпись прежняя.
        public static string WorldKeyOf(LiveBuilderTextureSpace space, LiveBuilderTexturePeriods periods, IReadOnlyList<float> shifts)
        {
            if (!space.IsWorld)
                return "";

            var hash = new Hash128();

            foreach (var periodSteps in periods.Steps)
                foreach (var shift in shifts)
                {
                    var steps = (long)System.Math.Round(shift / QuantizationStep);
                    var wrapped = periodSteps > 0 ? ((steps % periodSteps) + periodSteps) % periodSteps : steps;
                    hash.Append(ref wrapped);
                }

            return WorldKeyPrefix + hash;
        }


        // Сдвиги по трём осям постройки — у объекта из коробок.
        public static float[] AxisShiftsOf(LiveBuilderTextureSpace space)
        {
            return new[] { space.ShiftAlongAxis(0), space.ShiftAlongAxis(1), space.ShiftAlongAxis(2) };
        }
    }


    /// <summary>Через сколько метров повторяется рисунок постройки по осям её граней — все разные, по плиткам и подгонкам её мест.</summary>
    public sealed class LiveBuilderTexturePeriods
    {
        private const float QuantizationStep = 0.0001f;

        private readonly SortedSet<long> steps = new();


        public IEnumerable<long> Steps => steps;


        public void Add(LiveBuilderTextureFit fit, Material material)
        {
            var period = LiveBuilderTextureMapping.PeriodOf(fit, LiveBuilderMaterialTiling.SizeOf(material));
            steps.Add((long)System.Math.Round(period.x / QuantizationStep));
            steps.Add((long)System.Math.Round(period.y / QuantizationStep));
        }
    }
}
