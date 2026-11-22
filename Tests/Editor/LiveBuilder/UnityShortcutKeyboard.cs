using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Нажимает клавишу через обработчик ярлыков самой Unity (внутренний, отражением) — как при окне сцены в фокусе — и говорит, какой ярлык выполнился.
    /// Окно спора на время отключено: спор — это «не выполнилось ничего». Опирается на устройство Unity 6000.6: после обновления Unity — первое, что чинить.
    /// </summary>
    public sealed class UnityShortcutKeyboard : System.IDisposable
    {
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly object contextManager;
        private readonly object trigger;
        private readonly MethodInfo handleKeyEvent;
        private readonly FieldInfo showConflictsWindowField;
        private readonly bool wasShowingConflictsWindow;
        private readonly EventInfo beforeShortcutInvoked;
        private readonly System.Delegate invokedHandler;
        private readonly List<string> invokedIds = new();


        public UnityShortcutKeyboard(SceneView focusedSceneView)
        {
            var assembly = typeof(ShortcutManager).Assembly;
            var controller = assembly.GetType("UnityEditor.ShortcutManagement.ShortcutIntegration").GetProperty("instance", AnyMember).GetValue(null);
            contextManager = controller.GetType().GetProperty("contextManager", AnyMember).GetValue(controller);
            trigger = controller.GetType().GetProperty("trigger", AnyMember)?.GetValue(controller) ?? controller.GetType().GetField("m_Trigger", AnyMember).GetValue(controller);
            handleKeyEvent = trigger.GetType().GetMethod("HandleKeyEvent", AnyMember);
            contextManager.GetType().GetMethod("SetFocusedWindow", AnyMember).Invoke(contextManager, new object[] { focusedSceneView });

            showConflictsWindowField = trigger.GetType().GetField("m_ShowConflictsWindow", AnyMember);
            wasShowingConflictsWindow = (bool)showConflictsWindowField.GetValue(trigger);
            showConflictsWindowField.SetValue(trigger, false);

            var entryType = assembly.GetType("UnityEditor.ShortcutManagement.ShortcutEntry");
            beforeShortcutInvoked = trigger.GetType().GetEvent("beforeShortcutInvoked", AnyMember);
            var handler = GetType().GetMethod(nameof(RememberInvoked), AnyMember).MakeGenericMethod(entryType);
            invokedHandler = System.Delegate.CreateDelegate(beforeShortcutInvoked.EventHandlerType, this, handler);
            beforeShortcutInvoked.GetAddMethod(true).Invoke(trigger, new object[] { invokedHandler });
        }


        // Нажал и отпустил. Возвращает имена выполненных ярлыков; пусто — ни один (нет такого или спор).
        public List<string> Press(KeyCode key)
        {
            invokedIds.Clear();
            handleKeyEvent.Invoke(trigger, new object[] { new Event { type = EventType.KeyDown, keyCode = key }, contextManager });
            handleKeyEvent.Invoke(trigger, new object[] { new Event { type = EventType.KeyUp, keyCode = key }, contextManager });
            return new List<string>(invokedIds);
        }

        private void RememberInvoked<TEntry>(TEntry entry, ShortcutArguments arguments)
        {
            invokedIds.Add((string)typeof(TEntry).GetProperty("displayName", AnyMember).GetValue(entry));
        }


        public void Dispose()
        {
            beforeShortcutInvoked.GetRemoveMethod(true).Invoke(trigger, new object[] { invokedHandler });
            showConflictsWindowField.SetValue(trigger, wasShowingConflictsWindow);
            contextManager.GetType().GetMethod("SetFocusedWindow", AnyMember).Invoke(contextManager, new object[] { null });
        }
    }
}
