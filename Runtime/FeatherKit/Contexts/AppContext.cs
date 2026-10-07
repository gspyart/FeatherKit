using System;
using System.Collections.Generic;
using UnityEngine;
using FeatherKit.Audio;
using FeatherKit.Coroutines;
using FeatherKit.Logging;
using FeatherKit.Scenes;
using FeatherKit.Updatables;
using UnityEngine.SceneManagement;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Корневой контекст: живёт весь запуск игры и держит то, что переживает смену сцен —
    /// апдейт, корутины, звук, загрузку сцен. Наследник в игре добавляет своё: конфиг, сохранения, звук.
    ///
    /// Дочерние контексты лежат по СЦЕНАМ, а не по типам: контекст принадлежит своей сцене,
    /// и две одинаковые сцены (две арены, две комнаты) должны уживаться. По типу их можно
    /// только искать.
    ///
    /// Статический Current — сознательный компромисс: точка входа в игре одна, и таскать
    /// ссылку на неё через всё дерево ради чистоты не стоит. У дочерних контекстов своего
    /// Current нет, их достают отсюда.
    /// </summary>
    public abstract class AppContext : Context
    {
        public static AppContext Current { get; private set; }
        
        
        private readonly Dictionary<Scene, SceneContext> children = new Dictionary<Scene, SceneContext>();

        private Transform root;
        

        /// <summary>Единственный раннер апдейта; сервисы дочерних контекстов тикаются им же.</summary>
        public UpdateRunner Updates { get; private set; }

        /// <summary>Корутины переживают смену сцены — останавливать их надо самому, по токену.</summary>
        public CoroutineRunner Coroutines { get; private set; }

        /// <summary>Загрузка уровней. Живёт в корне: сам он смены сцены и переживает.</summary>
        public SceneLoader Scenes { get; private set; }

        /// <summary>Разовые звуки. В корне, потому что звук не обрывается со сменой сцены.</summary>
        public AudioPlayer Audio { get; private set; }


        // ── Объекты, живущие всю игру ─────────────────────────────────────

        /// <summary>
        /// Общий корень всего, что живёт всю игру: раннеры, звук, сервисы из префабов.
        /// Один бессмертный объект в иерархии вместо россыпи, и уборка одна на всех.
        /// </summary>
        public Transform Root
        {
            get
            {
                if (root != null)
                    return root;

                var host = new GameObject("[Global]");
                UnityEngine.Object.DontDestroyOnLoad(host);

                // Под ответственностью контекста: погаснет он — уйдёт и корень, а с ним
                // всё, что внутри: раннеры, звук, сервисы из префабов.
                MarkAsInstance(host);

                return root = host.transform;
            }
        }
        
        
        /// <summary>
        /// Сброс при входе в игру: класс статический, и с выключенной перезагрузкой домена
        /// ссылка на контекст прошлого запуска дожила бы до следующего.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Current = null;
        }

        
        /// <summary>Поднять игру. Второй вызов игнорируется — точка входа одна.</summary>
        public static T Launch<T>(T context) where T : AppContext
        {
            if (Current != null || context == null)
                return Current as T;

            Current = context;

            context.Initialize(null);

            return context;
        }
        
        /// <summary>Погасить игру целиком — нужно в тестах и при выходе из плей-мода.</summary>
        public static void Shutdown()
        {
            if (Current == null)
                return;

            var scenes = new List<Scene>(Current.children.Keys);

            for (var i = 0; i < scenes.Count; i++)
                Current.OnSceneUnloaded(scenes[i]);

            Current.Release();
            Current = null;
        }


        /// <summary>
        /// Поднять контекст для сцены. Старый контекст этой же сцены гасится — так работает
        /// перезагрузка сцены.
        /// </summary>
        public T OnSceneLoaded<T>(T context, Scene scene, Transform root) where T : SceneContext
        {
            if (context == null)
                return null;

            OnSceneUnloaded(scene);

            children[scene] = context;
            context.Bind(scene, root);
            context.Initialize(this);

            return context;
        }

        /// <summary>Погасить контекст конкретной сцены.</summary>
        public void OnSceneUnloaded(Scene scene)
        {
            if (!children.TryGetValue(scene, out var context))
                return;

            // Убираем из словаря до Release: во время разбора сервисов контекст уже не отдаём.
            children.Remove(scene);
            context.Release();
        }

        
        public List<T> GetServicesEverywhere<T>(Action<T> action = null) where T : class
        {
            List<T> result = new();

            var appService = GetLocal<T>();
            
            if (appService != null)
                result.Add(appService);
            
            foreach (var c in children.Values)
            {
                var service = c.GetLocal<T>();
                
                if (service != null)
                    result.Add(service);
            }

            if (action != null)
            {
                foreach (var r in result)
                {
                    action?.Invoke(r);
                }
            }
           
            return result;
        }
        
        
        /// <summary>
        /// Контекст сцены без указания типа. Нужен тем, кто про игру не знает, — самой
        /// библиотеке: сервис из него достаётся обычным Get, тип контекста для этого
        /// знать незачем.
        /// </summary>
        public SceneContext GetContext(Scene scene)
        {
            return children.TryGetValue(scene, out var context) ? context : null;
        }
        
        public T GetContext<T>(Scene scene) where T : SceneContext
        {
            return children.TryGetValue(scene, out var context) ? context as T : null;
        }

        /// <summary>
        /// Контекст того, кто спрашивает: сцену берём у самого объекта. Обычный путь для
        /// сценового компонента — <c>GetContext&lt;BattleContext&gt;(this)</c>.
        ///
        /// Вернёт null для объекта из DontDestroyOnLoad: у него своя служебная сцена,
        /// и контекста там нет. Такому нужно то, что живёт всю игру, — <see cref="Context.Get{T}"/>
        /// у самого AppContext.
        /// </summary>
        public T GetContext<T>(Component owner) where T : SceneContext
        {
            return owner != null ? GetContext<T>(owner.gameObject.scene) : null;
        }

        public T GetContext<T>(GameObject owner) where T : SceneContext
        {
            return owner != null ? GetContext<T>(owner.scene) : null;
        }

        /// <summary>
        /// Первый контекст нужного типа. Годится, пока сцена такого вида одна — а это
        /// обычный случай. Загрузишь две сразу — спрашивай по объекту или по сцене,
        /// иначе достанется случайная.
        /// </summary>
        public T GetContext<T>() where T : SceneContext
        {
            foreach (var context in children.Values)
            {
                if (context is T match)
                    return match;
            }

            return null;
        }
        
        
        // ── Сервисы из префабов ───────────────────────────────────────────

        /// <summary>
        /// Поднять компонент из префаба и положить в корневой контекст. Для того, что
        /// настраивают в инспекторе и что должно пережить смену сцен: музыка, экран
        /// загрузки, аналитика.
        ///
        /// Сцену, в отличие от <see cref="SceneContext"/>, не осматриваем принципиально:
        /// найденный на сцене объект умрёт вместе с ней, а корневой контекст остался бы
        /// со ссылкой на труп и не сказал бы об этом.
        /// </summary>
        protected T RegisterGlobalComponent<T>(T prefab, Action<T> setup = null) where T : Component
        {
            if (prefab == null)
            {
                FLog.Error((typeof(T).Name, FLogColor.Bad), ": нет префаба — глобальный сервис не поднять.");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, Root);
            instance.name = prefab.name; // без "(Clone)" — иначе в иерархии каша

            setup?.Invoke(instance);

            return Register(instance);
        }

        // Раннеры регистрируем как обычные сервисы: дочерние контексты найдут их через
        // родителя, и отдельный способ доставать «глобальное» не нужен.
        private protected override void RegisterDefaults()
        {
            Updates = Register(UpdateRunner.Create("Updates", Root));
            Coroutines = Register(CoroutineRunner.Create("Coroutines", Root));
            Scenes = Register(new SceneLoader(Coroutines));
            Audio = Register(AudioPlayer.Create("Audio", Root));
        }
    }
}
