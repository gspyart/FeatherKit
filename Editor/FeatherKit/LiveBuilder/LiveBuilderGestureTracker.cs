using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Знает, идёт ли сейчас жест — тянут ручку, поле или рисуют инструментом Live Builder, — и когда он кончился.
    /// Жест: мышь зажата на ручке или поле — IMGUI (<see cref="GUIUtility.hotControl"/>) или UI Toolkit (захват указателя), инструмент на середине рисования,
    /// жест открыт кодом (замер, проверки).
    /// Пока идёт жест, очередь (<see cref="LiveBuilderRebuildQueue"/>) собирает только черновики и не пишет файлы.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderGestureTracker
    {
        // Мышь держат на чужом контроле без единой правки столько — жест кончился: контрол мог не отпустить захват, с.
        public const double StuckMouseHoldSeconds = 10.0;

        private static int explicitGestureCount;
        private static bool wasGestureActive;

        // На чём сейчас держат мышь и когда там последний раз что-то происходило: взяли контрол или пришла правка.
        private static int heldHotControl;
        private static IEventHandler heldElement;
        private static double lastActivityTime;


        static LiveBuilderGestureTracker()
        {
            EditorApplication.update += OnEditorUpdate;
        }


        // Жест кончился: мышь отпустили, рисование закончили или отменили.
        public static event System.Action GestureEnded;


        public static bool IsGestureActive => explicitGestureCount > 0 || IsAnyToolBusy() || IsMouseHeldOnControl();


        // ── Жест кодом ───────────────────────────────────────────────────

        // Замер и проверки тянут ручку кодом, без мыши: жест открыт до конца using — и при ошибке закроется.
        public static LiveBuilderExplicitGesture BeginExplicitGesture()
        {
            explicitGestureCount++;
            wasGestureActive = true;
            return new LiveBuilderExplicitGesture(true);
        }


        // Зовёт только <see cref="LiveBuilderExplicitGesture.Dispose"/>.
        internal static void EndExplicitGesture()
        {
            if (explicitGestureCount == 0)
                return;

            explicitGestureCount--;
            CheckGestureEnded();
        }


        // ── Правки ───────────────────────────────────────────────────────

        // Очередь сообщает о каждой правке: мышь на контроле, а правки идут — жест живой.
        public static void NoteEdit()
        {
            lastActivityTime = EditorApplication.timeSinceStartup;
        }


        // ── Мышь на контроле ─────────────────────────────────────────────

        // Новый контрол под мышью — отсчёт сторожа заново.
        private static bool IsMouseHeldOnControl()
        {
            var hotControl = GUIUtility.hotControl;
            var capturingElement = hotControl == 0 ? InterfaceCapturingElement() : null;

            if (hotControl == 0 && capturingElement == null)
            {
                heldHotControl = 0;
                heldElement = null;
                return false;
            }

            var now = EditorApplication.timeSinceStartup;

            if (hotControl != heldHotControl || capturingElement != heldElement)
            {
                heldHotControl = hotControl;
                heldElement = capturingElement;
                lastActivityTime = now;
            }

            return now - lastActivityTime < StuckMouseHoldSeconds;
        }

        // Поле инспектора на UI Toolkit тянут мышью — указатель захвачен элементом, а hotControl пуст.
        private static IEventHandler InterfaceCapturingElement()
        {
            return CapturingElementIn(EditorWindow.focusedWindow) ?? CapturingElementIn(EditorWindow.mouseOverWindow);
        }

        private static IEventHandler CapturingElementIn(EditorWindow window)
        {
            var panel = window != null ? window.rootVisualElement.panel : null;
            return panel?.GetCapturingElement(PointerId.mousePointerId);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static bool IsAnyToolBusy()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                if (tool.IsBusy)
                    return true;

            return false;
        }

        private static void CheckGestureEnded()
        {
            var isActive = IsGestureActive;

            if (wasGestureActive && !isActive)
            {
                wasGestureActive = false;
                GestureEnded?.Invoke();
                return;
            }

            wasGestureActive = isActive;
        }


        private static void OnEditorUpdate()
        {
            CheckGestureEnded();
        }
    }


    /// <summary>Жест, открытый кодом (<see cref="LiveBuilderGestureTracker.BeginExplicitGesture"/>): закрывается в конце using, и при ошибке тоже.</summary>
    public readonly struct LiveBuilderExplicitGesture : System.IDisposable
    {
        // Пустая структура (default) жеста не открывала — и закрывать нечего.
        private readonly bool isOpened;


        internal LiveBuilderExplicitGesture(bool isOpened)
        {
            this.isOpened = isOpened;
        }


        // IDisposable
        public void Dispose()
        {
            if (isOpened)
                LiveBuilderGestureTracker.EndExplicitGesture();
        }
    }
}
