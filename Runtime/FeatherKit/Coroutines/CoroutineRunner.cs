using System.Collections;
using System.Collections.Generic;
using FeatherKit.Lifecycle;
using UnityEngine;

namespace FeatherKit.Coroutines
{
    /// <summary>
    /// ЗАВОДИТСЯ КОНТЕКСТОМ САМ. Своего создавать не надо и нельзя — бери готовый
    /// из <c>AppContext.Current.Coroutines</c>.
    ///
    /// Раннер корутин для классов без MonoBehaviour (сервисы, менеджеры, состояния).
    /// Живёт на собственном GameObject — в сцену его класть не надо.
    ///
    /// Объект persistent, поэтому корутина не обрывается при смене сцены — но по той же
    /// причине её надо останавливать самому, если она стала не нужна.
    ///
    /// Возвращает CoroutineToken, а не ссылку на Coroutine: по ссылке нельзя узнать, доиграла
    /// корутина или ещё идёт, и владельцу пришлось бы держать рядом свой флаг.
    /// </summary>
    public class CoroutineRunner : MonoBehaviour, IReleasable
    {
        private readonly Dictionary<int, Coroutine> running = new Dictionary<int, Coroutine>();

        // Растёт всегда и никогда не переиспользуется — иначе старый токен мог бы случайно
        // остановить чужую корутину, занявшую освободившийся номер.
        private int lastId;


        // internal, а не public: раннер один, и заводит его AppContext.
        internal static CoroutineRunner Create(string hostName, Transform parent)
        {
            var host = new GameObject(hostName);
            host.transform.SetParent(parent, false);

            return host.AddComponent<CoroutineRunner>();
        }


        // Null-корутина ничего не запускает и возвращает пустой токен.
        public CoroutineToken Run(IEnumerator routine)
        {
            if (routine == null)
                return default;

            var id = ++lastId;
            running[id] = StartCoroutine(Track(id, routine));

            return new CoroutineToken(id, this);
        }

        public void StopAll()
        {
            StopAllCoroutines();
            running.Clear();
        }

        /// <summary>Контекст гасит свои сервисы: корутины останавливаются. Объект не
        /// уничтожаем — его создал контекст, он же и унесёт.</summary>
        public void Release()
        {
            StopAll();
        }


        // Вызывается из CoroutineToken — снаружи работаем токеном, а не голыми id.
        internal bool IsRunning(int id) => running.ContainsKey(id);

        internal void Stop(int id)
        {
            if (!running.TryGetValue(id, out var coroutine))
                return;

            running.Remove(id);

            if (coroutine != null)
                StopCoroutine(coroutine);
        }


        // Обёртка нужна ровно для того, чтобы вычеркнуть корутину из списка, когда она
        // доработала сама. Именно "yield return routine", а не StartCoroutine(routine):
        // так вложенная корутина остаётся частью этой же, и Stop останавливает обе.
        //
        // finally, а не просто строка после yield: если внутри корутины бросит исключение,
        // Unity её оборвёт, и без finally запись осталась бы в словаре навсегда — токен
        // после этого врал бы, что корутина всё ещё идёт. (yield return внутри try с finally
        // язык разрешает, с catch — нет.)
        private IEnumerator Track(int id, IEnumerator routine)
        {
            try
            {
                yield return routine;
            }
            finally
            {
                running.Remove(id);
            }
        }
    }
}
