using System;
using System.Reflection;
using UnityEditor.ShortcutManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ставит контексты ярлыков Live Builder приоритетными: при одной клавише Unity выполняет ярлык приоритетного контекста, а не спрашивает окном.
    /// - опирается на внутреннее API Unity (ContextManager.RegisterPriorityContext) через отражение; ищет его один раз за загрузку скриптов
    /// - не нашлось (другая версия Unity) — обычная регистрация: «2», S и стрелки спорят с окном сцены, тогда работают предупреждение
    ///   и меню Resolve Shortcut Conflicts (<see cref="LiveBuilderShortcutConflicts"/>)
    /// </summary>
    public static class LiveBuilderPriorityShortcutContexts
    {
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool isResolved;
        private static Type contextManagerType;
        private static PropertyInfo shortcutControllerProperty;
        private static PropertyInfo contextManagerProperty;
        private static MethodInfo registerMethod;
        private static MethodInfo deregisterMethod;


        // Нашлось ли внутреннее API Unity. Нет — контексты ставятся обычными.
        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return registerMethod != null && deregisterMethod != null && contextManagerType.IsInstanceOfType(ContextManager);
            }
        }

        // Менеджер контекстов Unity — свой после каждой перезагрузки скриптов, поэтому не кешируется.
        private static object ContextManager => shortcutControllerProperty?.GetValue(null) is { } controller ? contextManagerProperty?.GetValue(controller) : null;


        // ── Регистрация ──────────────────────────────────────────────────

        public static void Register(IShortcutContext context)
        {
            if (IsAvailable)
                registerMethod.Invoke(ContextManager, new object[] { context });
            else
                ShortcutManager.RegisterContext(context);
        }


        public static void Unregister(IShortcutContext context)
        {
            if (IsAvailable)
                deregisterMethod.Invoke(ContextManager, new object[] { context });
            else
                ShortcutManager.UnregisterContext(context);
        }


        // ── Поиск внутреннего API ────────────────────────────────────────

        // Любое расхождение с ожидаемым устройством Unity — просто «не нашлось»: падать редактору из-за клавиш нельзя.
        private static void Resolve()
        {
            if (isResolved)
                return;

            isResolved = true;

            try
            {
                var assembly = typeof(ShortcutManager).Assembly;
                var integrationType = assembly.GetType("UnityEditor.ShortcutManagement.ShortcutIntegration");
                contextManagerType = assembly.GetType("UnityEditor.ShortcutManagement.ContextManager");
                shortcutControllerProperty = integrationType?.GetProperty("instance", AnyMember);
                contextManagerProperty = shortcutControllerProperty?.PropertyType.GetProperty("contextManager", AnyMember);
                registerMethod = contextManagerType?.GetMethod("RegisterPriorityContext", AnyMember, null, new[] { typeof(IShortcutContext) }, null);
                deregisterMethod = contextManagerType?.GetMethod("DeregisterPriorityContext", AnyMember, null, new[] { typeof(IShortcutContext) }, null);
            }
            catch (Exception)
            {
                registerMethod = null;
                deregisterMethod = null;
            }
        }
    }
}
