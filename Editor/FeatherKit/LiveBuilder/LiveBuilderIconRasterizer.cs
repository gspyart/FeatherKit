using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки окна Live Builder из линий, без файлов картинок: ломаные в квадрате 24 × 24 (y вниз) превращаются в текстуру с гладкими краями.
    /// Здесь же — заготовки для рисунков: ломаная по числам, дуга, кривая. Сами рисунки — у инструментов (<see cref="LiveBuilderTool.IconLines"/>) и в <see cref="LiveBuilderOptionIcons"/>.
    /// Текстуры не сохраняются со сценой и сами не удаляются: перед перезагрузкой скриптов их удаляем здесь — запомненные значки к тому времени уже забыты.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderIconRasterizer
    {
        // Сторона текстуры, пикс. Вдвое больше значка на экране — чётко и на экранах с удвоенной плотностью.
        private const int TextureSize = 48;

        // Толщина линии в клетках сетки 24 × 24.
        private const float StrokeWidth = 1.6f;

        // Все созданные текстуры — удалить перед перезагрузкой скриптов.
        private static readonly List<Texture2D> CreatedTextures = new();


        static LiveBuilderIconRasterizer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DestroyCreatedTextures;
        }


        // ── Растеризация ─────────────────────────────────────────────────

        // Каждый пиксель — насколько он близок к ближайшей линии: так края сглажены без лесенки.
        public static Texture2D Rasterize(List<Vector2[]> lines, Color color)
        {
            var scale = TextureSize / 24f;
            var halfWidth = StrokeWidth * scale / 2f;
            var pixels = new Color32[TextureSize * TextureSize];

            for (var row = 0; row < TextureSize; row++)
                for (var column = 0; column < TextureSize; column++)
                {
                    // У текстуры строка 0 — внизу, у значка y идёт вниз.
                    var point = new Vector2(column + 0.5f, TextureSize - row - 0.5f) / scale;
                    var distance = DistanceToLines(point, lines) * scale;
                    var alpha = Mathf.Clamp01(halfWidth + 0.5f - distance);
                    pixels[row * TextureSize + column] = new Color(color.r, color.g, color.b, color.a * alpha);
                }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels32(pixels);
            texture.Apply();
            CreatedTextures.Add(texture);
            return texture;
        }

        private static float DistanceToLines(Vector2 point, List<Vector2[]> lines)
        {
            var best = float.MaxValue;

            foreach (var line in lines)
            {
                // Одна точка — это точка-кружок толщиной в линию.
                if (line.Length == 1)
                    best = Mathf.Min(best, Vector2.Distance(point, line[0]));

                for (var index = 0; index < line.Length - 1; index++)
                {
                    var start = line[index];
                    var along = line[index + 1] - start;
                    var fraction = along.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, along) / along.sqrMagnitude) : 0f;
                    best = Mathf.Min(best, Vector2.Distance(point, start + along * fraction));
                }
            }

            return best;
        }


        // Перед перезагрузкой скриптов: словари значков забудутся, а текстуры остались бы висеть в памяти.
        private static void DestroyCreatedTextures()
        {
            foreach (var texture in CreatedTextures)
                if (texture != null)
                    Object.DestroyImmediate(texture);

            CreatedTextures.Clear();
        }


        // ── Заготовки рисунков ───────────────────────────────────────────

        // Ломаная по парам чисел: x1, y1, x2, y2…
        public static Vector2[] Points(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];

            for (var index = 0; index < points.Length; index++)
                points[index] = new Vector2(coordinates[index * 2], coordinates[index * 2 + 1]);

            return points;
        }


        // Прямоугольник от левого верхнего угла до правого нижнего.
        public static Vector2[] Rectangle(float left, float top, float right, float bottom)
        {
            return Points(left, top, right, top, right, bottom, left, bottom, left, top);
        }


        // Дуга окружности от угла до угла, градусы; 0 — вправо, 90 — вниз (y значка идёт вниз). 0 → 360 — круг.
        public static Vector2[] Arc(Vector2 center, float radius, float fromDegrees, float toDegrees)
        {
            const int steps = 24;
            var points = new Vector2[steps + 1];

            for (var step = 0; step <= steps; step++)
            {
                var angle = Mathf.Lerp(fromDegrees, toDegrees, step / (float)steps) * Mathf.Deg2Rad;
                points[step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return points;
        }


        // Кривая Безье по четырём точкам: начало, два уса, конец.
        public static Vector2[] Curve(Vector2 start, Vector2 startHandle, Vector2 endHandle, Vector2 end)
        {
            const int steps = 20;
            var points = new Vector2[steps + 1];

            for (var step = 0; step <= steps; step++)
            {
                var t = step / (float)steps;
                var inverse = 1f - t;
                points[step] = inverse * inverse * inverse * start + 3f * inverse * inverse * t * startHandle
                             + 3f * inverse * t * t * endHandle + t * t * t * end;
            }

            return points;
        }


        // Точка-кружок толщиной в линию.
        public static Vector2[] Dot(float x, float y)
        {
            return new[] { new Vector2(x, y) };
        }
    }
}
