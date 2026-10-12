using System.Threading.Tasks;
using FeatherKit.Rendering;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Запекает 3D-текстуру клубов для тумана по высоте (<see cref="HeightFogFeature"/>)
    /// и сохраняет её ассетом. Текстура повторяется без шва и кладётся в мир плиткой;
    /// по каждой стороне в ней <see cref="HeightFogFeature.NoiseVolumeSpotsPerTile"/>
    /// крупных клубов, а внутри — клубы помельче.
    ///
    /// Рисунок — «перлин-ворлей», как у облаков в играх: шум Ворлея даёт круглые клубы,
    /// шум Перлина рвёт их края, чтобы клубы не выглядели набором шариков.
    /// </summary>
    public static class HeightFogNoiseBaker
    {
        private const int Size = 128;
        private const int Seed = 1337;
        private const string DefaultName = "featherHeightFogNoise";

        // Сколько разбросов шума укладывается от нуля до единицы. Пять — почти весь шум
        // влезает без обрезки, и контраст остаётся живым.
        private const double BalancedSpreads = 5.0;


        // ── Запекание ─────────────────────────────────────────────────────────

        [MenuItem("FeatherKit/Запечь клубы тумана")]
        private static void BakeFromMenu()
        {
            var path = EditorUtility.SaveFilePanelInProject("Текстура клубов тумана", DefaultName, "asset",
                                                            "Куда сохранить 3D-текстуру клубов");

            if (!string.IsNullOrEmpty(path))
                Bake(path);
        }

        // Запекает текстуру в ассет по пути. Ассет, который там уже лежит, переписывается
        // на месте: ссылки на него из фичи рендера не слетают.
        public static void Bake(string assetPath)
        {
            Texture3D texture;

            try
            {
                EditorUtility.DisplayProgressBar("Текстура клубов тумана", "Считаю шум…", 0.5f);
                texture = CreateNoiseVolume();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath);

            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                Object.DestroyImmediate(texture);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AssetDatabase.CreateAsset(texture, assetPath);
            }

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath));
        }

        private static Texture3D CreateNoiseVolume()
        {
            var values = CalculateNoiseValues();
            var pixels = ToBalancedBytes(values);

            var texture = new Texture3D(Size, Size, Size, TextureFormat.R8, false)
            {
                name = DefaultName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            texture.SetPixelData(pixels, 0);
            texture.Apply(false);

            return texture;
        }

        // Значение шума в каждом вокселе. Слои по Z считаются параллельно: шум у вокселей
        // независим, а в один поток 128³ точек считались бы заметно дольше.
        private static float[] CalculateNoiseValues()
        {
            var values = new float[Size * Size * Size];
            var spots = HeightFogFeature.NoiseVolumeSpotsPerTile;

            Parallel.For(0, Size, z =>
            {
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    var point = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) / Size;

                    var perlin = (TileableNoise3D.Perlin(point, spots, Seed) * 0.5f
                                + TileableNoise3D.Perlin(point, spots * 2, Seed + 10) * 0.25f
                                + TileableNoise3D.Perlin(point, spots * 4, Seed + 20) * 0.125f) / 0.875f;

                    var worley = TileableNoise3D.Worley(point, spots, Seed + 30) * 0.625f
                               + TileableNoise3D.Worley(point, spots * 2, Seed + 40) * 0.25f
                               + TileableNoise3D.Worley(point, spots * 4, Seed + 50) * 0.125f;

                    values[x + Size * (y + Size * z)] = ErodeClumpsByPerlin(perlin * 0.5f + 0.5f, worley);
                }
            });

            return values;
        }

        // Клубы Ворлея, обкусанные Перлином: где Перлин низкий, от клуба остаётся меньше.
        private static float ErodeClumpsByPerlin(float perlin, float worley)
        {
            return (perlin - (worley - 1f)) / (2f - worley);
        }

        // Значения — в байты так, чтобы в среднем выходила ровно половина. Шейдер читает
        // половину как «густота без изменений», и сила клубов тогда не сдвигает общую
        // густоту тумана, а только раскачивает её вверх и вниз.
        //
        // Растягиваем по разбросу, а не по крайним значениям: редкие выбросы сжали бы весь
        // остальной рисунок к середине, и клубы вышли бы блёклыми.
        private static byte[] ToBalancedBytes(float[] values)
        {
            var mean = 0.0;

            foreach (var value in values)
                mean += value;

            mean /= values.Length;

            var variance = 0.0;

            foreach (var value in values)
                variance += (value - mean) * (value - mean);

            var spread = System.Math.Max(System.Math.Sqrt(variance / values.Length), 1e-5);
            var bytes = new byte[values.Length];

            for (var i = 0; i < values.Length; i++)
            {
                var balanced = 0.5 + (values[i] - mean) / (BalancedSpreads * spread);

                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01((float)balanced) * 255f);
            }

            return bytes;
        }
    }
}
