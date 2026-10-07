using System;
using System.Collections;
using FeatherKit.Coroutines;
using FeatherKit.Lifecycle;
using FeatherKit.Timers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Scenes
{
    /// <summary>
    /// ЗАВОДИТСЯ КОНТЕКСТОМ САМ. Своего создавать не надо и нельзя — бери готовый
    /// из <c>AppContext.Current.Scenes</c>.
    ///
    /// Загрузка уровней: одна точка вместо SceneManager.LoadScene по всему коду.
    ///
    /// Даёт то, ради чего это обычно и заводят: прогресс для экрана загрузки, события
    /// «начал / загрузил / выгружаю / выгрузил» и память о том, какой уровень сейчас идёт —
    /// его конфиг читает сцена, когда просыпается.
    ///
    /// Грузит всегда асинхронно: синхронная загрузка вешает кадр, и экран загрузки
    /// физически не успевает нарисоваться.
    /// </summary>
    public class SceneLoader : IReleasable, IProgressable
    {
        // Пустая сцена на время переезда: последнюю загруженную сцену Unity выгрузить не даёт.
        private const string TransitSceneName = "Transit";

        private readonly CoroutineRunner coroutines;

        private CoroutineToken routine;

        // Загрузка и выгрузка делят один токен, поэтому и запускаться должны по очереди.
        // Без этого вторая операция затирала бы токен первой, и остановить её было бы нечем.
        private bool busy;


        public event Action<LevelConfig> LoadStarted;

        public event Action<float> ProgressChanged;

        public event Action<LevelConfig> Loaded;


        /// <summary>До выгрузки — момент, когда сцена ещё жива и можно успеть сохраниться.</summary>
        public event Action<LevelConfig> Unloading;

        public event Action<LevelConfig> Unloaded;


        /// <summary>Какой уровень идёт сейчас. Сцена читает отсюда свои данные при старте.</summary>
        public LevelConfig Current { get; private set; }

        public bool IsLoading { get; private set; }

        /// <summary>0..1 для полосы загрузки.</summary>
        // IProgressable
        public float Progress { get; private set; }


        // internal: загрузчик один на игру и заводится вместе с корневым контекстом.
        internal SceneLoader(CoroutineRunner coroutines)
        {
            this.coroutines = coroutines;
        }


        /// <summary>Заменить всё, что загружено, этим уровнем.</summary>
        /// <param name="canActivate">Пока отвечает «нет», готовая сцена ждёт выключенной: её Awake
        /// и Start — начало жизни уровня, и оно должно совпасть с тем, что игрок его видит.</param>
        /// <returns>Началась ли загрузка. Нет — сцены нет в сборке или идёт другая операция.</returns>
        public bool Load(LevelConfig level, Action onComplete = null, Func<bool> canActivate = null)
        {
            return Run(level, LoadSceneMode.Single, onComplete, canActivate, false);
        }

        /// <summary>
        /// То же, но прежний уровень выгружается ДО загрузки, и память чистится: два уровня разом
        /// не лежат. Посреди переезда экран пуст — звать, только когда он закрыт.
        /// </summary>
        public bool UnloadAndLoad(LevelConfig level, Action onComplete = null, Func<bool> canActivate = null)
        {
            return Run(level, LoadSceneMode.Single, onComplete, canActivate, true);
        }

        /// <summary>Догрузить поверх уже загруженного — комната, UI-сцена, кусок мира.</summary>
        public bool LoadAdditive(LevelConfig level, Action onComplete = null)
        {
            return Run(level, LoadSceneMode.Additive, onComplete, null, false);
        }

        /// <summary>Перезапустить текущий уровень.</summary>
        public void Reload(Action onComplete = null)
        {
            if (Current != null)
                Load(Current, onComplete);
        }

        public void Unload(LevelConfig level, Action onComplete = null)
        {
            if (level == null || coroutines == null || !IsInBuild(level))
                return;

            if (busy)
            {
                Debug.LogWarning($"SceneLoader: «{level.DisplayName}» не выгружен — " +
                                 "предыдущая операция ещё идёт.");
                return;
            }

            var operation = SceneManager.UnloadSceneAsync(level.SceneName);
            if (operation == null)
                return;

            Unloading?.Invoke(level);

            busy = true;
            routine = coroutines.Run(WaitUnload(operation, level, onComplete));
        }

        // IReleasable
        public void Release()
        {
            routine.Stop();
            routine = default;

            // Флаги сбрасываем руками: корутину оборвали на середине, и её завершающая
            // часть не отработает. Иначе загрузчик навсегда остался бы «занят».
            busy = false;
            IsLoading = false;
            Progress = 0f;
        }


        private bool Run(LevelConfig level, LoadSceneMode mode, Action onComplete, Func<bool> canActivate,
            bool unloadFirst)
        {
            if (level == null || coroutines == null)
                return false;

            if (busy)
            {
                Debug.LogWarning($"SceneLoader: «{level.DisplayName}» не загружен — " +
                                 "предыдущая операция ещё идёт.");
                return false;
            }

            if (!IsInBuild(level))
                return false;

            busy = true;
            routine = coroutines.Run(LoadRoutine(level, mode, onComplete, canActivate, unloadFirst));

            return true;
        }

        private IEnumerator LoadRoutine(LevelConfig level, LoadSceneMode mode, Action onComplete,
            Func<bool> canActivate, bool unloadFirst)
        {
            IsLoading = true;
            Progress = 0f;

            LoadStarted?.Invoke(level);

            if (mode == LoadSceneMode.Single && Current != null)
                Unloading?.Invoke(Current);

            if (unloadFirst)
                yield return UnloadEverything();

            var operation = SceneManager.LoadSceneAsync(level.SceneName, mode);
            operation.allowSceneActivation = canActivate == null;

            while (!operation.isDone)
            {
                // Unity доводит progress только до 0.9, остальное — активация сцены.
                // Полоса, застрявшая на 90%, выглядит как зависание, поэтому растягиваем.
                SetProgress(Mathf.Clamp01(operation.progress / 0.9f));

                if (!operation.allowSceneActivation && canActivate())
                    operation.allowSceneActivation = true;

                yield return null;
            }

            SetProgress(1f);

            // Догруженная поверх сцена уровнем не становится: Reload перезапускает мир, а не её.
            if (mode == LoadSceneMode.Single)
                Current = level;
            IsLoading = false;
            busy = false;

            Loaded?.Invoke(level);
            onComplete?.Invoke();
        }

        // Выгрузка сцены отпускает объекты, но не ассеты: текстуры и меши уходят из памяти
        // только в UnloadUnusedAssets. Без неё прежний уровень лежал бы рядом с новым.
        private IEnumerator UnloadEverything()
        {
            var transit = OpenTransitScene();

            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene == transit || !scene.isLoaded)
                    continue;

                var unloading = SceneManager.UnloadSceneAsync(scene);

                while (unloading != null && !unloading.isDone)
                    yield return null;
            }

            // Сборщик сперва: пока живы ссылки из кода, ассет считается занятым и не уйдёт.
            GC.Collect();

            var cleanup = Resources.UnloadUnusedAssets();

            while (!cleanup.isDone)
                yield return null;
        }

        // Слушатель звука — ради тишины в консоли: без него Unity ругается каждый кадр,
        // пока ни одной сцены с камерой нет. Сама пустая сцена уйдёт вместе с загрузкой.
        private static Scene OpenTransitScene()
        {
            var transit = SceneManager.GetSceneByName(TransitSceneName);

            if (!transit.IsValid())
            {
                transit = SceneManager.CreateScene(TransitSceneName);
                SceneManager.SetActiveScene(transit);

                new GameObject(TransitSceneName).AddComponent<AudioListener>();
            }
            else
            {
                SceneManager.SetActiveScene(transit);
            }

            return transit;
        }

        private IEnumerator WaitUnload(AsyncOperation operation, LevelConfig level, Action onComplete)
        {
            while (!operation.isDone)
                yield return null;

            if (Current == level)
                Current = null;

            busy = false;

            Unloaded?.Invoke(level);
            onComplete?.Invoke();
        }

        private void SetProgress(float value)
        {
            if (Mathf.Approximately(Progress, value))
                return;

            Progress = value;
            ProgressChanged?.Invoke(value);
        }

        // Сцена не в списке сборки — самая частая причина «ничего не грузится».
        // Молчать тут нельзя: SceneManager бросит исключение уже внутри корутины.
        private static bool IsInBuild(LevelConfig level)
        {
            if (string.IsNullOrEmpty(level.SceneName))
            {
                Debug.LogError($"{level.name}: не задано имя сцены.", level);
                return false;
            }

            if (Application.CanStreamedLevelBeLoaded(level.SceneName))
                return true;

            Debug.LogError($"{level.name}: сцены «{level.SceneName}» нет в Build Settings.", level);

            return false;
        }
    }
}
