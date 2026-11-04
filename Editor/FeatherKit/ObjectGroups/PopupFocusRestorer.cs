using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Возвращает редактор в рабочее состояние после всплывающего окна (ShowPopup), открытого под курсором.
    /// Такое окно, закрываясь, может оставить Unity без окна в фокусе: клики работают, а камера сцены правой кнопкой
    /// не вращается — её клавиши живут, только пока сцена в фокусе. Сама Unity при этом считает, что фокус у сцены,
    /// поэтому просто «дать фокус сцене» ничего не делает. Помогает то же, что руками: фокус на миг — другому видимому окну, потом обратно.
    /// Сцене под курсором — ещё и «мышь вошла»: её она тоже теряет, пока окно висит над ней.
    /// </summary>
    public static class PopupFocusRestorer
    {
        // Через сколько после закрытия проверить фокус и через сколько повтор, сек. Раньше нельзя: окно ещё не исчезло совсем.
        private const double FirstAttemptDelay = 0.15;
        private const double RetryDelay = 0.3;
        private const int MaxAttempts = 3;

        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;


        // После закрытия: фокус никому не достался — вернуть windowFocusedBefore; окну сцены под курсором — «мышь вошла».
        // Кликнул мимо, в другое окно, — фокус уже у него, его не трогаем. Unity не на переднем плане — тоже не трогаем.
        public static void RestoreAfterClose(EditorWindow windowFocusedBefore, Vector2 mouseScreenPosition)
        {
            EditorApplication.delayCall += () => SendMouseEnter(mouseScreenPosition);

            if (windowFocusedBefore == null)
                return;

            var nextAttemptTime = EditorApplication.timeSinceStartup + FirstAttemptDelay;
            var attempts = 0;
            EditorWindow bouncedTo = null;
            EditorApplication.CallbackFunction watch = null;

            watch = () =>
            {
                // Кадр после отскока: фокус — обратно.
                if (bouncedTo != null)
                {
                    bouncedTo = null;
                    windowFocusedBefore.Focus();
                    return;
                }

                if (EditorApplication.timeSinceStartup < nextAttemptTime)
                    return;

                var isFocusLost = EditorWindow.focusedWindow == null && windowFocusedBefore != null && InternalEditorUtility.isApplicationActive;
                attempts++;
                nextAttemptTime = EditorApplication.timeSinceStartup + RetryDelay;

                if (!isFocusLost || attempts > MaxAttempts)
                {
                    EditorApplication.update -= watch;
                    return;
                }

                bouncedTo = OtherVisibleWindow(windowFocusedBefore);

                if (bouncedTo != null)
                    bouncedTo.Focus();
                else
                    windowFocusedBefore.Focus();
            };

            EditorApplication.update += watch;
        }


        // Другое окно, которое сейчас видно: не скрытая вкладка (её фокус переключил бы вкладки) и не панель инструментов сверху.
        private static EditorWindow OtherVisibleWindow(EditorWindow except)
        {
            var parentField = typeof(EditorWindow).GetField("m_Parent", InstanceMembers);
            var hostViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
            var actualViewProperty = hostViewType?.GetProperty("actualView", InstanceMembers);

            if (parentField == null || actualViewProperty == null)
                return null;

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == except || !window.docked || window is SceneView || window.GetType().Name.Contains("Toolbar"))
                    continue;

                var parent = parentField.GetValue(window);

                if (parent != null && actualViewProperty.GetValue(parent) as EditorWindow == window)
                    return window;
            }

            return null;
        }


        private static void SendMouseEnter(Vector2 mouseScreenPosition)
        {
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView == null || !sceneView.position.Contains(mouseScreenPosition))
                    continue;

                var localPosition = mouseScreenPosition - sceneView.position.position;
                sceneView.SendEvent(new Event { type = EventType.MouseEnterWindow, mousePosition = localPosition });
                return;
            }
        }
    }
}
