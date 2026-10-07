using System.Collections.Generic;
using FeatherKit.Lifecycle;
using UnityEngine;

namespace FeatherKit.Updatables
{
    /// <summary>
    /// ЗАВОДИТСЯ КОНТЕКСТОМ САМ. Своего создавать не надо и нельзя — бери готовый
    /// из <c>AppContext.Current.Updates</c>. Тебе тут нужен только IUpdatable:
    /// реализуй его на сервисе, зарегистрируй в контексте — и он начнёт тикаться.
    ///
    /// Носитель кадрового апдейта для обычных классов: держит список IUpdatable
    /// и прогоняет его в своём Update. Живёт на собственном persistent GameObject —
    /// в сцену его класть не надо.
    ///
    /// Раннер один на всю игру. Сценовые сервисы тоже тикаются им, но снимают себя
    /// при выгрузке контекста — см. Context.Release.
    ///
    /// Порядок обхода — порядок добавления. Он предсказуем, но закладываться на него не стоит:
    /// если двум сервисам важно, кто первый, это связь между ними, и выражать её надо явно.
    ///
    /// Он же каждый кадр отдаёт шейдерам реальное время (_FeatherUnscaledTime): встроенное
    /// _Time обнуляется при загрузке сцены и стоит на паузе.
    /// </summary>
    public class UpdateRunner : MonoBehaviour, IReleasable
    {
        private static readonly int UnscaledTimeProperty = Shader.PropertyToID("_FeatherUnscaledTime");

        private readonly List<IUpdatable> updatables = new List<IUpdatable>();

        // Проходим по копии: сервис может добавить или убрать другого прямо из своего OnUpdate,
        // и без копии это упало бы прямо посреди кадра.
        private readonly List<IUpdatable> iterationBuffer = new List<IUpdatable>();


        // internal, а не public: раннер должен быть один, и заводит его AppContext.
        // Второй такой же тикал бы всех по второму разу — это ловится компилятором,
        // а не комментарием.
        internal static UpdateRunner Create(string hostName, Transform parent)
        {
            var host = new GameObject(hostName);
            host.transform.SetParent(parent, false);

            return host.AddComponent<UpdateRunner>();
        }


        public void Add(IUpdatable updatable)
        {
            if (updatable == null || updatables.Contains(updatable))
                return;

            updatables.Add(updatable);
        }

        public void Remove(IUpdatable updatable)
        {
            if (updatable == null)
                return;

            updatables.Remove(updatable);
        }

        public void Clear()
        {
            updatables.Clear();
        }

        /// <summary>Контекст гасит свои сервисы: список чистится. Объект не уничтожаем —
        /// его создал контекст, он же и унесёт.</summary>
        public void Release()
        {
            updatables.Clear();
        }


        private void Update()
        {
            // Замедление живёт по реальному времени, поэтому тикается до сервисов —
            // иначе оно тормозило бы само себя.
            GameTiming.GameTime.Tick(Time.unscaledDeltaTime);
            Shader.SetGlobalFloat(UnscaledTimeProperty, Time.unscaledTime);

            iterationBuffer.Clear();
            iterationBuffer.AddRange(updatables);

            for (var i = 0; i < iterationBuffer.Count; i++)
            {
                var updatable = iterationBuffer[i];

                // Уничтоженный MonoBehaviour вычёркиваем сами: Unity после Destroy делает
                // объект "поддельно-null" (== null истинно, хотя ссылка в списке жива), так
                // что подписчику-компоненту не нужно ничего писать в OnDestroy.
                // Для обычных C#-классов такой проверки не существует — их снимает владелец.
                if (updatable is Object unityObject && unityObject == null)
                {
                    updatables.Remove(updatable);
                    continue;
                }

                // Игровое время: на паузе сервисы стоят.
                updatable.OnUpdate(GameTiming.GameTime.DeltaTime);
            }
        }

        // Ноль — знак шейдерам, что раннера нет: после выхода из игры превью в редакторе снова крутится само.
        private void OnDestroy()
        {
            Shader.SetGlobalFloat(UnscaledTimeProperty, 0f);
        }
    }
}
