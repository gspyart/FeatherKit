using System;
using System.Collections.Generic;
using FeatherKit.Lifecycle;
using FeatherKit.Updatables;
using UnityEngine;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Хранилище сервисов с родителем. Ручная замена Zenject: регистрация явная, без
    /// атрибутов и рефлексии — по коду видно, что где создаётся и в каком порядке.
    ///
    /// Родитель — то, ради чего это вообще класс, а не словарь: не нашли у себя, спрашиваем
    /// выше. Поэтому пул или звук живут одни на всю игру, а сцена ими просто пользуется,
    /// не заводя свои.
    ///
    /// Ключ — тип. Два сервиса одного типа в одном контексте не живут: второй вытеснит
    /// первого, погасив его. Нужны оба — заводи дочерний контекст.
    /// </summary>
    public abstract class Context
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly List<object> registrationOrder = new List<object>();
        private readonly List<GameObject> serviceInstances = new List<GameObject>();

        private bool initialized;


        /// <summary>У кого спрашивать то, чего нет здесь. Null у корневого.</summary>
        public Context Parent { get; private set; }


        public T Register<T>(T service) where T : class
        {
            if (service == null)
                return null;

            // Старый сервис под тем же типом гасим: иначе он продолжил бы тикаться
            // и держать подписки.
            if (services.TryGetValue(typeof(T), out var previous))
            {
                registrationOrder.Remove(previous);
                DisposeService(previous);
            }

            services[typeof(T)] = service;
            registrationOrder.Add(service);

            AddServiceToUpdates(service);

            // Регистрация после запуска контекста — штатный случай (сервис завёл сервис),
            // просто инициализируем сразу: ждать уже нечего.
            if (initialized)
                (service as IInitializable)?.Init();

            return service;
        }

        
        /// <summary>Сервис из этого контекста или любого родительского. Нет нигде — null.</summary>
        public T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var value))
                return (T)value;

            return Parent?.Get<T>();
        }

        /// <summary>
        /// То же, но забытая регистрация — это ошибка, а не пустой результат. Бросает сразу
        /// и с именем типа, вместо NullReferenceException через десять кадров в чужом классе.
        ///
        /// Бери его везде, где сервис обязан быть. Обычный Get — для того, чего может
        /// законно не оказаться.
        /// </summary>
        public T GetRequired<T>() where T : class
        {
            var service = Get<T>();

            if (service == null)
                throw new InvalidOperationException(
                    $"{GetType().Name}: сервис {typeof(T).Name} не зарегистрирован. " +
                    "Проверь RegisterServices у этого контекста и у родительских.");

            return service;
        }

        /// <summary>Только свои сервисы, без обращения к родителю.</summary>
        public T GetLocal<T>() where T : class
        {
            return services.TryGetValue(typeof(T), out var value) ? (T)value : null;
        }

        public bool Has<T>() where T : class => Get<T>() != null;


        /// <summary>Здесь наследник заводит свои сервисы.</summary>
        protected abstract void RegisterServices();

        /// <summary>
        /// Взять объект под свою ответственность: контекст погаснет — объект уничтожится.
        ///
        /// Зовётся только для того, что контекст создал сам. Объект, найденный на сцене,
        /// не наш: на нём может висеть чужое хозяйство, и сносить его целиком нельзя.
        /// </summary>
        private protected void MarkAsInstance(GameObject instance)
        {
            if (instance != null)
                serviceInstances.Add(instance);
        }


        // internal: поднимать и гасить контекст — дело владельца. RegisterServices нельзя
        // звать из конструктора базы, поля наследника там ещё не готовы.
        /// <summary>
        /// Что контекст заводит себе сам, до сервисов наследника: у корневого — апдейт
        /// и корутины, у сценового — пул.
        ///
        /// private protected — видно только контекстам самой библиотеки. Игре этот метод
        /// не нужен и не показывается: она пишет один RegisterServices и про встроенное
        /// не думает.
        /// </summary>
        private protected virtual void RegisterDefaults() { }


        internal void Initialize(Context parent)
        {
            Parent = parent;

            RegisterDefaults();
            RegisterServices();

            // Init всем — уже после регистрации всех: к этому моменту сервис видит контекст
            // целиком, и порядок строк регистрации перестаёт иметь значение.
            for (var i = 0; i < registrationOrder.Count; i++)
                (registrationOrder[i] as IInitializable)?.Init();

            initialized = true;
        }

        internal void Release()
        {
            initialized = false;
            
            // В обратном порядке: тот, кто зарегистрировался позже, мог подписаться
            // на того, кто раньше.
            for (var i = registrationOrder.Count - 1; i >= 0; i--)
                DisposeService(registrationOrder[i]);

            DestroyInstances();

            registrationOrder.Clear();
            services.Clear();
            Parent = null;
        }   


        // Сервисы в Release чистят только внутрянку — объекты уносит тот, кто их создал,
        // то есть контекст. Иначе половина сервисов дестроит себя, половина нет, и понять,
        // кто за что отвечает, можно только чтением каждого.
        private void DestroyInstances()
        {
            for (var i = 0; i < serviceInstances.Count; i++)
            {
                if (serviceInstances[i] == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(serviceInstances[i]);
                else
                    UnityEngine.Object.DestroyImmediate(serviceInstances[i]);
            }

            serviceInstances.Clear();
        }


        // Раннер апдейта общий и переживает сцену, поэтому сервисы снимаем сами.
        private void AddServiceToUpdates(object service)
        {
            if (service is IUpdatable updatable)
                Get<UpdateRunner>()?.Add(updatable);
        }

        private void DisposeService(object service)
        {
            if (service is IUpdatable updatable)
                Get<UpdateRunner>()?.Remove(updatable);

            (service as IReleasable)?.Release();
        }
    }
}
