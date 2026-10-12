using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Трёхмерный шум, который повторяется без шва: значения у одной грани куба продолжаются
    /// с противоположной. Нужен текстурам, которые кладутся плиткой в мир, — иначе на стыке
    /// плиток виден срез. Сюда входит только математика шума: что из него печь, решает
    /// тот, кто зовёт.
    ///
    /// Точка подаётся в долях куба, от нуля до единицы; период — сколько ячеек шума
    /// укладывается на сторону куба.
    /// </summary>
    internal static class TileableNoise3D
    {
        // Двенадцать направлений к серединам рёбер куба — классический набор Перлина:
        // равномерный по сторонам и без лишней арифметики.
        private static readonly Vector3[] Gradients =
        {
            new Vector3(1, 1, 0), new Vector3(-1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, -1, 0),
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1),
            new Vector3(0, 1, 1), new Vector3(0, -1, 1), new Vector3(0, 1, -1), new Vector3(0, -1, -1),
        };


        // ── Шумы ──────────────────────────────────────────────────────────────

        // Плавные холмы, примерно от -1 до 1.
        public static float Perlin(Vector3 point, int period, int seed)
        {
            var scaled = point * period;
            var cell = new Vector3Int(Mathf.FloorToInt(scaled.x), Mathf.FloorToInt(scaled.y), Mathf.FloorToInt(scaled.z));
            var offset = scaled - (Vector3)cell;
            var fade = new Vector3(Fade(offset.x), Fade(offset.y), Fade(offset.z));

            float Corner(int x, int y, int z)
            {
                var gradient = Gradients[Hash(Wrap(cell.x + x, period), Wrap(cell.y + y, period), Wrap(cell.z + z, period), seed) % 12];

                return Vector3.Dot(gradient, offset - new Vector3(x, y, z));
            }

            var bottom = Mathf.Lerp(Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), fade.x),
                                    Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), fade.x), fade.y);

            var top = Mathf.Lerp(Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), fade.x),
                                 Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), fade.x), fade.y);

            return Mathf.Lerp(bottom, top, fade.z);
        }

        // Сглаживание внутри ячейки: без него видны грани сетки.
        private static float Fade(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }


        // Клубы: единица в центрах, к границам между ними спадает к нулю.
        public static float Worley(Vector3 point, int period, int seed)
        {
            var scaled = point * period;
            var cell = new Vector3Int(Mathf.FloorToInt(scaled.x), Mathf.FloorToInt(scaled.y), Mathf.FloorToInt(scaled.z));
            var nearest = float.MaxValue;

            for (var z = -1; z <= 1; z++)
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                var neighbour = cell + new Vector3Int(x, y, z);
                var center = (Vector3)neighbour + RandomInsideCell(Wrap(neighbour.x, period), Wrap(neighbour.y, period), Wrap(neighbour.z, period), seed);

                nearest = Mathf.Min(nearest, (scaled - center).sqrMagnitude);
            }

            return 1f - Mathf.Clamp01(Mathf.Sqrt(nearest));
        }

        // Где внутри ячейки стоит центр её клуба. Берётся по уже свёрнутому номеру ячейки,
        // поэтому у соседей через край куба центры те же — и шва нет.
        private static Vector3 RandomInsideCell(int x, int y, int z, int seed)
        {
            return new Vector3(Hash(x, y, z, seed) / (float)uint.MaxValue,
                               Hash(x, y, z, seed + 1) / (float)uint.MaxValue,
                               Hash(x, y, z, seed + 2) / (float)uint.MaxValue);
        }


        // ── Общие помощники ───────────────────────────────────────────────────

        // Номер ячейки по кругу: ячейка за краем куба — это ячейка с другого края.
        private static int Wrap(int value, int period)
        {
            var wrapped = value % period;

            return wrapped < 0 ? wrapped + period : wrapped;
        }

        // Случайное, но всегда одно и то же число для ячейки и зерна.
        private static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                var hash = (uint)seed * 0x9E3779B9u;

                hash ^= (uint)x * 0x85EBCA6Bu;
                hash = (hash << 13) | (hash >> 19);
                hash ^= (uint)y * 0xC2B2AE35u;
                hash = (hash << 17) | (hash >> 15);
                hash ^= (uint)z * 0x27D4EB2Fu;

                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;

                return hash;
            }
        }
    }
}
