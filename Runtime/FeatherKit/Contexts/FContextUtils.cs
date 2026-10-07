using UnityEngine;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Короткий путь к сервисам СВОЕЙ сцены для обычных компонентов: две загруженные сцены
    /// не должны лезть друг к другу.
    /// - объект сцены может звать уже в Awake: инсталлер поднимает контекст раньше всех;
    /// - объект внутри префаба компонент-сервиса просыпается посреди регистрации и видит
    ///   только заведённых выше — ему ждать Start или Init.
    /// </summary>
    public static class FContextUtils
    {
        /// <summary>Сервис из контекста той сцены, где лежит объект. Нет контекста или сервиса — null.</summary>
        public static T GetService<T>(this Component owner) where T : class
        {
            return owner != null ? GetService<T>(owner.gameObject) : null;
        }

        public static T GetService<T>(this GameObject owner) where T : class
        {
            if (owner == null || AppContext.Current == null)
                return null;

            return AppContext.Current.GetContext(owner.scene)?.Get<T>();
        }
    }
}
