<div align="center">

# 🪶 FeatherKit

**Лёгкий каркас для старта Unity-игры.**
Контексты сервисов, пул объектов, время и пауза, загрузка уровней, сохранения,
боевой фидбек, иконки над миром, UI-заготовки, тун-шейдер и отладочные инструменты.

[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com)
[![URP](https://img.shields.io/badge/Render%20Pipeline-URP%2017-5c6bc0)](https://docs.unity3d.com/Manual/urp/urp-introduction.html)
[![Cinemachine](https://img.shields.io/badge/Cinemachine-3.x-7e57c2)](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html)
[![License: MIT](https://img.shields.io/badge/License-MIT-2e7d32)](LICENSE.md)

</div>

---

Про конкретную игру библиотека не знает ничего, и это проверяет компилятор, а не
договорённость: `FeatherKit` — отдельная сборка, игровые классы ей не видны.
Критерий, по которому сюда что-то попадает: **это понадобится и в следующей игре тоже.**

## Содержание

- [Установка](#установка)
- [Быстрый старт](#быстрый-старт)
- [Что внутри](#что-внутри)
- [Готовые префабы и шейдеры](#готовые-префабы-и-шейдеры)
- [Правила, которых держится библиотека](#правила-которых-держится-библиотека)
- [Ограничения](#ограничения)
- [Лицензия](#лицензия)

---

## Установка

**Через Package Manager:** `Window → Package Manager → + → Add package from git URL…`

```
https://github.com/gspyart/FeatherKit.git
```

**Или в `Packages/manifest.json`:**

```json
"com.gspy.featherkit": "https://github.com/gspyart/FeatherKit.git"
```

Зафиксировать версию можно меткой или коммитом: `…FeatherKit.git#v0.1.0`.

**Требования:** Unity 6000.0+, URP 17+, Cinemachine 3+, uGUI 2 (TextMeshPro внутри).
Зависимости подтянутся сами.

---

## Быстрый старт

**1. Корневой контекст** — живёт весь запуск:

```csharp
public class GameContext : AppContext
{
    public GameConfig Config { get; private set; }

    protected override void RegisterServices()
    {
        Config = Resources.Load<GameConfig>("GameConfig");
    }
}

public static class EntryPoint
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => AppContext.Launch(new GameContext());
}
```

**2. Контекст сцены** — сервисы уровня:

```csharp
public class BattleContext : SceneContext
{
    protected override void RegisterServices()
    {
        var score = Register(new ScoreCounter());

        // Зависимости — аргументами: видно в сигнатуре, что сервису нужно.
        // PoolManager уже готов — его SceneContext заводит себе до RegisterServices.
        Register(new EnemySpawner(Get<PoolManager>(), score));
    }
}
```

**3. Корень сцены** — пустой объект на сцене с этим компонентом:

```csharp
public class BattleInstaller : SceneContextInstaller<BattleContext> { }
```

Сервис теперь достаётся откуда угодно. Сценовый компонент спрашивает контекст
**по себе** — так он попадёт в контекст своей сцены, а не в чужой:

```csharp
var score = AppContext.Current.GetContext<BattleContext>(this).GetRequired<ScoreCounter>();
```

`GetRequired` вместо `Get` — если сервис забыли зарегистрировать, упадёт сразу и с именем
типа, а не `NullReferenceException` через десять кадров в другом классе.

**4. Боевой фидбек** — перетащи на сцену `Runtime/Prefabs/featherFeedbackCanvas`. Полоса
здоровья появится над тем, кому добавишь `HealthBarTarget`. Летящие числа показывает
`FloatingTextController.Show(point, value)` — что именно показывать, решает игра.

**5. Вспышка при попадании** — компонент `HitFlash` на префаб юнита, а его материалам нужен
шейдер `FeatherKit/Toon Lit`. Готовый материал — `Runtime/Materials/featherToonTemplate`,
от него и делай варианты.

### Что контекст заводит сам

Эти сервисы поднимаются вместе с контекстом. Своих заводить не надо, а первые три уже
и не выйдет — конструкторы закрыты:

| Сервис | Где взять | Что от тебя нужно |
|---|---|---|
| `UpdateRunner` | `AppContext.Current.Updates` | Реализовать `IUpdatable` и зарегистрировать сервис в контексте |
| `CoroutineRunner` | `AppContext.Current.Coroutines` | `Coroutines.Run(...)`, остановка по токену |
| `SceneLoader` | `AppContext.Current.Scenes` | `Scenes.Load(levelConfig)` |
| `AudioPlayer` | `AppContext.Current.Audio` | `Audio.Play3D(clip, point)` или `Audio.Play2D(clip)` |
| `PoolManager` | `SceneContext.Get<PoolManager>()` | `pool.Get(prefab, position, rotation)` |
| `GameTime`, `EventBus`, `DebugConsole`, `DebugPanel` | статические, отовсюду | — |

---

## Что внутри

Подробное «почему так» — в комментариях к самим классам. Здесь только что есть и зачем.

### Contexts — контейнеры сервисов

| Класс | Зачем |
|---|---|
| `Context` | База: хранилище сервисов с родителем. `Get` — можно и null, `GetRequired` — обязан быть |
| `AppContext` | Корень игры, живёт весь запуск. `GetContext<T>(this)` отдаёт контекст сцены спрашивающего |
| `SceneContext` | Контекст сцены, заводит себе `PoolManager` |
| `SceneContextInstaller<T>` | Компонент-корень сцены: поднимает контекст на `Awake`, гасит на `OnDestroy` |
| `SceneServiceResolver` | Достать сценовый компонент: есть на сцене → иначе спавним префаб |
| `FContextUtils` | `this.GetService<T>()` у любого компонента |
| `IInitializable`, `IReleasable` | `Init()` когда зарегистрированы уже все; `Release()` при выгрузке, в обратном порядке |

### Pooling, Entities — объекты из пула

| Класс | Зачем |
|---|---|
| `PoolManager` | Реестр пулов сцены: один пул на префаб. Просить можно объект или компонент |
| `ObjectPool` | Пул одного префаба. Напрямую не используется — только через `PoolManager` |
| `IPoolable` | Хуки `OnSpawned` / `OnDespawned` для сброса состояния между жизнями |
| `PoolHandle<T>` | Безопасная ссылка: сама знает, что объект уже вернулся в пул |
| `FEntityBase` | База всего, что живёт в пуле: рендереры, коллайдеры, теги, смысловой центр |
| `UnitTag` | Тег-ассет. Сравнение по ссылке, не по строке |
| `IObjectHeight` | Рост объекта — для всего, что рисуется над ним |

### Health, Modifiers — числа, которые меняются

| Класс | Зачем |
|---|---|
| `Health` | Запас здоровья. Слова «урон» в ядре нет намеренно |
| `HealthChange` | Что сделать со значением: прибавить, отнять, умножить, установить |
| `HealthChangedEvent` | Единственное событие ядра. Свои события игра объявляет у себя |
| `TimedModifiers<T>` | Вклады с ключами и сроком жизни. Наследники: `TimedBonuses`, `TimedMultipliers`, `TimedOffsets`, `TimedFlags` |

### GameTiming, Updatables, Coroutines, Timers — время

| Класс | Зачем |
|---|---|
| `GameTime` | `DeltaTime` для геймплея, `UnscaledDeltaTime` для UI. Пауза по источникам, замедление. Единственный, кто трогает `Time.timeScale` |
| `IUpdatable` | Реализуй на сервисе — контекст сам подпишет его на кадровый апдейт |
| `UpdateRunner` | Тикает всех, кто в контексте. Один на игру |
| `CoroutineRunner`, `CoroutineToken` | Корутины для обычных классов, переживают смену сцены |
| `Cooldown` | Отсчёт без MonoBehaviour: кулдаун, таймер |
| `IProgressable` | Прогресс 0..1 — общий язык для полос загрузки |

### Scenes, Saving — уровни и сохранения

| Класс | Зачем |
|---|---|
| `SceneLoader` | Загрузка с прогрессом, аддитивная, события «начал / загрузил / выгружаю / выгрузил» |
| `LevelConfig` | Уровень как данные. Свои поля игра дописывает наследником |
| `LevelTransition` | Переезд целиком: экран гаснет, сцена меняется, экран проявляется |
| `ScreenCurtain`, `LoadingScreenView` | Заслонка и экран загрузки — заготовки под свой вид |
| `SaveStorage` | JSON в файл, атомарная запись. Только хранилище |

### Events, Registry, StateMachine, Collections — связки

| Класс | Зачем |
|---|---|
| `EventBus` | Типизированная шина. Сознательно не фреймворк: ни очередей, ни приоритетов |
| `TypeRegistry<T>` | Список живых объектов одного типа. По нему можно ходить и убивать в том же цикле |
| `StateMachine`, `IState` | Переключить состояние и тикать его. Ровно это и ничего больше |
| `WeightedRandom<T>` | Взвешенный выбор |

### Feedback — что видно на события

| Класс | Зачем |
|---|---|
| `FloatingTextController`, `FloatingText`, `FloatingTextAnimationConfig` | Летящие числа: `Show(point, value)`. Префаб и есть стиль |
| `HealthBarController`, `HealthBarTarget`, `HealthBarView` | Полосы здоровья на экранном канвасе, проекцией из мира |
| `HitFlash` | Заливка модели цветом в момент удара. Нужен `_HitFlash` в шейдере |
| `DitherFade`, `MaterialCopies` | Растворение объекта по заявкам и копии материалов для него |
| `PooledEffect` | Одноразовый партикл: сам знает свою длительность и возвращается в пул |
| `UiShake` | Жест «нельзя»: дрожь элемента |

### Icons — иконки над миром

Каркас без конкретных правил: рендер → трекер → правила. Что показывать, решают
наследники `IconRule` в игре.

| Класс | Зачем |
|---|---|
| `IconsRenderer` | UI-объекты в точках мира: прижатие к краю экрана, расталкивание, пул |
| `IconsTracker`, `TrackedIcon` | Держать иконку над сущностью или точкой, пока она нужна |
| `IconRule`, `IconRulesRunner`, `IconRuleContext` | Правила-ассеты: кому и когда показывать |
| `IconRequest`, `IconView`, `OffscreenMode` | Заявка на иконку, база для скрипта на ней, что делать за экраном |

### UI — заготовки интерфейса

| Класс | Зачем |
|---|---|
| `UiWindowBase`, `ConfirmWindow`, `IShowHideAnimation` | Окно с `Open` / `Close` / `Toggle`, очередь окон, анимация показа |
| `UiBounce`, `UiPressBounce`, `TotalGrowthBounce` | Прыжок масштабом: по событию, по нажатию, по росту числа |
| `RollingNumberText` | Бегущее число в TMP |
| `FlyingIcon` | Перелёт картинки из мира или UI в цель |
| `UiCarousel`, `UiGestureRecognizer` | Карусель карточек и жесты: тап, удержание, перетаскивание |
| `ScrollList`, `ScrollEdgeFade`, `GridCellFitter`, `ViewList`, `UiObjectPool` | Списки, мягкий край прокрутки, сетка, ряд вьюх из префаба |
| `SafeAreaFitter`, `FullScreenRect` | Безопасная зона и растяжка на весь холст |

### Audio, CameraEffects, Navigation, Rendering

| Класс | Зачем |
|---|---|
| `AudioPlayer` | Разовые звуки, 2D и 3D, без спавна объектов: набор каналов крутится по кругу |
| `PooledAudioSource` | Источник звука из пула — когда нужен живой источник: зациклить, оборвать, таскать за объектом |
| `CameraShake`, `ShakeProfile`, `CameraZoom` | Тряска и зум для Cinemachine |
| `NavMeshPathfinder`, `ObstacleScanner` | Направление по навмешу без `NavMeshAgent` и обход препятствий лучами |
| `HeightFogVolume` | Туман по высоте через Volume (нужна `HeightFogFeature` в рендерере) |
| `ToonLitSettingsFeature` | Общие настройки тун-шейдера на весь проект |
| `CameraRendererOverride`, `RenderPipelineOverride` | Свой рендерер или ассет URP на время сцены |

### Debugging — отладка

| Класс | Зачем |
|---|---|
| `FLog`, `FLogColor`, `FLogPart` | Лог из кусков со своим цветом. `Info` / `Warning` вырезаются из релиза |
| `DebugConsole`, `DebugCommand`, `DebugCommandButton` | Команды текстом: «gold 500». В релизе не регистрируется ничего |
| `DebugPanel`, `DebugAnchor`, `DebugPanelStyle` | Панели «свойство — значение» на экране. В релизе вырезается |
| `FGizmos` | Фигуры гизмо: силуэт префаба, стрелки, дуги |

### Helpers — расширения

| Класс | Зачем |
|---|---|
| `FComponentUtils` | `GetOrAddComponent`. Единственный класс без пространства имён — виден везде без `using` |
| `FCollectionUtils` | Случайный элемент, N случайных без повторов, перемешать |
| `FVector3Utils`, `FTransformUtils` | `ReplaceX/Y/Z`, `SetX/Y/Z`, телепорт с Rigidbody |
| `FDistanceUtils`, `FDirectionUtils`, `FGroundUtils` | Дистанции по плоскости с поправкой на радиусы, ввод → мир, посадка на землю |
| `FCameraUtils`, `ScreenPoint` | Виден ли объект, точка мира или UI → пиксели экрана |
| `FNumberUtils`, `FTimeFormatUtils` | `10K`, `x12`, `+5`, `12:34` |
| `FObjectIdUtils` | Стабильный номер объекта на любой версии Unity |
| `FDescriptionAttribute` | Описание компонента прямо в инспекторе |

### Editor — инструменты

| Инструмент | Где |
|---|---|
| Поиск использований ассета | ПКМ по ассету → `Найти использования`, `FeatherKit/Asset Usage Finder` |
| Префабы в варианты общего шаблона с починкой ссылок | ПКМ по префабам → `Сделать вариантами шаблона…` |
| Материалы сцены: кто чем красится | `FeatherKit/Материалы сцены` |
| Иконки ассетам | `FeatherKit/Asset Icons` |
| Гизмо в Game View | `FeatherKit/Гизмо в Game View` |
| Инспекторы тун-шейдеров, база для своих инспекторов | `ToonLitShaderGUI`, `ToonTerrainShaderGUI`, `FeatherEditor` |

---

## Готовые префабы и шейдеры

Префабы лежат в `Runtime/Prefabs` и сделаны нейтральными: это заготовки под **варианты**
(ПКМ по префабу → Create → Prefab Variant). Правь вариант, а не оригинал.

| Префаб | Что это |
|---|---|
| `featherFeedbackCanvas` | Канвас с летящими числами и полосами здоровья. Один на сцену |
| `featherFloatingText` | Одна летящая надпись. Весь вид — в её TMP |
| `featherHealthBar` | Одна полоса: фон, хвост урона, шкала |
| `featherPooledAudioSource` | Источник звука из пула: `pool.Get(...).Play(clip)` |
| `featherPooledEffect` | Одноразовый партикл из пула, возвращает себя сам |
| `featherFloatingTextAnimation` | Как летят надписи: высота, разброс, кривые |

Шрифт у `featherFloatingText` не задан намеренно — TMP подставит дефолтный шрифт проекта.

| Шейдер | Что это |
|---|---|
| `FeatherKit/Toon Lit` | Ступенчатый свет, обводка, вспышка урона, растворение. Материал-шаблон — `featherToonTemplate` |
| `FeatherKit/Toon Terrain` | То же для мешей с раскраской по вершинному цвету (до четырёх слоёв) |
| `FeatherKit/Height Fog` | Туман по высоте, управляется `HeightFogVolume` |
| `FeatherKit/Masked Swirl` | Декоративный UI-эффект свечения |

---

## Правила, которых держится библиотека

- **Ядро не знает про игру.** Нужно что-то из игры — игра подставляет это сама.
- **Ничего не ищем по сцене в рантайме.** Сущность регистрируется в сервисе сама, в `OnEnable`.
- **Статику сбрасываем при входе в игру** через `[RuntimeInitializeOnLoadMethod]`:
  с выключенной перезагрузкой домена поля переживают выход из плей-мода.
- **Префикс `F`** — подпись библиотеки для того, что перекликается с Unity:
  `FLog` рядом с `Debug.Log`, `FGizmos` рядом с `Gizmos`.
- **Имена файлов ассетов — с маленькой, с префиксом `feather`.** Большая буква только
  в именах классов.
- **Зависимости — аргументами конструктора; контекст передаём, только когда иначе никак.**
  Тогда сервис принимает `Context` и достаёт нужное в `Init()`, где уже зарегистрированы все.
- **Комментарий объясняет «почему», а не «что».**

## Ограничения

- Сохранения идут через `JsonUtility`: только поля `[Serializable]`-классов. Ни словарей,
  ни свойств, ни полиморфизма.
- Пакету нужны uGUI, Cinemachine и URP. Туман по высоте работает только с Render Graph.
- `HitFlash` ждёт от шейдера `_HitFlash` и `_HitFlashColor` (имена настраиваются). Нет их —
  вспышки просто не будет.
- Дистанции и направления в `Helpers` и `Navigation` считаются по плоскости XZ: это
  под игры с видом сверху или сбоку, для вертикального геймплея бери свои.
- Нет и не планируется «на всякий случай»: локализация, аналитика, сеть, ECS.
  Появится, когда понадобится второй раз подряд.

## Лицензия

[MIT](LICENSE.md) © gspy. Пользуйся свободно, авторство сохраняй.
