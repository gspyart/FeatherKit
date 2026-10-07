using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Панели отладки на экране: «HP: 42», «Живых врагов: 7», «Состояние: погоня».
    ///
    /// Рисует всё это один <see cref="DebugPanelDrawer"/>, а остальные только присылают ему
    /// данные. Поэтому свой OnGUI не нужен никому: он у каждого свой прямоугольник, и десяток
    /// таких раскладывают надписи друг на друга.
    ///
    /// Окно заводит тот, кто первым в него написал; заголовок и есть ключ. Второй с тем же
    /// заголовком попадёт в то же окно — так соседние системы складывают свои строки вместе,
    /// не зная друг о друге.
    ///
    /// Статический по той же причине, что и <see cref="DebugConsole"/>: пишут отовсюду
    /// и в любой момент. В релизной сборке вызовы вырезает компилятор, и панель
    /// не создаётся вовсе — если игра не поставила символ FEATHERKIT_DEBUG.
    /// </summary>
    public static class DebugPanel
    {
        /// <summary>
        /// Имя ассета оформления в любой папке Resources. Положил ассет с таким именем —
        /// панель подхватит его сама, не положил — рисует по умолчанию.
        /// </summary>
        public const string StyleResource = "DebugPanelStyle";

        private const string EditorOnly = "UNITY_EDITOR";
        private const string DevelopmentOnly = "DEVELOPMENT_BUILD";

        // Тот же символ, что у консоли: отладка включается в сборке одним движением.
        private const string AnyBuild = DebugConsole.AnyBuildSymbol;

        private static readonly List<DebugPanelWindow> windows = new List<DebugPanelWindow>();

        private static DebugPanelStyle style;


        /// <summary>Показывать ли панели. Переключает игра: хоткеем, кнопкой, командой консоли.</summary>
        public static bool IsVisible { get; set; } = true;

        /// <summary>Масштаб текста. 0 — подобрать по высоте экрана, иначе своё значение.</summary>
        public static float Scale { get; set; }

        /// <summary>
        /// Как панель выглядит. Ассет ищется сам, в Resources, по имени
        /// <see cref="StyleResource"/> — игре не нужно ничего подключать, а библиотеке
        /// не нужно знать, откуда игра берёт свои настройки.
        ///
        /// Нет ассета — рисуем по умолчанию. Отладка не должна ломаться из-за отсутствия
        /// ассета оформления.
        /// </summary>
        public static DebugPanelStyle Style
        {
            get => style != null ? style : style = Load();
            set => style = value;
        }


        internal static IReadOnlyList<DebugPanelWindow> Windows => windows;


        /// <summary>
        /// Показывать значение, пока владелец жив. Функция опрашивается каждый кадр — держи
        /// её дешёвой: чтение поля, а не поиск по сцене.
        ///
        /// Владелец нужен для уборки: обычно это <c>this</c>, и по нему строки снимаются
        /// одной строкой в OnDisable.
        /// </summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void Watch(object owner, string window, string label, Func<object> value)
        {
            if (string.IsNullOrWhiteSpace(window) || string.IsNullOrWhiteSpace(label) || value == null)
                return;

            DebugPanelDrawer.EnsureExists();

            GetOrCreate(window.Trim()).Set(owner, label.Trim(), value);
        }

        /// <summary>
        /// Показать уже посчитанное значение. Для того, что считается внутри цикла и наружу
        /// не выставлено, — тогда зовётся оттуда же, где считается.
        /// </summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void Set(object owner, string window, string label, object value)
        {
            Watch(owner, window, label, () => value);
        }

        /// <summary>В какой угол экрана прижать окно. По умолчанию — левый верхний.</summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void SetAnchor(string window, DebugAnchor anchor)
        {
            if (!string.IsNullOrWhiteSpace(window))
                GetOrCreate(window.Trim()).Anchor = anchor;
        }

        /// <summary>
        /// Убрать все строки этого владельца. Зови в OnDisable: объект мог уехать в пул,
        /// и его функция значения обратилась бы к чужому состоянию.
        /// </summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void Forget(object owner)
        {
            for (var i = windows.Count - 1; i >= 0; i--)
            {
                windows[i].Forget(owner);

                // Окно без строк — это уже не окно, а пустая рамка с заголовком.
                if (windows[i].RowCount == 0)
                    windows.RemoveAt(i);
            }
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void Clear()
        {
            windows.Clear();
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuild)]
        public static void Toggle()
        {
            IsVisible = !IsVisible;
        }


        private static DebugPanelWindow GetOrCreate(string title)
        {
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].Title == title)
                    return windows[i];
            }

            var window = new DebugPanelWindow(title, DebugAnchor.TopLeft);
            windows.Add(window);

            return window;
        }


        private static DebugPanelStyle Load()
        {
            var asset = Resources.Load<DebugPanelStyle>(StyleResource);

            return asset != null ? asset : DebugPanelStyle.CreateDefault();
        }


        /// <summary>
        /// Сброс при входе в игру. Класс статический: с выключенной перезагрузкой домена
        /// окна прошлого запуска дожили бы до следующего вместе со ссылками на мёртвые объекты.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            windows.Clear();

            IsVisible = true;
            Scale = 0f;
            style = null;
        }
    }
}
