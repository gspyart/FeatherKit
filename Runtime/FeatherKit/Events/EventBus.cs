using System;
using System.Collections.Generic;

namespace FeatherKit.Events
{
    /// <summary>
    /// Узкая шина сообщений: издатель не знает подписчиков, подписчик не знает издателя.
    /// Нужна там, где событие на самом объекте не подходит — попапы урона рисует один
    /// контроллер на весь экран, и вешать на каждого актора компонент-переходник ради
    /// этого избыточно.
    ///
    /// Сознательно НЕ фреймворк: ни приоритетов, ни очередей, ни отложенной доставки,
    /// ни истории. Publish
    /// зовёт подписчиков сразу и в текущем потоке. Если начнёт хотеться очередей — это
    /// сигнал, что задачу решают не тем инструментом.
    ///
    /// Сообщения — структуры: подписчиков на событие обычно один-два, а урон летит десятками
    /// в секунду, и мусорить классами тут не за что.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();


        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            var key = typeof(T);

            Handlers[key] = Handlers.TryGetValue(key, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            var key = typeof(T);
            if (!Handlers.TryGetValue(key, out var existing))
                return;

            var left = Delegate.Remove(existing, handler);

            if (left == null)
                Handlers.Remove(key);
            else
                Handlers[key] = left;
        }

        public static void Publish<T>(T message) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var existing))
                return;

            // Приводим к конкретному делегату один раз: Delegate.DynamicInvoke боксит
            // аргументы и стоит на порядок дороже, а урон летит каждый кадр.
            ((Action<T>)existing)?.Invoke(message);
        }

        /// <summary>
        /// Сброс на случай, если кто-то не отписался. Штатный путь — Unsubscribe в OnDisable;
        /// это аварийный выход, чтобы подписка с прошлого забега не стреляла в следующем.
        /// </summary>
        public static void Clear()
        {
            Handlers.Clear();
        }


        /// <summary>
        /// Сброс при входе в игру. Шина статическая, и с выключенной перезагрузкой домена
        /// подписки прошлого запуска дожили бы до следующего — вместе со ссылками на объекты
        /// уничтоженной сцены.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Handlers.Clear();
        }
    }
}
