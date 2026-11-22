# План этапов 2 и 4 Live Builder (архитектор)

Код не писал, Unity не запускал — всё ниже из чтения кода и сцен автора.
Ссылки `файл:строка` — на момент плана; программист этапа 1 правит `SplineWallRebuilder`, `SplineBuildingRebuilder`, `BoxModelRebuilder`, `LiveBuilderRebuildQueue` — там строки уже сдвигаются, ориентир — имя метода.
Вопросов автору нет: где был выбор, решение записано в разделе 10 с причиной.

---

## 1. Что делаем и зачем

Этапы связаны: этап 2 убирает четыре вида повтора и привязку к этажам, а заменяет их повтор этапа 4 `LiveBuilderArray`. Поэтому **ядро повтора делаем в этапе 2**, а этап 4 добавляет к нему заполнение длины, узор, ход вдоль стены, настоящие копии префабов и инструмент Module.

**Этап 2 — одно правило на одну вещь.**
- Вырез: в объекте сначала складываются все коробки, потом вычитаются все вырезы — порядок в списке не важен. Вырез любой формы — один компонент `GeometryCutter`. Что поставить внутрь выреза — во вложенный объект.
- Комната — всегда вырез объёма плюс оболочка: стены, пол, потолок по отдельности.
- Этажи — одни, уровня. Здание стоит на этаже уровня и берёт его высоту, своя — переопределением.
- Здание держит этажи дочерними объектами `Floor 1`, `Floor 2`… и `Roof`. Что положил в этаж — едет с ним, без сверки.
- Все повторы — один `LiveBuilderArray`.

Пример из игры. Подъезд 9 этажей. На первом этаже нарисовал лестницу с площадкой и в карточке поставил «до верха». Сейчас это привязка к этажам с синхронизацией высот и запретом копий на верхнем этаже. Станет: лестница лежит в `Floor 1`, на ней `LiveBuilderArray` — Count Y «до верха здания», Step Y — высота этажа, Skip Y — `last`. Добавил десятый этаж — повтор сам дал ещё одну копию, этажи выше уехали вместе с крышей.

**Этап 4 — повторы, узоры, модули.** Фасад 24 м. Инструментом Module кликаешь по стене с Shift — встаёт ряд окон-префабов вдоль стены, сколько влезет. В карточке добавил вариант B (балконная дверь), узор `ABA`, Count Y = 9, Skip Y = 1. Получается фасад этажей 2–9, проёмы прорезаны: вырез внутри каждого префаба режет стену здания.

---

## 2. Как устроено сейчас (по коду)

**Вырезы — три механизма с разными правилами.**
- Вырезающая коробка — `BoxModelPart.Mode = Cut` (`Runtime/FeatherKit/LiveBuilder/BoxModelPart.cs:31,35`). Солвер идёт по списку, и каждая следующая коробка режет всё набранное (`BoxModelSolver.cs:78-101`). Отсюда «вырезал, добавил, вырезал».
  - Стены и здания своего объекта она режет без оглядки на порядок — через проёмы в стене по линии и дыры в перекрытиях: `SplineWallRebuilder.CollectHoles` / `AddCutBoxHoles`, `SplineBuildingRebuilder.CollectSlabHoles`.
  - Исключения: коробка во всю высоту стены стену не режет (`SplineWallRebuilder.IsSpaceAroundWall`); пустота комнаты режет только здания (`BoxModelSolverBox.CutsTarget`, `BoxModelSolverBox.cs:142-145`).
  - Объект вырезающей коробки — `SplineWallRebuilder.OuterModelOf`: ближайший объект из коробок, здания для него прозрачны.
- Внутренние вырезы — у комнаты (`BoxModelShapeCollector.AddRoom`, `:206-216`) и над лестницей (`AddStairs`, `:266-272`). Вырез над лестницей стоит в списке раньше её ступеней, поэтому их не режет.
- Проём `GeometryCutter` — булево вычитание из готового меша (`GeometryCutterApplier.Cut`, `:28-34`). Кого режет — `GeometryCutterLookup.CanCut` (`:94-115`): «Авто» — внутри ближайшей постройки над ним (`OwnGroupOf`, `:150-157`), «Всё вокруг» — всё, что пересекает.

**Повторы — пять механизмов**, а не четыре: пятый нашёлся в сцене автора.
- `BoxModelRepeat` — группа повтора, копии считает сборщик: `BoxModelShapeCollector.CollectChildren`, `:83-106`.
- «Копии вверх» `CopiesUp` / `CopyStep` у стены и проёма (`IStoreyCopies`).
  - Стена копирует меш (`SplineWallMeshBuilder.cs:61-69`).
  - Проём — свои копии в вычитании, рамке, ручках, выборе: `GeometryCutterOutline.CopyToWorld` (`:29-32`), `GeometryCutterPicker.cs:87`, `GeometryCutterGizmos.cs:24`, `GeometryCutterHandles.cs:25-60`.
- `SplineBuildingStoreyLink` — «На этажах» у привязки.
  - Задаёт `CopiesUp` стенам и проёмам (`SplineBuildingLinkSync.Sync`, `:143-197`).
  - Коробкам, комнатам, лестницам и вложенному объекту копии делает сборщик (`TryGetStoreyCopies`, `:109-119`).
  - Привязка внутри привязанного копий не добавляет (`IsInsideStoreyCopies`).
- `ArrayModifier` FeatherKit на проёме — ряд вдоль стены. Его копии — настоящие скрытые объекты `HideAndDontSave` (`ArrayModifierBuilder.cs:26,78`), в игре они встают сами в `Awake` (`ArrayModifier.cs:183-187`).
- **`ArrayModifier` на `BoxModelPart` в `c1-two-towers.unity`** — 4 штуки (`Add Box` и вырезающая `Opening`, Count Y 5–6, шаг −4,5). Скрытые копии — соседи в том же родителе, сборщик берёт их как обычные коробки, в **конце** списка. В `Live Builder Showcase.unity` 8 массивов — на проёмах с привязкой.

**Комната — пять видов** (`LiveBuilderRooms.KindOf`, `:39-59`):
1. `BoxModelRoom` со стенами;
2. `BoxModelRoom` без стен — пустота в доме;
3. замкнутая стена в здании — перегородки, стороны вдоль стен здания не строятся;
4. замкнутая стена в доме из коробок;
5. здание в один этаж.

У `BoxModelRoom` `hasWalls = false` выключает и пол с потолком: `RoomShellOf` зовётся только при стенах.

**Этажи.**
- `LiveBuilderLevelStoreys` (в сцене) — высота и пол первого этажа уровня.
- У здания своя `storeyHeight` (`SplineBuilding.cs:41-43`). Совпадение с уровнем — только проверка `SplineBuildingLevelAlignment.IsAlignedToLevel` (`:26-60`).
- Части здания лежат в группах `Storey N` в начале координат здания (`SplineBuildingParts.Create` / `StoreyGroupOf`, `:58-87`).
- Интерьер — папка `Interior` (`BoxModelFactory.InteriorOf`, `:290-302`).
- Связь интерьера с этажом — только привязка: сверка двигает объект по высоте и подгоняет высоты.
  - Стена до потолка или «весь этаж».
  - Лестница и коробка «во весь этаж» — если их высота равнялась прошлой высоте этажа.
  - Комната до потолка.
  - Пропавший этаж прячет объект (`Hide` / `Show`, `:227-246`), «На крыше» едет за крышей (`SyncOnRoof`, `:200-214`).
- Здание ищут через привязку: `SplineWallBuildingAnchors.cs:31`, `SplineWallBuildingSides.cs:78`, `LiveBuilderPathOwners.LinkedBuildingOf` (`:60-69`), `LiveBuilderContourDrag.cs:202`, `LiveBuilderBuildingContext.BuildingOfPiece` (`:187-194`), `LiveBuilderOpeningPlacer.cs:73`.

**Коробки в здании — куски внешнего объекта.** Сборщик не останавливается на здании (`IsNestedConstruction` — только `BoxModel` и `SplineWall`). Поэтому лестницы и балконы из интерьера — в меше объекта, который держит здание, и вырез над лестницей пробивает перекрытия здания.

**Палитра уровня** (`Editor/FeatherKit/LevelPalette/`, своя сборка, нужен ProBuilder). Клик по префабу только подсвечивает его в Project (`LevelPaletteWindow.OnCellClicked`, `:172`). Live Builder на сборку палитры не ссылается — покраска идёт через общий реестр `ObjectMaterialPainters` в `FeatherKit.Editor`.

**Пользователи `ArrayModifier`.**
- Вне Live Builder: окно Feather Modifiers — общий инструмент на любой объект.
- Внутри: `LiveBuilderRepeatCard.cs:159-206`, подписка очереди `LiveBuilderRebuildQueue.cs:37`, `GeometryCutter : IArrayCopySize`, тест `GeometryCutterTests.ArrayOfCutters_StepsFromCutterEdge`.
- В игре — только две сцены выше, обе на объектах Live Builder. Других проектов в `Unity Projects` с ним нет.

**Данные автора, которые обязаны открыться:**
- `c1-two-towers.unity` — 11 объектов, 34 коробки, в том числе вырезающие, 2 стены, 4 массива;
- `Live Builder Showcase.unity` — 38 привязок (3 на крыше), 2 повтора (ось X), 3 комнаты, 28 проёмов, 2 здания, 2 лестницы;
- префабы `Prefabs/Scene Blocking/Floor.prefab` (объект из коробок) и `Prefabs/Blockout/Balconies/Blockout_Balcony_Loggia_4.prefab` (проём).

---

## 3. Схема данных: было → станет

| Было | Станет |
|---|---|
| `BoxModelPart.mode` Add/Cut, `cutTargets` | `BoxModelPart` — только добавляет. Поля `mode` / `cutTargets` скрыты, их читает только миграция. Вырез-коробка — `GeometryCutter`, форма Box |
| `GeometryCutter.scope`: Automatic / OwnGroup / Everything | те же числа: `OwnObject = 0` («Own object»), `LegacyOwnGroup = 1` (читается как Own object, в инспекторе не выбрать), `EverythingItTouches = 2` |
| `GeometryCutter.revealMaterial` | остаётся материалом всех стенок + новое `[Serializable] GeometryCutterSideMaterials sideMaterials`: низ, верх, бока, ±X, ±Z — как у вырезающей коробки |
| `copiesUp` / `copyStep` у `GeometryCutter` и `SplineWall`, интерфейс `IStoreyCopies` | `LiveBuilderArray` (Count Y, Step Y). Поля скрыты, их читает только миграция; интерфейс удалён |
| `BoxModelRepeat` (count, step) | `LiveBuilderArray` на том же объекте. Класс остаётся скрытым — без него Unity не прочтёт старые данные |
| `ArrayModifier` на объектах Live Builder | `LiveBuilderArray`. На остальных объектах `ArrayModifier` не трогаем |
| `SplineBuildingStoreyLink`: здание, этажи повтора, «на крыше», место на этаже, синхронизированные высоты | иерархия: объект лежит в `Floor N` / `Roof` здания (`SplineBuildingStorey`); повтор — `LiveBuilderArray`; память высот — у этажа. Класс скрыт, его читает миграция |
| у здания группы `Storey N` с частями в нуле координат здания и папка `Interior` | `Floor N` (`SplineBuildingStorey`) на высоте пола этажа: внутри части здания (Walls, Slab) и интерьер этажа; `Roof` — на верху крыши, внутри крыша и всё, что на ней |
| `SplineBuilding.storeyHeight` — всегда своя | плюс `[Serializable] SplineBuildingLevelPlacement levelPlacement`: стоит ли на этаже уровня, на каком, берёт ли высоту уровня, синхронизированные числа уровня. `storeyHeight` при высоте уровня пишет сверка. Плюс `dataVersion` |
| `BoxModelRoom.hasWalls` — вся оболочка | `hasWalls` — только стены, `hasFloor` / `hasCeiling` — независимо. Оболочка — 6 коробок, стороны на стенах здания не строятся |
| `LiveBuilderRooms.KindOf` — пять видов | комната — только `BoxModelRoom`. Замкнутая стена и здание в один этаж — просто стена и здание |
| у `BoxModel` версии нет | `dataVersion`: 0 — старое правило вырезов, 2 — новое |
| — | новые (этап 2): `LiveBuilderArray`, `SplineBuildingStorey` |
| — | новые (этап 4): поля Fill, Wall, Pattern у `LiveBuilderArray`; `LiveBuilderWallPlacement`, `LiveBuilderModule`, `LiveBuilderGeneratedCopies` |

Ловушка Unity. Если у старого объекта в файле нет нового поля, оно берёт значение из инициализатора поля, а не ноль. Поэтому:
- `dataVersion` — инициализатор 0 («старое»), а текущую версию ставят фабрика и `Reset()`;
- у `SplineBuildingLevelPlacement` инициализаторы — «как раньше»: не на этаже уровня, высота своя.

### `LiveBuilderArray` — поля

| Поле | Этап | Что |
|---|---|---|
| `countX/Y/Z` (int ≥ 1) | 2 | копий по оси, 1 — нет повтора |
| `stepX/Y/Z` (м) | 2 | шаг по оси |
| `stepYIsStoreyHeight` (по умолчанию да) | 2 | Step Y = высота этажа: здания, если повтор в нём, иначе уровня |
| `countYToBuildingTop` | 2 | Count Y = до верхнего этажа здания (вместо «И этажи выше») |
| `skipX/Y/Z` (строка) | 2 | `1, 5`, `2-4`, `last`; номера по оси с 1 |
| `mode` Count / Fill, `length`, `margin` | 4 | Fill — только по X (вдоль) |
| `along` Axes / Wall | 4 | Wall — вдоль линии стены или контура здания, с поворотом, углы пропускаются |
| `variants` (List&lt;Transform&gt; — дети), `pattern` (строка `ABA`) | 4 | узор по X; пусто в `variants` — копируется всё поддерево |
| `MaxCopies = 5000` | 2 | на цепочку вложенных повторов; сверх — копии не строятся, предупреждение на карточке |

---

## 4. Устройство после

### 4.1 Одно правило выреза

```
Вырез (источник) = GeometryCutter | пустота BoxModelRoom | вырез над BoxModelStairs
  Reach (кого может резать):
    Own object  → ближайшая постройка над вырезом (объект из коробок, стена, здание),
                  всё внутри неё, кроме вложенных объектов из коробок
    Everything  → всё, чего касается (+ нынешние запреты: свои дети, свой префаб, открытый префаб)
  Cuts: всё / только перекрытия / только стены — для стен и зданий; коробки своего объекта — всегда
  Не режет свой кусок: лестница — свои ступени, комната — свою оболочку
Внутри объекта: сначала все добавления (по списку — кому достаются грани), потом все вырезы
Как режет — выбирается само по геометрии (MethodOf):
  коробка без скруглений, оси совпадают с осями объекта →
      коробки объекта: клетки солвера; стены: проём по линии (если перекрывает толщину);
      перекрытия: дыра в плане
  иначе → вычитание из меша (как сейчас у проёма)
```

Пять решений, на которых держится правило (почему — в разделе 10):
- «Own object» — это нынешнее «Авто». Старые проёмы режут ровно то же.
- Точные способы, которыми сейчас режут вырезающие коробки, остаются для всех вырезов-коробок по осям. Поэтому старые вырезающие коробки выглядят как раньше.
- Вырез не режет свой кусок. Иначе при «сначала добавления» вырез над лестницей съел бы её верхние ступени: он начинается на 2,1 м, а ступени идут до верха.
- Исключения «пустота комнаты не режет стены» и «коробка во всю высоту стены — это комната» уходят. Перегородку внутри выреза кладут во вложенный объект — так же, как колонну в зале. Миграция делает это сама, инструмент «Стена» — тоже (раздел 6, шаг 2.3).
- Куски в этажах здания (`Floor N`) по-прежнему куски внешнего объекта. Здание и его этажи для сборщика прозрачны, как сейчас.

### 4.2 Повтор `LiveBuilderArray`: копии — часть результата

```
LiveBuilderArray (данные) ──► LiveBuilderArrayLayout: места копий одного повтора
                                (Count/Fill, Axes/Wall, Skip, Pattern, предел)
                                     │
LiveBuilderArrayCopyPoses (кеш на проход): CopiesOf(узел) — все повторы на узле и над ним,
     перемноженные; номер копии; вариант узора; граница «настоящей части» и сгенерированных копий
     │
     ├─ BoxModelShapeCollector — коробки, комнаты, лестницы, вырезы-коробки × копии → солвер
     │      (все копии одного объекта решаются вместе: шахта без щелей между этажами)
     ├─ GeometryCutterLookup / Applier / Picker / Gizmos / Handles — копии проёма (GeometryCutterCopy)
     ├─ SplineWallRebuilder — меш стены × копии (у каждой копии свои проёмы)
     ├─ SplineBuildingRebuilder — части × копии (здание под повтором — редко)
     └─ этап 4: LiveBuilderArraySync — настоящие копии «настоящих частей»
            (экземпляры префабов и не-Live-Builder объекты): сохранены в сцене,
            спрятаны в иерархии, лежат в сгенерированном контейнере рядом с оригиналом
```

**Правило поддерева.** Повтор на объекте копирует всё, что под ним, через любые постройки. Пример: повтор на группе шахты копирует её коробки, стены и проёмы. Нынешняя ловушка «проёмы во вложенном объекте не повторяются» уходит.

**Граница — настоящая часть.** Это внешний корень экземпляра префаба или объект с компонентами не Live Builder (рендерер, коллайдер, свет, свой скрипт).
- Её копирует `LiveBuilderArraySync` настоящими объектами.
- Внутри неё повторы выше не применяются расчётом — иначе копии удвоились бы.
- Внутри сгенерированного контейнера ничего не копируется второй раз.

**Копия номер 1 — место самого оригинала.** Skip и узор действуют и на неё: в примере ТЗ Skip Y = 1.
- Для данных Live Builder это просто: сборщики не строят первую копию.
- Настоящий объект, который на своём месте не показан (пропущен или там другой вариант узора), повтор выключает. Флаг «выключил повтор» — в `LiveBuilderGeneratedCopies`, так же как сейчас `IsHiddenByMissingStorey`.

**Повороты.**
- Копии вдоль стены поворачиваются. Коробки солвера — только по осям: копия коробки с поворотом не на 90° пропускается с предупреждением на карточке.
- Проёмы и префабы поворачиваются как угодно.

### 4.3 Здание и этажи

```
Section (BoxModel — внешний объект; здание всегда в нём)
└─ Building (SplineBuilding)                 стоит на этаже уровня N, высота — уровня или своя
   ├─ Floor 1 (SplineBuildingStorey, y = пол 1-го этажа)
   │   ├─ Walls, Slab   (SplineBuildingPart — меши в осях здания, сдвиг компенсирован)
   │   ├─ Stair Core    (группа, LiveBuilderArray: Y до верха, Skip Y = last)
   │   ├─ Room          (BoxModelRoom — кусок объекта Section)
   │   ├─ Partition     (SplineWall; точки держатся за стены здания — SplineWallBuildingAnchor)
   │   └─ Windows South (GeometryCutter, LiveBuilderArray X 8, Y до верха)
   ├─ Floor 2 …
   └─ Roof (SplineBuildingStorey, IsRoof, y = верх крыши): крыша + будка выхода, вентиляция
```

- Имена этажей — по нумерации уровня: здание на 3-м этаже уровня даёт `Floor 3`, `Floor 4`… Подвалы — `Floor -1`.
- **Сверка уровня** (`SplineBuildingLevelSync`).
  - Здание с высотой уровня получает `storeyHeight` уровня своей сцены.
  - Здание, стоящее на этаже уровня, встаёт низом на его пол, когда меняют этажи уровня.
  - Сдвинули здание руками — запоминается новый этаж; если не на полу — становится «свободным».
- **Сверка этажей** (`SplineBuildingStoreySync`).
  - Контейнеры есть, стоят на своей высоте, названы.
  - Этажа больше нет: контейнер с содержимым выключен, пустой удалён.
  - Содержимое подгоняется по высотам по прежним правилам привязки, только память высот теперь одна на этаж:
    - стена «до потолка» / «весь этаж»;
    - лестница и коробка «во весь этаж» — если их высота равна прошлой высоте этажа;
    - комната до потолка.

### 4.4 Комната

- Оболочка — 6 коробок: стены ±X и ±Z во всю высоту, пол и потолок под всей площадью. Каждая со своими материалами внутренней стороны. Плюс вырез объёма (Own object, режет всё).
- Добавляется до всех вырезов, поэтому соседние комнаты делят одну стену: оболочки сливаются в солвере.
- **В здании** сторона, лежащая на стене здания, не строится — та же проверка, что у перегородок: `SplineWallBuildingSides`, 5 см до внутренней стороны в начале, середине и конце. Пол и потолок новой комнаты в здании выключены — там перекрытия здания.
- **В доме из коробок** оболочка сливается с его объёмом, видна только пустота.
- Рисуется только прямоугольником.
- Многоугольная «комната» — инструментом «Стена» (замкнуть, «Пол и потолок») или «Здание» на один этаж. Инструмент «Комната» в режиме контура говорит об этом подсказкой, а не рисует.

### 4.5 Этап 4: модуль на стене

- **Клик** по стене здания, стене по линии или грани коробки — префаб из палитры уровня встаёт на стену.
  - Встаёт по нормали, `+Z` префаба смотрит наружу.
  - Низ — на полу этажа плюс `LiveBuilderModule.BottomAboveFloor`; компонента нет — 0.
  - По глубине — по `DepthAnchor`: по умолчанию середина стены.
  - Вдоль стены — по сетке от ближнего края стены, той же логикой, что «Проём» «По сетке».
- **Куда кладётся.**
  - В здании — в `Floor N` этажа под мышью.
  - У стены по линии — к родителю стены.
  - Место на линии помнит `LiveBuilderWallPlacement`: двигаешь точки — модуль едет со своим отрезком.
- **Вырез внутри префаба** с «Everything it touches» режет стену. Модуль без выреза или с вырезом «Own object» внутри своего объекта из коробок — предупреждение в подсказке.
- **Shift + клик** — контейнер `<Префаб> Row`:
  - на контейнере `LiveBuilderWallPlacement` и `LiveBuilderArray`: Along = Wall, Mode = Fill, Length = 0 (вся сторона), Margin и Step X — из настроек проекта;
  - в здании с «И этажи выше» ещё Count Y — до верха;
  - модуль — ребёнок контейнера, вариант A.
  - Кнопка «Add Variant» на карточке берёт префаб из палитры как ребёнка B.

---

## 5. Классы

### 5.1 Новые — этап 2

| Класс | Где | Что это |
|---|---|---|
| `LiveBuilderArray` | Runtime | Повтор: копии объекта и всего под ним — по осям, этажам, а в этапе 4 вдоль стены и узором. Копии — часть результата, в иерархии их нет |
| `SplineBuildingStorey` | Runtime | Этаж здания в иерархии (`Floor N`, `Roof`): здание ставит его на высоту пола, интерьер в нём едет с этажом. Помнит, под какие высоты подогнано содержимое |
| `SplineBuildingLevelPlacement` | Runtime, `[Serializable]` в `SplineBuilding` | Как здание стоит на этажах уровня: на каком, берёт ли высоту уровня, по каким числам уровня сверено |
| `GeometryCutterSideMaterials` | Runtime, `[Serializable]` в `GeometryCutter` | Материалы стенок выреза по сторонам: пол, потолок, стены, каждая сторона |
| `LiveBuilderCutRules` | Editor | Одно место на правило выреза: кого вырез может резать, что из этого, свой ли это кусок и каким способом резать |
| `LiveBuilderCutSource` | Editor, struct | Вырез глазами правила: чей он, где режет, что режет, номер копии |
| `GeometryCutterSolverBoxes` | Editor | Вырез-коробку по осям объекта переводит в коробку солвера (сначала в осях выреза, потом в осях объекта — без сдвига на миллиметр) |
| `GeometryCutterCopy` | Editor, struct | Одна копия проёма: проём, где она в мире, номер копии |
| `BoxModelRoomShell` | Editor | Оболочка комнаты коробками: какие стороны строить (в здании — без сторон на его стенах), материалы внутренних сторон |
| `BoxModelStairsLayout` | Editor | Раскладка лестницы на ступени, площадку, скаты и вырез над ней — выносится из сборщика (`BoxModelShapeCollector.cs:230-373`), там это отдельный пласт |
| `LiveBuilderArrayLayout` | Editor | Места копий одного повтора по его числам. Чистая математика, без сцены |
| `LiveBuilderArrayPose` | Editor, struct | Одна копия повтора: номера по осям, общий номер, вариант, сдвиг и поворот относительно оригинала |
| `LiveBuilderArraySkipList` | Editor, в файле `LiveBuilderArrayLayout` | Разбор строки пропусков: `1, 5`, `2-4`, `last`; ошибки — для карточки |
| `LiveBuilderArrayCopyPoses` | Editor | Копии узла с учётом всех повторов над ним (кеш на проход): для сборщиков, подписей, рамок и ручек |
| `LiveBuilderArrayActions` | Editor | Поставить, поменять, убрать повтор на выделенном с Undo: «Вверх», «По X», «По Z», «И этажи выше» |
| `LiveBuilderArrayEditor` + `LiveBuilderArrayFields` | Editor | Инспектор повтора. Поля рисует общий помощник — его возьмёт карточка этапа 3 |
| `LiveBuilderArrayCard` | Editor, `Window/Cards` | Карточка «Повтор» на вкладке «Выделено» — до панели этапа 3 |
| `LiveBuilderArrayHandle` | Editor | Ручка числа копий в окне сцены (вместо ручки повтора) |
| `SplineBuildingStoreys` | Editor | Этажи здания в иерархии: найти, создать, поставить на высоту, назвать, спрятать этаж; в каком этаже узел; здание узла |
| `SplineBuildingStoreySync` | Editor | Подгоняет содержимое этажей под высоту этажа и перекрытие — то, что делала сверка привязки |
| `SplineBuildingLevelSync` | Editor | Здания и этажи уровня: высота этажа уровня, низ на пол своего этажа, «свободное» здание |
| `GeometryCutterDefaults` | Editor | «Где режет» у нового выреза по месту: в корне уровня и в префабе без постройки над ним — «Everything it touches», иначе — «Own object» |
| `Upgrade/` — `LiveBuilderUpgrade` | Editor | Когда и в каком порядке обновлять старые данные: открытие сцены, префаба, меню на весь проект; итог — в консоль |
| `LiveBuilderUpgradeSnapshot` | Editor | «Как было»: тело каждого объекта из коробок по старому правилу и кого резал каждый вырез — для сверки после |
| `LiveBuilderLegacyShapeCollector` | Editor | Нынешний сборщик с повторами и привязками — только для «как было», удалится вместе со старыми классами |
| `LiveBuilderCutOrderUpgrade` | Editor | Коробки, стоявшие в списке после пересекающего их выреза, и стены внутри пустот — во вложенный объект; вырезы, которые их резали, дотянуть |
| `LiveBuilderCutBoxUpgrade` | Editor | Вырезающая коробка → `GeometryCutter` той же формы, материалов и «Режет»; старое «Своя группа» → «Own object», «Авто» в корне → «Everything it touches» |
| `LiveBuilderRoomUpgrade` | Editor | Старая комната → новые переключатели оболочки |
| `LiveBuilderRepeatUpgrade` | Editor | Группа повтора, копии вверх и `ArrayModifier` на объектах Live Builder → `LiveBuilderArray` |
| `LiveBuilderStoreyLinkUpgrade` | Editor | Привязки к этажам → объект в `Floor N` / `Roof` плюс повтор по Y |
| `SplineBuildingUpgrade` | Editor | Здание старого вида: внешний объект, этажи-контейнеры, части в них, флаги уровня |
| `LiveBuilderUpgradeReport` | Editor | Что поменяли и что может выглядеть иначе — строками в консоль со ссылкой на объект |
| `LiveBuilderLookDigest` | Tests | Отпечаток «по виду»: для каждого материала — плоскости граней в мире, их площадь и габарит. Не зависит от того, на какие меши и треугольники разбито |

### 5.2 Новые — этап 4

| Класс | Где | Что это |
|---|---|---|
| `LiveBuilderWallPlacement` | Runtime | Место объекта на линии стены или контура здания: двигаешь точки — объект едет со своим отрезком. Для модулей и рядов модулей |
| `LiveBuilderModule` | Runtime, на корне префаба | Как модуль встаёт на стену: низ над полом этажа, где по толщине стены. Нет компонента — опора на полу, по оси стены |
| `LiveBuilderGeneratedCopies` | Runtime, на контейнере | Контейнер настоящих копий одного оригинала: чей он, какие копии, выключил ли повтор сам оригинал |
| `LiveBuilderArrayWallPath` | Editor | Ход вдоль линии стены: точка и поворот на расстоянии, сторона между углами, попадает ли копия на угол |
| `LiveBuilderArrayRealParts` | Editor | Что в поддереве повтора копируется настоящими объектами, а что расчётом |
| `LiveBuilderArraySync` | Editor | Настоящие копии: создать, переставить, убрать, пересоздать после правки оригинала; спрятать или показать оригинал |
| `LiveBuilderArrayCopySelection` | Editor | Клик по сгенерированной копии выделяет её оригинал, а если он спрятан — объект повтора |
| `ObjectCopyFactory` | `FeatherKit.Editor` | Копия объекта как у Array FeatherKit: экземпляр того же префаба с теми же правками, иначе клон; меши ProBuilder — свои. Выносится из `ArrayModifierBuilder.CreateCopy` (`:78`), поведение Array не меняется |
| `IWallLinePlacement` | Editor | Общее у проёма и модуля «место на линии»: читать и писать отрезок, долю, сдвиг, поворот, сверенное |
| `LiveBuilderWallLineSync` | Editor | Нынешний `GeometryCutterLineSync`, переименованный и обобщённый на `IWallLinePlacement` |
| `LiveBuilderWallFitting` | Editor | Как прямоугольник встаёт на стену: точка, нормаль, вдоль, сетка от ближнего края, пол этажа. Выносится из `LiveBuilderOpeningPlacer` (`AimAtPathWall`, `LeftEdgeAlong`, `TryAlignToFaceEdge`, `FitCutterToWall`); там 457 строк, а нужен двоим |
| `LiveBuilderModuleTool` | Editor, `Tools/` | Инструмент Module: ставит префабы из палитры на стены |
| `LiveBuilderModulePlacer` | Editor | Жест модуля: призрак под мышью, клик — один, Shift + клик — ряд |
| `LiveBuilderModuleFactory` | Editor | Создаёт модуль и ряд модулей с Undo, в нужный этаж или объект |
| `LevelPaletteActiveAsset` | `FeatherKit.Editor` | Какой префаб выбран в палитре уровня сейчас. Палитра пишет, Live Builder читает: сборка палитры требует ProBuilder, ссылаться на неё нельзя |

### 5.3 Удаляются

- Editor:
  - `SplineBuildingLinkSync`, `SplineBuildingStoreyLinkEditor`, `LiveBuilderStoreyLinkCard`;
  - `LiveBuilderRepeatCard`, `LiveBuilderRepeatActions`, `LiveBuilderRepeatHandle`;
  - `LiveBuilderRooms`;
  - `SplineWallRebuilder.IsSpaceAroundWall`;
  - ветка «по контуру» у `LiveBuilderRoomGesture` и настроек комнаты;
  - всё, что читает `CopiesUp` / `CopyStep`.
- Runtime: интерфейс `IStoreyCopies`.
- Скрываются, но остаются для миграции: `BoxModelRepeat`, `SplineBuildingStoreyLink`. Им ставим `[AddComponentMenu("")]` и комментарий «читает только миграция». Удалить — отдельной уборкой, когда все сцены и префабы обновлены командой проекта.

### 5.4 Меняются (смысл класса меняется — описание переписать)

| Класс | Что меняется |
|---|---|
| `BoxModelPart` | **смысл**: только добавляет; описание и `FDescription` — заново; `mode` / `cutTargets` скрыты |
| `GeometryCutter` | **смысл**: единственный вырез, в том числе коробка внутри объекта; материалы по сторонам; «Where it cuts» — две подписи; копии вверх — только миграция |
| `BoxModelRoom` | **смысл**: всегда вырез плюс оболочка, переключатели независимы, в здании сливается со стенами |
| `SplineBuilding` | **смысл**: стоит на этаже уровня, держит этажи-контейнеры; `levelPlacement`, `dataVersion` |
| `BoxModel` | `dataVersion`; описание без «что ниже — главнее» |
| `SplineWall` | копии — только миграция; «в привязке к зданию» → «в здании» |
| `SplineBuildingPart` | вид `Floor` показывается как `Slab` (имя в перечислении — `Slab`, число то же): иначе `Floor 3/Floor` |
| `BoxModelShapeCollector` | вместо повторов и привязок — копии из `LiveBuilderArrayCopyPoses`; собирает вырезы-коробки своего объекта; комната → `BoxModelRoomShell`; лестница → `BoxModelStairsLayout` |
| `BoxModelSolverBox` | несёт `LiveBuilderCutSource`, ключ куска с номером копии, список «досягаемостей» (объект и здание над куском); `CutsTarget` / `CutTargets` уходят в правило |
| `BoxModelSolverCache` (этап 1), `BoxModelRebuilder` | правило по версии объекта: 0 — `LaterBoxesCut`, 2 — `CutsAfterAllAdds` с фильтром «досягаемость + не свой кусок» |
| `GeometryCutterLookup`, `GeometryCutterApplier`, `GeometryCutterOutline`, `GeometryCutterPicker`, `GeometryCutterGizmos`, `GeometryCutterHandles` | копии — `GeometryCutterCopy` из повторов; `CanCut` → `LiveBuilderCutRules`; из вычитания исключены вырезы, которые режут эту постройку точно |
| `SplineWallRebuilder`, `SplineWallMeshBuilder` | меш × копии, у каждой копии свои проёмы; `CollectHoles` / `CutBoxesHashOf` — через правило |
| `SplineBuildingRebuilder`, `SplineBuildingMeshBuilder`, `SplineBuildingParts` | части — в `Floor N` / `Roof` со сдвигом назад, меши те же; `CollectSlabHoles` — через правило; копии здания |
| `LiveBuilderRebuildQueue` | порядок прохода (раздел 5.5); на открытие сцены — сначала миграция |
| `BoxModelFactory` | `InteriorOf` → этаж здания; `CreateCutterBox`; версии новым объектам; здание — всегда во внешнем объекте |
| `LiveBuilderBuildingContext`, `SplineWallBuildingAnchors`, `SplineWallBuildingSides`, `LiveBuilderPathOwners`, `LiveBuilderContourDrag`, `LiveBuilderFloorRectangle` | здание и этаж — по иерархии (`SplineBuildingStoreys.BuildingOf` / `StoreyOf`), а не по привязке |
| `BoxModelDrawGesture`, `LiveBuilderOpeningPlacer`, `LiveBuilderRoomGesture`, `LiveBuilderSurfaceBoxGesture`, `BoxModelFacePiecePushPull`, `BoxModelActions`, `LiveBuilderGroups` | вырез → `GeometryCutter`; «И этажи выше» → повтор; на крышу → `Roof`; в здание → `Floor N` |
| `LiveBuilderMaterialSlots`, `GeometryCutterEditor`, `LiveBuilderBoxPartCard`, `LiveBuilderOwnComponentCard`, `SplineBuildingEditor`, `SplineWallEditor` | стороны выреза; «Where it cuts»; этаж уровня и своя высота у здания; переключатели комнаты |
| `LiveBuilderLevelCheck`, `LiveBuilderSelectionInfo`, `LiveBuilderIsolation`, `BoxModelPieceTidying` | копии в рамке; новые проверки; уборка не трогает поворот выреза |
| `ArrayModifierBuilder` (этап 4) | создание копии — через `ObjectCopyFactory`, без изменения поведения |
| `LevelPaletteWindow` (этап 4) | клик по префабу ещё и делает его выбранным (`LevelPaletteActiveAsset`) |
| `LiveBuilderReferenceLevel` / `Section` / `Yard` | на новые понятия (шаг 2.7) |

Размер. После правок около 300 строк могут дорасти `GeometryCutterLookup` (283 + копии) и `BoxModelShapeCollector` (384 − повторы − лестница + вырезы).
- Лестница выносится сразу.
- У `GeometryCutterLookup` на ревью пересчитать пласты. Кандидат в отдельную часть — блок «Подпись».

### 5.5 Главные методы и данные

**Правило:**
- `LiveBuilderCutRules.OwnConstructionOf(Transform cut)`;
- `Reaches(LiveBuilderCutSource cut, Transform target)`;
- `CutsKind(LiveBuilderCutTargets, Component target)`;
- `IsOwnPiece(cut, addBox)`;
- `MethodOf(cut, target)` → `None` / `SolverCells` / `WallHoles` / `SlabHoles` / `MeshSubtraction`.

**Солвер (этап 1):** `BoxModelSolverCache.Solve(boxes, BoxModelCutRule)`. Для `CutsAfterAllAdds` резчики коробки i такие:
- вырезы c, которые пересекают её, у которых `c.ReachId ∈ i.ReachIds` и `c.PieceKey ≠ i.PieceKey`;
- плюс более поздние добавляющие, которые её пересекают.

Ключ кеша — коробка плюс её резчики, как в этапе 1. Если досягаемость поменялась, набор резчиков другой — и ключ другой сам.

**Повтор:**
- `LiveBuilderArrayLayout.PosesOf(LiveBuilderArray, LiveBuilderArrayContext)` → `List<LiveBuilderArrayPose>`. Контекст — высота этажа и этажей до верха, ширина копии, этап 4 — путь стены.
- `LiveBuilderArrayCopyPoses`:
  - `BeginPass()` / `EndPass()`;
  - `CopiesOf(Transform node)` → `IReadOnlyList<LiveBuilderCopy>` — мировая матрица копии, ключ копии;
  - `KeyOf(Transform node)` — для подписей;
  - `CopyInView(Transform node, Camera)` — копия, на которой рисовать ручки;
  - `WarningsOf(LiveBuilderArray)` — сверх предела, копии на изгибе, ошибка в Skip.
- `LiveBuilderArraySync.SyncAll()` (этап 4).

**Этажи:**
- `SplineBuildingStoreys`: `ContainerOf(building, storey)`, `RoofOf(building)`, `BuildingOf(Transform)`, `StoreyOf(Transform)`, `NameOf(building, storey)`, `PlaceContainers(building)`;
- `SplineBuildingStoreySync.SyncAll()`;
- `SplineBuildingLevelSync.SyncAll()`.

**Порядок прохода очереди:**

```
LevelSync → StoreySync → AnchorSync → WallLineSync → ArraySync (этап 4)
→ ArrayCopyPoses.BeginPass → BoxModels → Buildings → Walls → EndPass
```

**Миграция:**
- `LiveBuilderUpgrade.UpgradeScene(Scene)`, `UpgradePrefab(path)`;
- `NeedsUpgrade(GameObject root)`;
- меню `Tools/FeatherKit/Live Builder/Upgrade Project Data…`.

---

## 6. Шаги — этап 2

Каждый шаг закончен: собирается, тесты зелёные, его можно сдать отдельно.
Пока миграции нет (до шага 2.6), старые компоненты работают по-старому. Старые объекты (версия 0) режутся старым правилом. Новые объекты, которые делают инструменты, — уже по-новому.

**Шаг 2.0. Эталоны «как было» — до любых правок, на коде этапа 1**
- `LiveBuilderLookDigest` (тесты) — отпечаток «по виду» по всему уровню.
- Записать его для `LiveBuilderReferenceLevel`: `Tests/Editor/LiveBuilder/Baselines/ReferenceLevelLook.txt`.
- Сцены-образцы `Tests/Editor/LiveBuilder/UpgradeFixtures/*.unity`, собранные **нынешним кодом**, у каждой — отпечаток «по виду»:
  - `AddAfterCut` — зал-вырез, колонна после него, ещё вырез поверх колонны;
  - `RoomKinds` — все пять видов комнат;
  - `StoreyLinks` — «только этот», «по этаж», «до верха», без верхнего у лестницы, на крыше, пропавший этаж, привязка в привязанном, дверь в перегородке с другим числом копий, вырез шахты в привязанной группе;
  - `Repeats` — по оси, вложенный, по диагонали;
  - `CopiesUp` — стена и проём без здания;
  - `ArrayModifierOnParts` — коробка и вырез, как в c1;
  - `ArrayModifierOnCutters` — ряд с привязкой, как в Showcase;
  - `CutBoxes` — в интерьере здания, во всю высоту стены, мельче стены, «Только перекрытия»;
  - `LegacyPrefab` — префаб с вырезающей коробкой и привязкой.
- Копии сцен автора — только копии, в папку тестов, не в git игры. С них снять отпечатки «по виду» по корням.
- Тесты: `UpgradeFixtures_AreReadable`.

**Шаг 2.1. Одно правило выреза**
- `LiveBuilderCutRules`, `LiveBuilderCutSource`; поля резчика у `BoxModelSolverBox`.
- Солвер:
  - `BoxModel.dataVersion`; фабрика ставит 2;
  - `BoxModelRebuilder` берёт `CutsAfterAllAdds` для версии 2;
  - фильтр «досягаемость + не свой кусок».
- Вырез над лестницей и пустота комнаты получают «Own object», каждый — свой «Режет».
- `SplineWallRebuilder.CollectHoles` / `CutBoxesHashOf`, `SplineBuildingRebuilder.CollectSlabHoles`, `GeometryCutterLookup.CanCut` — через `LiveBuilderCutRules`. У версии 2 нет `IsSpaceAroundWall` и исключения «пустота не режет стены».
- `GeometryCutterScope`: подписи «Own object» и «Everything it touches»; `LegacyOwnGroup` читается как Own object и в инспекторе не выбирается.
- `GeometryCutterDefaults` (`ObjectFactory.componentWasAdded`): «Где режет» по месту.
- «Own object» без постройки над ним не режет ничего — инспектор об этом пишет.
- Тесты `LiveBuilderCutRuleTests`:
  - коробка до и после выреза — одно и то же;
  - вложенный объект цел;
  - верхние ступени под своим вырезом целы;
  - оболочка комнаты цела, перегородка в комнате режется, а во вложенном объекте цела;
  - Own object / Everything через группы и префаб;
  - Cuts — все три значения;
  - объект версии 0 режется по-старому;
  - кеш этапа 1 совпадает с эталонным `Solve` для нового правила.
- Старые тесты, которые закрепляли исключения (`LiveBuilderCutRulesTests.RoomInterior_DoesNotCutPartitionInsideIt`, `:58`), переписать на новое правило: «во вложенном объекте цела».

**Шаг 2.2. Один компонент выреза**
- `GeometryCutterSideMaterials`.
- `GeometryCutterSolverBoxes`: вырез-коробка без скруглений, оси которого совпадают с осями объекта, → коробка солвера. Угол считается в осях выреза (`сдвиг − половина`), потом переводится в оси объекта.
- `MethodOf`:
  - коробки своего объекта → клетки;
  - стены → проём по линии, если коробка перекрывает толщину стены на этом отрезке, иначе вычитание (ниша);
  - перекрытия → дыра в плане;
  - всё остальное → вычитание. `GeometryCutterLookup` не отдаёт в вычитание то, что режется точно.
- Проём по линии и дыра в плане берут материал стенок у выреза (сторона или общий), иначе — как сейчас.
- Вычитание из перекрытия без своего материала: стенки дыры — материал торцов здания (`OuterWallMaterial`), а не «самый частый рядом». Так дыра выглядит как от вырезающей коробки.
- Инструменты, что ставили вырезающую `BoxModelPart`, ставят `GeometryCutter` (`BoxModelFactory.CreateCutterBox`):
  - `BoxModelDrawGesture` (`:276`, режим Cut / Авто внутрь);
  - `BoxModelFacePiecePushPull` (`:98`);
  - `LiveBuilderOpeningPlacer` (`:340`);
  - `LiveBuilderSurfaceBoxGesture` (`:142`);
  - `BoxModelActions` (`:52` — «Add ⇄ Cut» превращает коробку в вырез и обратно, один Ctrl+Z).
- Ручки граней, выделение граней, покраска (`LiveBuilderMaterialSlots`), выбор кликом (`GeometryCutterPicker.cs:96`) — у выреза-коробки как у вырезающей коробки.
- `LiveBuilderOpeningTool.CanEditTarget` (`:76`) — только `GeometryCutter`.
- Тесты `GeometryCutterBoxTests`:
  - вырез-коробка даёт те же клетки, что вырезающая коробка в том же месте (до байта, через `BoxModelSolver.Solve`);
  - повёрнутая на 30° — вычитанием;
  - мельче стены — ниша;
  - материалы сторон на гранях дыры;
  - «Add ⇄ Cut» и Ctrl+Z.

**Шаг 2.3. Комната — одно поведение**
- `BoxModelRoomShell`; `hasWalls` — только стены; стороны на стенах здания не строятся.
- `LiveBuilderRooms` удалить. Карточка и строка о выделенном пишут «Комната» только у `BoxModelRoom`.
- Инструмент «Комната» — только прямоугольник.
  - В здании — в этаж, толщина стен — перегородки из настроек, пол и потолок выключены.
  - В доме из коробок и в пустоте — как сейчас.
- Инструмент «Стена»: линия, нарисованная внутри пустоты или выреза объекта, встаёт во вложенный объект `Kept From Cuts` того же родителя. Подсказка говорит об этом заранее.
- Тесты `BoxModelRoomTests`:
  - переключатели по отдельности;
  - стороны на стенах здания не строятся;
  - соседние комнаты — одна стена;
  - в доме из коробок видна только пустота;
  - материалы пола, стен и потолка изнутри;
  - стена внутри комнаты встаёт во вложенный объект и цела.

**Шаг 2.4. Ядро повтора**
- `LiveBuilderArray`, поля этапа 2.
- `LiveBuilderArrayLayout`, `LiveBuilderArraySkipList`, `LiveBuilderArrayCopyPoses`.
- Встроить копии:
  - сборщик — все копии одного объекта в один солвер;
  - копии проёма — вычитание, ключ, рамка, ручки «на копии в середине окна», выбор кликом;
  - меш стены × копии с проёмами каждой копии;
  - здание × копии;
  - рамка выделенного и изоляция.
- Предел 5000: копии сверх не строятся, на карточке предупреждение. Старый предел коробок солвера остаётся.
- Подписи: `LiveBuilderArrayCopyPoses.KeyOf` вместо `CopiesUp` / `CopyStep`. Старые постройки один раз пересоберутся.
- Область пересборки (этап 1, `LiveBuilderRebuildScope`): правка повтора задевает постройки в его поддереве, а также всё, что резали копии его вырезов раньше и режут теперь (обратная карта этапа 1 — по копиям).
- `LiveBuilderArrayActions`, `LiveBuilderArrayCard`, `LiveBuilderArrayEditor` / `Fields`, `LiveBuilderArrayHandle`. Старые карточка, действия и ручка повтора больше не создают старых компонентов, только повтор.
- Тесты `LiveBuilderArrayTests`:
  - число и шаг по осям;
  - вложенные повторы перемножаются;
  - 5000 + предупреждение;
  - разбор Skip: `1, 5`, `2-4`, `last`, ошибка;
  - копии выреза режут стену и здание;
  - копии стены;
  - копии вложенного объекта — один меш, шахта без щелей;
  - дверь в стене под повтором копируется вместе со стеной;
  - сдвиг одной коробки пересчитывает только её копии (счётчик кеша этапа 1).

**Шаг 2.5. Этажи уровня и здания в иерархии**
- `SplineBuildingLevelPlacement`, `SplineBuildingLevelSync`; нумерация — функция уровня (`LiveBuilderStoreys.NumberOf`).
- `SplineBuildingStorey`, `SplineBuildingStoreys`, `SplineBuildingStoreySync`. Части здания в этажах со сдвигом назад: меши в осях здания не меняются.
- Повтор: Step Y = высота этажа здания, Count Y «до верха». Копии содержимого этажа не выходят выше верхнего этажа.
- Здание узла — по иерархии везде, где брали привязку (список в разделе 2).
- Инструменты кладут в `Floor N` и `Roof` и ставят повтор вместо привязки:
  - `BoxModelDrawGesture` — на крышу (`:336`, `:363`, `:386-394`);
  - `LiveBuilderOpeningPlacer` — «И этажи выше» (`:73`);
  - лестница — Skip Y `last`; «И на последнем этаже» убирает `last`;
  - группы (`LiveBuilderGroups.CreateGroupInside`), «Новый объект» на полу здания, комната, стена.
- Новые постройки: здание — сразу во внешнем объекте, `dataVersion` и флаги уровня.
- Карточка здания: «On level storey N», «Storey height: level / own».
- Тесты `SplineBuildingStoreyTests`:
  - этажи созданы, стоят на высоте, названы по уровню, подвал `Floor -1`;
  - сменили высоту этажа — этажи и интерьер уехали, перегородки до потолка и «весь этаж», лестница и коробка во весь этаж, комната до потолка подогнались, своя высота осталась;
  - 9 → 8: `Floor 9` с содержимым выключен, пустой убран; 8 → 9 — вернулся;
  - `Roof` едет за крышей;
  - уровень поменял высоту — здание с высотой уровня перестроилось, стоящее на этаже встало на новый пол, своё не тронуто;
  - двигаешь здание руками — новый этаж или «свободное»;
  - балкон «И этажи выше» в `Floor 1` — на всех этажах.
- `LiveBuilderToolGestureTests` переписать на повтор и этажи: балкон, ограждение, второй марш, коробка на крыше в `Roof`.

**Шаг 2.6. Миграция** (раздел 8)
- Хуки, снимок «как было», преобразования, сверка, итог, меню на проект. Потом удалить старое: сверку привязки, сборщик повторов и привязок, карточки.
- Тесты `LiveBuilderUpgradeTests` — по каждой сцене-образцу шага 2.0:
  - после миграции отпечаток «по виду» совпадает с записанным;
  - строки итога — ожидаемые;
  - второе открытие ничего не меняет и сцену не помечает изменённой;
  - префаб обновлён один раз, экземпляр без лишних правок.

**Шаг 2.7. Эталон и документы**
- `LiveBuilderReferenceLevel` на новых понятиях (раздел 9).
  - `Meshes_LookSameAsBaseline` — отпечаток «по виду» против шага 2.0.
  - Точный отпечаток этапа 1 перезаписать: новый эталон для этапов 3–6.
- `Measure Rebuild` — снова; бюджеты этапа 1 должны держаться. Цифры — в отчёт.
- `LiveBuilder.md`: «Понятия» — на один экран (черновик — раздел 11); разделы про повтор, привязку, комнаты, группы, «Режет / Где режет» — переписать; убранное — удалить.
- Отчёт: миграция копий сцен автора — какие объекты поменяли устройство и какие могут выглядеть иначе.

---

## 7. Шаги — этап 4 (после этапа 3)

**Шаг 4.1. Fill и узор**
- Поля `mode`, `length`, `margin`, `variants`, `pattern`.
- Fill по X: копий n = ⌊(L − 2M − w) / Step X⌋ + 1, не меньше 1; шаг растягивается до (L − 2M − w) / (n − 1). Здесь w — ширина копии: самая широкая из вариантов, по `IArrayCopySize`, `LiveBuilderModule` или габариту.
  - Along = Axes: первая копия — сам оригинал, L — от его края.
  - Along = Wall и L = 0: вся сторона между углами, оригинал переезжает на первое место.
- Узор — по номеру X; строки Y и Z повторяют его. Пропуски не сдвигают узор.
- Вариант копии считается по номеру: `pattern[(i − 1) mod длину]`. Пустой узор при заданных вариантах — всегда A.
- Для данных Live Builder вариант решает сборщик: узел под вариантом B даёт геометрию только в копиях с буквой B.
- Тесты `LiveBuilderArrayLayoutTests`: Fill — L, M, w, шаг; `AB`, `ABBA`, `ABA` и пусто; Skip вместе с узором.

**Шаг 4.2. Настоящие копии**
- `ObjectCopyFactory` — вынос из `ArrayModifierBuilder`; тест Array FeatherKit не меняется.
- `LiveBuilderArrayRealParts`, `LiveBuilderGeneratedCopies`, `LiveBuilderArraySync`.
  - Контейнер `<имя> Copies` — сосед оригинала, `HideFlags.HideInHierarchy`, сохраняется в сцену.
  - Копии внутри — `NotEditable`, создаются без Undo: это след чисел повтора, после Ctrl+Z числа вернутся и копии пересоберутся.
- Оригинал, не показанный на своём месте, выключен, флаг — в контейнере. Показан снова — включён.
- Правка оригинала (переопределения префаба, состав, компоненты) пересоздаёт копии — по подписи содержимого, как `ArrayModifierBuilder.ContentOf`.
- `LiveBuilderArrayCopySelection`.
- Вырезы в настоящих копиях — обычные `GeometryCutter`, их находит `GeometryCutterLookup`.
- Тесты `LiveBuilderArrayRealCopyTests`:
  - префаб × 3: копии — экземпляры, сохраняются, в иерархии не видны;
  - их вырезы режут стену;
  - Skip 1 и узор прячут оригинал, показываются обратно;
  - правка префаба-ассета доходит до копий;
  - Ctrl+Z числа;
  - открытый префаб с повтором внутри;
  - клик по копии выделяет оригинал;
  - вход в Play: копии на месте и в мире.

**Шаг 4.3. Вдоль стены**
- `LiveBuilderWallPlacement`, `IWallLinePlacement`, `LiveBuilderWallLineSync` (`GeometryCutter` — через свои поля, без переноса: перенос обнулил бы старые значения).
- `LiveBuilderArrayWallPath`:
  - копия — точка и поворот пути на расстоянии от места оригинала;
  - Y — вверх, Z — по нормали;
  - копия, которая шириной накрывает излом больше 1°, пропущена;
  - стороны между углами.
- Тесты:
  - ряд вдоль Г-образного здания с пропуском угла;
  - кривая стена — проёмы поворачиваются, коробки на изгибе пропущены с предупреждением;
  - двигаешь контур — ряд едет.

**Шаг 4.4. Инструмент Module**
- `LevelPaletteActiveAsset` (клик по префабу в палитре).
- `LiveBuilderModule`, `LiveBuilderWallFitting` (вынос из «Проёма»), `LiveBuilderModuleTool` / `Placer` / `Factory`.
- Место в палитре — по списку этапа 3 (Module — седьмой). Перетаскивание префаба на стену при Module — то же, что клик.
- Тесты через `LiveBuilderToolDriver`:
  - клик по стене здания — модуль по нормали, низ — пол этажа плюс высота модуля, по сетке от края;
  - стена прорезана;
  - Shift + клик — ряд Fill;
  - модуль без выреза — предупреждение;
  - контур здания сдвинули — модуль уехал.
- `FacadePatternExample` (тест-сцена, префабы окна и балкона — во временной папке теста): фасад 24 м, Wall, Fill, `ABA`, Count Y 9, Skip Y 1.
  - Лучом сквозь каждый проём этажей 2–9 — дыра.
  - Первый этаж цел.
  - Балконы на 2–9.

**Шаг 4.5. Документы и замер**
- `LiveBuilder.md`: повтор — полностью, Module — свой раздел.
- `Measure Rebuild` — ещё раз.
- Руками у автора: пример ТЗ за минуту.

---

## 8. Миграция старых данных

**Когда.**
- `EditorSceneManager.sceneOpened`; `PrefabStage.prefabStageOpened`.
- Меню `Tools/FeatherKit/Live Builder/Upgrade Project Data…` — сначала список, что изменится, потом подтверждение.
- При открытии сцены сначала обновляются префабы-ассеты, чьи экземпляры в ней старые: базовые раньше вариантов, `LoadPrefabContents` → миграция → `SaveAsPrefabAsset`. Потом объекты самой сцены.

**Как.**
- Без Undo: это обновление при загрузке, как у ассетов Unity. Сцена помечается изменённой — откатить можно, закрыв без сохранения.
- Префабы пишутся на диск сразу, итог их перечисляет.

**Идемпотентно.**
- `BoxModel.dataVersion` (0 → 2) и `SplineBuilding.dataVersion` (0 → 2).
- Плюс признаки старых данных: есть `SplineBuildingStoreyLink`, `BoxModelRepeat`, `ArrayModifier` на объекте Live Builder; `copiesUp > 1`; `BoxModelPart.mode = Cut`; `scope = LegacyOwnGroup`.
- Нет ни одного признака — ничего не делается и ничего не помечается.

**Порядок внутри сцены** (`LiveBuilderUpgrade.UpgradeScene`):
1. Достроить скрытые копии `ArrayModifier` (`ArrayModifierBuilder.Rebuild`) — «как было» должно быть полным.
2. Снимок «как было» (`LiveBuilderUpgradeSnapshot`):
   - тело каждого объекта из коробок — `LiveBuilderLegacyShapeCollector` + `BoxModelSolver.Solve` по старому правилу, клетки в миллиметрах;
   - для каждой стены, здания и объекта — какие вырезы их режут.
3. Преобразования (ниже, по пунктам).
4. Сверка «как стало» — те же данные по новым правилам.
   - Тело сравнивается точно: объём клеток и объём их пересечения.
   - Добавленные миграцией вложенные объекты считаются вместе с родителем.
5. Где не совпало — правки F1–F4. Ещё раз сверка.
6. Версии и итог.

### 8.1 Порядок вырезов (F1–F4, `LiveBuilderCutOrderUpgrade`)

- **F1.** Добавляющий кусок (коробка, оболочка комнаты, ступени), который в старом списке стоял после пересекающего его выреза, уезжает во вложенный объект `Kept From Cuts`.
  - Объект создаётся у того же родителя, на месте куска, мир — тот же.
  - Под теми же повторами — значит, копии сохраняются.
  - **По виду:** то же. Внутри тела появятся невидимые грани. Если кусок вровень с наружной гранью родителя, возможно мерцание — строка итога.
- **F2.** Стену или здание, которые теперь режет то, что раньше не резало (пустота комнаты, коробка во всю высоту стены), — так же во вложенный объект. **По виду:** то же.
- **F3.** Вырезы, которые раньше резали перенесённое (стояли после него), дотягиваются до вложенного объекта.
  - Если «Everything it touches» не добавит ему новых целей (проверка по габаритам всего вокруг), меняется только «Где режет».
  - Иначе — копия выреза во вложенном объекте, со строкой итога: две копии надо держать вместе.
- **F4.** Вырез, который больше не достаёт цель. Так бывает с вырезающей коробкой в интерьере здания: раньше она резала весь внешний объект, теперь — здание. Правка — «Everything it touches», если это не добавит целей. Иначе — строка «может выглядеть иначе».
- Если в конфликте участвует копия повтора (вторая копия коробки после выреза первой), повтор сначала разворачивается в обычные объекты. Строка итога.

### 8.2 Вырезающие коробки (`LiveBuilderCutBoxUpgrade`)

- На том же объекте: `BoxModelPart` уходит, ставится `GeometryCutter`:
  - форма Box, ширина и высота из размера, глубина — Z;
  - `shapeOffset = (x/2, 0, z/2)` — опора объекта остаётся в углу;
  - материалы — в `GeometryCutterSideMaterials`;
  - `cutTargets` — тот же;
  - «Где режет» — Own object.
- Имя `Cut Box` остаётся.
- **По виду:** коробки, стены, перекрытия — тем же точным способом. Если коробка мельче толщины стены, которую резала насквозь, будет ниша — строка итога.

### 8.3 Проёмы (`LiveBuilderCutBoxUpgrade`)

- `LegacyOwnGroup` → Own object.
- «Авто» без постройки над проёмом → «Everything it touches»: как и было, «всё, что пересекает».
- **По виду:** то же. Проём-коробка по осям, сквозной, режет стену теперь точным проёмом по линии: форма та же, треугольники другие.

### 8.4 Комнаты (`LiveBuilderRoomUpgrade`) — все пять видов

1. `BoxModelRoom` со стенами — переключатели как были, оболочка коробками. **По виду:** то же.
2. `BoxModelRoom` без стен → стены, пол, потолок выключены. **По виду:** то же, видна только пустота.
3. Замкнутая стена-перегородка в здании — остаётся стеной; уезжает в `Floor N` (8.6), стороны на стенах здания — как были. **По виду:** то же.
4. Замкнутая стена в доме из коробок — остаётся. Если внутри пустоты или выреза — F2. **По виду:** то же.
5. Здание в один этаж — остаётся зданием, флаги уровня (8.7). **По виду:** то же.

### 8.5 Повторы (`LiveBuilderRepeatUpgrade`)

- **`BoxModelRepeat`** → `LiveBuilderArray` на том же объекте.
  - Шаг по одной оси → Count и Step этой оси.
  - Вложенные повторы → вложенные повторы.
  - Шаг сразу по двум осям → повтор разворачивается в обычные коробки, строка итога.
- **Копии вверх** у стены и проёма вне здания → Count Y и Step Y — свои числа, без высоты этажа; `copiesUp = 1`.
- **`ArrayModifier` на объекте Live Builder** (проём, коробка, комната, лестница, стена, объект из коробок):
  - сетка → Count и Step по осям, «от края» → размер плюс зазор;
  - пропуски в одном ряду → Skip этой оси;
  - пропуски в сетке 2D / 3D → развернуть с итогом;
  - круг → оставить `ArrayModifier` (он и дальше работает), строка итога.
  - Скрытые копии убираются (`ArrayModifierBuilder.Remove`).
  - Повтор Y из привязки на том же объекте складывается в тот же `LiveBuilderArray`. Одна и та же ось у двух — обёртка-родитель со вторым повтором.
- **По виду:** то же. У c1 скрытые копии стояли в конце списка — то, что это меняло в порядке, ловит F1.

### 8.6 Привязки к этажам (`LiveBuilderStoreyLinkUpgrade`)

Сначала здания (8.7) — этажи должны уже быть. Потом каждая привязка L на объекте X, от верхних в иерархии к нижним:
- **«На крыше»** → X в `Roof`, мир тот же.
- **Иначе** → X в `Floor <L.Storey>`.
  - Если X лежал в папке внутри `Interior`, папка с тем же именем появляется в этаже.
  - Если X лежал в другом объекте из коробок, чем здание, — оставить на месте, дать повтор, строка «не едет с этажом»: перенос поменял бы, чей он кусок.
- **Копии** — по `CopiesFrom`:
  - «только этот» — без повтора;
  - «до верха» → Count Y «до верха», Step Y — этаж, Skip Y `last`, если `SkipsTopStorey`;
  - «по этаж K» → Count Y = K − Storey + 1. Копии выше здания и так не строятся — как раньше `CopiesFrom` ограничивало.
- **Привязка внутри привязанного** — свой повтор не нужен: копии даёт повтор выше, как раньше.
- **Повтор выше даёт ребёнку другое число копий**, чем было у ребёнка. Так бывает с дверью в перегородке, привязанной не так, как перегородка, или с высоким вырезом шахты в привязанной группе.
  - Ребёнок выезжает к ближайшему предку без повтора (обычно в `Floor N`), мир тот же.
  - Если «Где режет» = Own object и после выезда он достаёт не то — строка итога.
  - У двери в стене теряется «место на линии» — строка итога.
- **Пропавший этаж** (`IsHiddenByMissingStorey`) → X включается, а выключен его этаж.
- **Память высот** привязки → у этажа: прошлая высота этажа и перекрытие здания.
- Привязка удаляется.
- **По виду:** то же. Исключения перечисляет итог.

### 8.7 Здания (`SplineBuildingUpgrade`)

- Здание в корне сцены оборачивается в новый объект из коробок `<имя> Object` на том же месте. В корне префаба — не оборачивается, строка итога.
- Группы `Storey N` → `Floor N` (`SplineBuildingStorey`) на высоте пола этажа; части внутри — со сдвигом назад, меши те же.
- `Roof`-контейнер, крыша — в него.
- `Interior`: привязанное разъезжается по 8.6. Непривязанное — по высоте в этаж или `Roof`, мир тот же. Пустой `Interior` удаляется.
- Флаги уровня:
  - высота уровня — если она совпадает с высотой здания и этажи уровня лежат в сцене;
  - «стоит на этаже уровня» — если низ на полу этажа уровня (`SplineBuildingLevelAlignment.IsOnLevelFloor`);
  - иначе — своя высота и «свободное».
- **По виду:** то же.

### 8.8 Итог в консоль (по-английски)

Одна строка на сцену или префаб плюс строки по объектам со ссылкой, клик выделяет.
```
Live Builder: upgraded 'c1-two-towers' — 9 objects restructured, 1 may look different, 2 prefabs upgraded (Floor.prefab, …).
  Restructured: 'Tower A/Opening' — cut box became a Cut; 'Hall/Column' moved into 'Kept From Cuts' to stay uncut.
  May look different: 'Tower B/Niche' — cut box thinner than the wall: now a niche, was a hole.
```

---

## 9. Как проверить

**Тесты EditMode** — все старые зелёные или переписаны под новое правило, плюс новые из шагов.
- Жесты — через `LiveBuilderToolDriver`.
- Перед `run_tests` (MCP) убедиться, что не открыта и не изменена ни одна сцена автора: он сохраняет открытые изменённые сцены, а миграция при открытии их помечает изменёнными.

**Эталонный уровень на новых понятиях.**

| Было в этапе 1 | Станет |
|---|---|
| 16 проёмов, каждый со своей привязкой «до верха» (`LiveBuilderReferenceSection.cs:117-120`) | 2 проёма в `Floor 1`, у каждого `LiveBuilderArray` X 8 (тот же шаг), Y «до верха» |
| лестница в интерьере с привязкой «до верха», без верхнего (`:140-152`) | в `Floor 1`, повтор Y «до верха», Skip Y `last` |
| группа шахты с привязкой и высокий вырез «Всё вокруг / Только перекрытия» в ней (`:159-178`) | группа в `Floor 1` с повтором; вырез перекрытий — на один этаж, его копирует повтор группы |
| перегородки и двери 1-го этажа (`:215-222`) | в `Floor 1`, двери — дети стен |
| балконы с привязкой «до верха» (`:252-254`) | в `Floor 1`, повтор Y |
| двор: ограда повтором 150, ниши — вырезающая коробка повтором (`LiveBuilderReferenceYard.cs:80`) | `LiveBuilderArray` X 150; ниши — `GeometryCutter`-коробка с повтором X 28 |

- `Meshes_LookSameAsBaseline` — отпечаток «по виду» против шага 2.0.
- Точный отпечаток и отпечаток «по форме» этапа 1 сюда не годятся: пути объектов и разбиение на клетки меняются (оболочка комнаты — 6 коробок), а вид — нет.

**Сцены автора** — только копии.
- Миграция, отпечатки «по виду» по корням до и после, итог в отчёт.
- Оригиналы автор открывает сам: миграция пометит сцену изменённой, сохранять — ему.

**Руками (автор).**
- Этап 2:
  - эталон;
  - старые сцены — Showcase и c1;
  - лестница с площадкой на 9 этажей, 9 → 10;
  - колонна в зале во вложенном объекте;
  - комната в здании — стороны на стенах не строятся;
  - этажи уровня — сменить высоту.
- Этап 4: пример ТЗ за минуту; ряд вдоль Г-образного здания; модуль едет за контуром.

**Скорость.** `Measure Rebuild` после шагов 2.4, 2.7 и 4.5. Бюджеты этапа 1 держатся.
- Риск — сдвиг окна, у которого 8 × 9 копий: каждая копия — отдельная подпись и вычитание.
- Решение заранее: если сдвиг проёма дольше 16 мс, заготовка этапа 1 «меш без двигаемого проёма» строится по всем копиям двигаемого проёма сразу.

---

## 10. Решения вместо вопросов

1. **Вырез — только `GeometryCutter`, `BoxModelPart` только добавляет.** Почему: ТЗ — «один компонент выреза», так устроен Chisel (форма плюс операция).
2. **Способ резать выбирается сам: коробка по осям — точно, остальное — вычитанием.** Почему: старые вырезающие коробки выглядят как раньше, а человек видит одно правило.
3. **«Own object» = нынешнее «Авто»: ближайшая постройка, без вложенных объектов из коробок.** Почему: старые проёмы режут то же самое.
4. **Вырез не режет свой кусок.** Почему: иначе вырез над лестницей съест её верх.
5. **Исключения «пустота не режет стены» и «коробка во всю высоту — комната» убраны.** Почему: одно правило. Защита — вложенный объект, его ставит миграция и инструмент «Стена».
6. **Оболочка комнаты — 6 коробок, стороны на стенах здания не строятся.** Почему: стены, пол, потолок включаются по отдельности, и нет двойных стен со зданием.
7. **Комната — только прямоугольник, контур — «Стена» или «Здание».** Почему: этап 3 делает «Комнату» режимом «Коробки», а у коробки контура нет.
8. **Этажи — `Floor N` и `Roof`: части здания и интерьер в одном контейнере.** Почему: иерархия вместо сверки, один ребёнок на этаж.
9. **Меши частей — в осях здания, сдвиг контейнера компенсирован.** Почему: файлы и отпечатки не меняются.
10. **Куски в этажах здания — куски внешнего объекта, здание всегда во внешнем объекте.** Почему: так сейчас пробиваются перекрытия над лестницей. Комнате и лестнице нужен объект.
11. **Здание и уровень: флаги и сверенные числа — отдельной `[Serializable]` частью.** Почему: новые поля, старые значения не обнуляются.
12. **Новый `LiveBuilderArray`; `ArrayModifier` остаётся для остального.** Почему: копии там — скрытые объекты и появление в игре. Нам нужен расчёт без генерации в игре. Сломали бы пользователей окна Feather Modifiers.
13. **Правило поддерева: повтор копирует всё под собой, через постройки.** Почему: «интерьер этажа × этажи» одним повтором, и ловушка «проёмы во вложенном объекте не повторяются» уходит.
14. **Копии префабов и прочих объектов — настоящие, сохранены, спрятаны в иерархии.** Почему: свету, коллайдерам, вырезам и игре нужны настоящие объекты, а генерации в игре нет (ТЗ).
15. **Skip и узор действуют на копию номер 1, оригинал выключается повтором.** Почему: пример ТЗ (Skip Y = 1). Спрятать надёжно и в редакторе, и в игре можно только так.
16. **Fill и узор — только по X.** Почему: фасадный случай закрыт, полей меньше.
17. **Skip — строкой по каждой оси: `1, 5`, `2-4`, `last`.** Почему: так в ТЗ; `last` — лестница без верхнего этажа.
18. **Step Y по умолчанию — высота этажа здания или уровня; Count Y «до верха здания».** Почему: заменяет «И этажи выше» и следит за числом этажей.
19. **Коробки в копиях — только с поворотом на 90°.** Почему: солвер по осям, переписывать его нельзя.
20. **Ядро повтора — в этапе 2, Fill, Wall, узор и настоящие копии — в этапе 4.** Почему: этап 2 не закончить без замены повторов.
21. **Миграция сама при открытии, без Undo, версии плюс признаки; префабы сцены — первыми.** Почему: ТЗ; повторное открытие ничего не меняет; Undo большой миграции дороже, чем «закрыть без сохранения».
22. **Старые классы остаются скрытыми.** Почему: без класса Unity не прочтёт старые данные. Удалить — отдельной уборкой.
23. **Косой повтор и пропуски в сетке разворачиваются в обычные объекты.** Почему: редкость, ради неё не нужны лишние поля.
24. **Сверка «стало как было» — точно по телу коробок и по спискам вырезов.** Почему: отпечаток меша ловит лишнее — грани изнутри, разбиение.
25. **Данные модуля — необязательный `LiveBuilderModule` на префабе.** Почему: «высота из префаба» явно, без правил к опоре модели из Blender.
26. **Палитра → Live Builder — через `LevelPaletteActiveAsset` в `FeatherKit.Editor`.** Почему: сборка палитры требует ProBuilder, Live Builder на неё ссылаться не может.
27. **Нумерация этажей — функция уровня, имена `Floor -1`, `Floor 1`…** Почему: одна нумерация (ТЗ), подвал без нулевого.

---

## 11. Черновик «Понятий» для LiveBuilder.md (один экран)

- **Объект** — объект из коробок: коробки, комнаты, лестницы, вырезы, стены и здания в нём. Один меш.
- **Коробка** добавляет объём. Порядок в списке не важен.
- **Вырез** (`GeometryCutter`) — коробка, арка или круг.
  - В объекте сначала складываются все коробки, потом вычитаются все вырезы.
  - «Where it cuts»: «Own object» — то, в чём лежит; «Everything it touches» — всё, чего касается.
  - «Cuts»: всё, только перекрытия, только стены.
  - Свой кусок вырез не режет: лестница — свои ступени.
- **Вложенный объект** — граница: вырезы снаружи его не режут. Колонна в вырезанном зале — во вложенном объекте.
- **Комната** — пустота плюс оболочка: стены, пол, потолок по отдельности. В здании стороны на его стенах не строятся.
- **Здание** — по контуру. Стоит на этаже уровня, высота этажа — уровня или своя.
- **Этажи** — `Floor N` и `Roof` в здании. Что положил в этаж, едет с ним. Номера — этажи уровня.
- **Повтор** (`LiveBuilderArray`) — на чём угодно.
  - По осям или вдоль стены, ровно N или «сколько влезет», узор `ABA`, пропуски `1, last`.
  - Копии — часть результата. Копии префабов — настоящие экземпляры, спрятанные в иерархии.
- **Модуль** — префаб из палитры на стене. Его вырез режет стену.

---

## 12. Что ломается, риски, что не трогаем

**Ломается и рискованно:**
- **Миграция — самый большой риск.**
  - Сверка тела точная только для коробок.
  - Мерцание, если перенесённый кусок вровень с гранью родителя.
  - Ниша вместо проёма у тонкой вырезающей коробки.
  - Проём, потерявший «место на линии».
  - Всё это попадает в итог. Проверка — сцены-образцы шага 2.0 и копии сцен автора.
- **Миграция трогает файлы:** префабы-ассеты сразу, сцену — помечает изменённой.
  - Агентам — сцены автора не открывать, работать на копиях.
  - Перед `run_tests` (MCP) — проверить, что ничего не изменено.
- **Перегородка внутри пустоты комнаты режется** — так работает правило. Инструмент «Стена» кладёт её во вложенный объект сам, старые — миграция. Нарисованная другим способом (штатным сплайном, копией) — режется, и «Проверка уровня» пишет почему.
- **Настоящие копии сохранены в сцене.**
  - Сцена больше. Предварительная оценка: экземпляр префаба — 30–40 строк, 144 окна — около 6 тыс.
  - Спрятанные объекты могут удивить. Клик по копии ведёт к оригиналу.
  - В экземпляре префаба с повтором внутри контейнер копий — добавленный объект, это правка префаба.
- **Выключенный повтором вариант** (B не на первом месте) правится через карточку («Select variant»), его ручки в сцене не видны. Удобную правку на копии делает панель этапа 3.
- **Ряд проёмов по осям** (стена прямая, сквозь) переходит с вычитания на точный проём по линии. Форма та же, треугольники другие — постройки один раз пересоберутся.
- **Сверка уровня двигает здания**, стоящие на этаже уровня, когда меняют этажи уровня. «Свободные» не трогает.
- **Имя `Floor N` меняется**, если здание переехало на другой этаж уровня.
- **Подписи меняются** (копии вместо `CopiesUp`, правило по версии) — старые сцены один раз пересоберутся.
- **Части здания** стоят в этажах со сдвигом назад — их опора не там, где этаж. Руками их не правят.

**Не трогаем:**
- `MeshBoolean`, `MeshSubtraction`, ядро солвера и его кеш этапа 1 — меняется только фильтр «кто режет»;
- поверхности, фаска, скосы, запись мешей, уборка, развёртка, общие меши экземпляров;
- правка контура, привязки к точкам и направляющие, набор чисел, линейка, прыжки, «В ProBuilder»;
- `ArrayModifier` и окно Feather Modifiers — только вынос создания копии, без смены поведения.

**Соседние этапы:**
- **Этап 3 (панель).** Поля повтора и выреза рисуют общие помощники `LiveBuilderArrayFields` и поля `GeometryCutterEditor`. Карточка выделенного берёт их, как сейчас `DrawCachedInspector`. Ей же — предупреждения `LiveBuilderArrayCopyPoses.WarningsOf` и «Select variant». Module — седьмой инструмент из списка этапа 3.
- **Этап 5 (UV).** Материалы сторон выреза `GeometryCutterSideMaterials` — то место, где этап 5 добавит сдвиг, масштаб и поворот на стороне. Одна структура с коробкой, договориться о ней.
- **Этап 6 (формы).** Колонна отдельным мешем — постройка со своим мешем. Копии ей даёт `LiveBuilderArrayCopyPoses.CopiesOf`, как стене (меш × копии). Пандус — режим коробки, повторяется как коробка.

---

## 13. Суть простыми словами

- Сейчас вырезающая коробка зависит от порядка в списке, проём — нет, у комнаты пять разных превращений, повторов пять видов, а здание связано с интерьером сверкой высот. Будет по одному правилу на вещь.
- **Вырез:** сначала всё складывается, потом всё вырезается. Хочешь колонну в вырезанном зале — клади её во вложенный объект. Вырез теперь один, любой формы.
- **Комната:** всегда пустота и стены вокруг. В здании стены комнаты вдоль стен здания не строятся.
- **Этажи:** одни, уровня. У здания в иерархии `Floor 1…9` и `Roof`. Положил шахту в `Floor 1` — она едет с этажом.
- **Повтор:** один компонент на всё. «На всех этажах» — это повтор по Y «до верха». Копии считаются сами и в иерархии не мешают.
- **Этап 4:** окно-префаб кликом на стену, с Shift — целый ряд. Узор `ABA` и «без первого этажа» дают фасад из окон и балконов за минуту.
- **Старые сцены** при открытии обновятся сами. В консоли — что переложено и что может выглядеть иначе. Проверяем на копиях сцен автора и на эталоне.
