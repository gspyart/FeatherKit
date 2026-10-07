using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Переключатель гизмо в Game View: горячая клавиша и пункт меню с галкой.
    ///
    /// Штатная кнопка «Gizmos» стоит в правом конце тулбара Game View, а в вертикальном
    /// формате окно узкое — тулбар обрезается, кнопка уезжает за край, и достать её нечем:
    /// тулбар не скроллится. Клавиша дёргает тот же флаг напрямую.
    ///
    /// Публичного API у флага нет — свойство у Game View внутреннее, поэтому только
    /// рефлексия. Не нашлось — ругаемся в консоль: имя Unity однажды сменит, и молчащая
    /// клавиша выглядела бы как «гизмо сломались».
    /// </summary>
    public static class GameViewGizmosToggle
    {
        private const string MenuPath = "FeatherKit/Гизмо в Game View";

        // Клавиша задана суффиксом пункта меню (%&g — Cmd/Ctrl+Alt+G), а не через
        // Shortcut Manager: пока фокус в Game View, ввод уходит в игру, и клавиши
        // менеджера до редактора не доходят. Пункт меню перехватывает их раньше окна.
        private const string MenuPathWithShortcut = MenuPath + " %&g";
        private const string GameViewTypeName = "UnityEditor.GameView";

        // Именно drawGizmos, а не похожее showGizmos: второе досталось Game View
        // от PlayModeView, живёт своим полем и на картинку не влияет вовсе.
        private const string GizmosPropertyName = "drawGizmos";

        // ── Доступ к внутренностям Game View ─────────────

        private static Type GameViewType => typeof(EditorWindow).Assembly.GetType(GameViewTypeName);

        private static PropertyInfo GizmosProperty => GameViewType?.GetProperty(
            GizmosPropertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // ── Переключение ─────────────────────────────────

        [MenuItem(MenuPathWithShortcut)]
        public static void Toggle()
        {
            var property = GizmosProperty;
            if (property == null)
            {
                Debug.LogWarning($"Гизмо: у {GameViewTypeName} не нашлось «{GizmosPropertyName}» — " +
                                 "Unity сменил внутреннее имя, переключатель надо починить.");
                return;
            }

            var windows = FindGameViews();
            if (windows.Length == 0)
            {
                Debug.LogWarning("Гизмо: Game View не открыт.");
                return;
            }

            // Все открытые Game View переключаем разом, по состоянию первого: два окна
            // с разными гизмо — это про то, что клавиша сработала непонятно.
            bool enabled = !(bool)property.GetValue(windows[0]);

            foreach (var window in windows)
            {
                property.SetValue(window, enabled);
                ((EditorWindow)window).Repaint();
            }

            Menu.SetChecked(MenuPath, enabled);
        }

        private static UnityEngine.Object[] FindGameViews()
        {
            var type = GameViewType;
            return type == null ? Array.Empty<UnityEngine.Object>() : Resources.FindObjectsOfTypeAll(type);
        }

        // ── Галка в меню ─────────────────────────────────

        // Галку ставим при открытии меню, а не после переключения: гизмо гасят и штатной
        // кнопкой, и тогда запомненное значение разошлось бы с настоящим.
        [MenuItem(MenuPathWithShortcut, validate = true)]
        private static bool UpdateMenuChecked()
        {
            var property = GizmosProperty;
            var windows = FindGameViews();
            bool enabled = property != null && windows.Length > 0 && (bool)property.GetValue(windows[0]);

            Menu.SetChecked(MenuPath, enabled);
            return true;
        }
    }
}
