using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Корень сцены: поднимает её контекст на Awake и гасит при выгрузке. Наследнику
    /// остаётся только назвать свой контекст:
    ///
    /// <code>public class GameplayInstaller : SceneContextInstaller&lt;GameplayContext&gt; { }</code>
    ///
    /// Порядок выполнения задан заранее: контекст должен существовать раньше любого другого
    /// Awake на сцене, иначе сервисы будут искать его и не находить.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public abstract class SceneContextInstaller<T> : MonoBehaviour where T : SceneContext, new()
    {
        [Tooltip("Трансформ в котором будут спавниться сервисы-компоненты")]
        [SerializeField] private Transform servicesRoot;

        public Transform ServicesRoot => servicesRoot != null ? servicesRoot : this.transform;
        
        
        /// <summary>Контекст своей сцены.</summary>
        public T Context => AppContext.Current?.GetContext<T>(gameObject.scene);
        
        
        /// <summary>Переопредели, если контексту нужны аргументы конструктора.</summary>
        protected virtual T CreateContext() => new T();

        /// <summary>
        /// Что сделать, когда сцена уже проснулась целиком. Не Awake: к этому моменту все
        /// объекты сцены зарегистрировались в своих сервисах.
        /// </summary>
        protected virtual void OnSceneReady() { }
        
        /// <summary>
        /// Что делать, перед тем как сцена и контекст будут выгружены.
        /// </summary>
        protected virtual void OnSceneBeforeDestroy() { }

        private void Awake()
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            
            if (AppContext.Current == null)
            {
                Debug.LogError($"{name}: корневой контекст не поднят — сцена не запустится. " +
                               "Проверь точку входа игры.", this);
                return;
            }

            AppContext.Current.OnSceneLoaded(CreateContext(), gameObject.scene, ServicesRoot);
        }

        private void Start()
        {
            OnSceneReady();
            FLog.Info(new FLogPart(typeof(T).Name, new Color(0.22f, 0.59f, 0.36f)),
                new FLogPart(" is initialized"));
        }

        private void OnDestroy()
        {
            OnSceneBeforeDestroy();
            AppContext.Current?.OnSceneUnloaded(gameObject.scene);
            
            FLog.Info(new FLogPart(typeof(T).Name, new Color(0.59f, 0.21f, 0.24f)),
                new FLogPart(" is destroyed"));
        }
    }
}
