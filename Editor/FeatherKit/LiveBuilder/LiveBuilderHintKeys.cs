using System.Collections.Generic;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Клавиши-плашки в строке подсказки внизу окна сцены: «Drag — box ⟨Enter⟩ done ⟨Esc⟩ cancel».
    /// - метит клавишу внутри обычной строки подсказки: инструменты и жесты так и отдают строку
    /// - подпись ярлыка — из его привязки в Shortcut Manager; Enter и Esc — постоянные: ярлыком их не сделать
    /// - режет строку на текст и плашки для рисования (<see cref="LiveBuilderSceneText.DrawHint"/>)
    /// </summary>
    public static class LiveBuilderHintKeys
    {
        // Невидимые знаки вокруг подписи клавиши: в именах построек и в наборе с клавиатуры их не бывает.
        private const char KeyStart = '\u0002';
        private const char KeyEnd = '\u0003';


        // ── Метки ────────────────────────────────────────────────────────

        public static string Enter => Key("Enter");

        public static string Esc => Key("Esc");

        public static string Shift => Key("Shift");

        public static string Ctrl => Key("Ctrl");

        public static string Alt => Key("Alt");

        // Отмена Unity — как в меню Edit: на Mac ⌘Z.
        public static string Undo => Key(UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor ? "⌘Z" : "Ctrl+Z");


        // Клавиша, мышь или их сочетание плашкой: «Ctrl+Z», «click again».
        public static string Key(string label)
        {
            return KeyStart + label + KeyEnd;
        }


        // Клавиша ярлыка Live Builder — как она привязана сейчас. Ярлык без клавиши — плашки нет.
        public static string Bound(string shortcutId)
        {
            var label = LiveBuilderShortcutIds.KeyLabelOf(shortcutId);
            return label.Length > 0 ? Key(label) : "";
        }


        // ── Разбор ───────────────────────────────────────────────────────

        // Куски строки по порядку: текст и плашки. Пустые куски пропускаются.
        public static List<(string Text, bool IsKey)> PartsOf(string hint)
        {
            var parts = new List<(string, bool)>();
            var start = 0;

            while (start < hint.Length)
            {
                var keyStart = hint.IndexOf(KeyStart, start);

                if (keyStart < 0)
                {
                    AddText(parts, hint.Substring(start));
                    break;
                }

                AddText(parts, hint.Substring(start, keyStart - start));
                var keyEnd = hint.IndexOf(KeyEnd, keyStart + 1);

                if (keyEnd < 0)
                    keyEnd = hint.Length;

                var label = hint.Substring(keyStart + 1, keyEnd - keyStart - 1);

                if (label.Length > 0)
                    parts.Add((label, true));

                start = keyEnd + 1;
            }

            return parts;
        }

        private static void AddText(List<(string, bool)> parts, string text)
        {
            text = text.Trim();

            if (text.Length > 0)
                parts.Add((text, false));
        }


        // Та же строка без меток — для проверок и для текста там, где плашек нет.
        public static string PlainTextOf(string hint)
        {
            return hint.Replace(KeyStart.ToString(), "").Replace(KeyEnd.ToString(), "");
        }
    }
}
