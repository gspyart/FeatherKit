using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace FeatherKit.Logging
{
    /// <summary>
    /// Лог из кусков, у каждого свой цвет. Нужен затем, что в консоли важное тонет:
    /// одноцветную строку приходится вычитывать, а цветную видно боковым зрением.
    ///
    /// <code>
    /// FLog.Info("Объект ", (target.name, FLogColor.Accent), " убран, осталось ", (left.ToString(), FLogColor.Value));
    /// FLog.Warning(this, "Пул переполнен");
    /// </code>
    ///
    /// Info и Warning вырезаются из релизной сборки целиком — вместе с вычислением аргументов,
    /// так что за строки в них можно не бояться. Error остаётся всегда: он про то, что
    /// сломалось у игрока.
    /// </summary>
    public static class FLog
    {
        private const string EditorOnly = "UNITY_EDITOR";
        private const string DevelopmentOnly = "DEVELOPMENT_BUILD";

        // Один буфер на все вызовы: логи зовут часто, а собирать строку каждый раз заново — мусор.
        private static readonly StringBuilder Builder = new StringBuilder(128);


        [Conditional(EditorOnly), Conditional(DevelopmentOnly)]
        public static void Info(params FLogPart[] parts)
        {
            Debug.Log(Compose(parts));
        }

        /// <summary>Тот же лог, но с объектом: клик по строке в консоли подсветит его в иерархии.</summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly)]
        public static void Info(Object context, params FLogPart[] parts)
        {
            Debug.Log(Compose(parts), context);
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly)]
        public static void Warning(params FLogPart[] parts)
        {
            Debug.LogWarning(Compose(parts));
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly)]
        public static void Warning(Object context, params FLogPart[] parts)
        {
            Debug.LogWarning(Compose(parts), context);
        }

        public static void Error(params FLogPart[] parts)
        {
            Debug.LogError(Compose(parts));
        }

        public static void Error(Object context, params FLogPart[] parts)
        {
            Debug.LogError(Compose(parts), context);
        }


        private static string Compose(FLogPart[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            Builder.Clear();

            for (var i = 0; i < parts.Length; i++)
                Append(parts[i]);

            return Builder.ToString();
        }

        private static void Append(FLogPart part)
        {
            if (string.IsNullOrEmpty(part.Text))
                return;

            // Цвет консоль понимает только тегом. В билде теги видны как текст, но туда
            // доходит один Error, где читаемость важнее чистоты.
            if (!part.HasColor)
            {
                Builder.Append(part.Text);
                return;
            }

            Builder.Append("<color=#")
                .Append(ColorUtility.ToHtmlStringRGB(part.Color))
                .Append('>')
                .Append(part.Text)
                .Append("</color>");
        }
    }
}
