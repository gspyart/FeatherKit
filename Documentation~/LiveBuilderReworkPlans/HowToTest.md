# Как проверять во время доработки (для программистов)

Временный файл на время доработки, удалить в конце.

## Unity автора не трогать

- В редакторе автора открыта игровая сцена; `run_tests` через unityMCP сохраняет изменённые сцены на диск,
  а перезагрузка скриптов FeatherKit делает её изменённой. Поэтому **инструменты unityMCP не использовать вообще**.
- Все тесты и замеры — в отдельном проекте-стенде в фоновом режиме Unity (batch mode), он подключает FeatherKit по пути
  и компилирует его с диска при каждом запуске.

## Стенд

Скрипт: `/private/tmp/claude-501/-Users-gspy--claude-spares-spare-vT02Zv/268ea423-b78e-45c5-b04d-b4096c5bfc21/scratchpad/tools/run-testbed.sh`

- Весь набор Live Builder: `run-testbed.sh <имя-прогона>` — около минуты (запуск Unity + ~30 с тестов). Печатает итог и упавшие.
- Часть тестов: `run-testbed.sh <имя> -testFilter "Regex"` (по имени теста или класса).
- `[Explicit]` в фоновом режиме Unity идут и в полном прогоне. Поэтому запись эталонов (`WriteBaseline`, `WriteLookBaseline`) ещё и ждёт флага:
  `run-testbed.sh <имя> -testFilter "<полное имя>" -writeLiveBuilderBaselines`; без флага тест пропускается.
- Ошибки компиляции: прогон не даст файла результатов, скрипт напечатает строки `error CS` из лога.
- Замер скорости: `run-testbed.sh <имя> --exec TestBedCommands.MeasureRebuild` — отчёт в
  `.../scratchpad/FeatherKitTestBed/measure-report.txt`. Идёт минуты — запускать в фоне.
- Лог каждого прогона — `.../scratchpad/testbed-out/<имя>.log`.
- Стенд — один проект: два прогона одновременно нельзя (Unity запирает проект).
- Команды длиннее 2 минут — запускать в фоне (`run_in_background`), ждать уведомления, не опрашивать.

## Второй стенд — копия игры

Сцены автора с постройками Live Builder (`Live Builder Showcase`, `c1-two-towers`, префаб `Scene Blocking/Floor`) без ассетов игры не открыть,
поэтому для них — полная копия игры: `.../scratchpad/GameCopyBed`.

- Сделана только чтением из проекта игры: `Assets`, `ProjectSettings`, `Packages` скопированы `rsync -a`, без `Library`.
  В `Packages/manifest.json` FeatherKit — абсолютным путём (`file:/Users/gspy/Projects/Unity Projects/FeatherKit`), `com.coplaydev.unity-mcp` убран,
  `packages-lock.json` удалён. Обновить копию — тот же `rsync -a` из игры в копию; импорт после этого долгий.
- Скрипт: `.../scratchpad/tools/run-gamecopy.sh`, логи — `.../scratchpad/gamecopy-out/<имя>.log`.
  - `run-gamecopy.sh <имя>` — открыть проект и выйти (импорт, компиляция);
  - `run-gamecopy.sh <имя> --exec <Класс.Метод> [аргументы]` — свой метод;
  - `run-gamecopy.sh <имя> --tests [-testFilter …]` — тесты Live Builder на копии.
- Сцены и префабы копии можно открывать, пересобирать и сохранять — это копия. В git FeatherKit из неё ничего не кладём.
- Стенды — разные проекты: прогон на копии и на первом стенде могут идти одновременно, но в каждом — по одному.

**Отпечатки «по виду» сцен автора** — эталон «как было» для миграции этапа 2:
```
run-gamecopy.sh look --exec FeatherKit.Tests.LiveBuilder.LiveBuilderSceneLookWriter.WriteFromCommandLine \
  -lookAssets "Assets/Project/Scenes/Live Builder Showcase.unity;Assets/Project/Scenes/Game Levels/c1-two-towers.unity;Assets/Project/Prefabs/Scene Blocking/Floor.prefab" \
  -lookOut "<scratchpad>/GameCopyBed-baselines"
```
- На каждую сцену два файла: `<имя>.saved.look.txt` — меши как сохранены, `<имя>.look.txt` — после полной пересборки нынешним кодом. Эталон — второй.
- Разделы — корни построек (постройка, над которой нет другой), строки `# root` их перечисляют. Раздел `*` — все постройки вместе:
  его и сверять, если миграция переложила корни (`LiveBuilderLookDigest.SectionOf`).
- Эталоны — в `.../scratchpad/GameCopyBed-baselines/`, не в FeatherKit. Записаны на коде этапа 1: после правок правил не перезаписывать.

## Сцены-образцы этапа 2

- `Tests/Editor/LiveBuilder/UpgradeFixtures/*.unity` — старые устройства, собранные кодом этапа 1, с мешами рядом и отпечатком `<сцена>.look.txt`.
  Тест `UpgradeFixtures_AreReadable` открывает их из пакета без пересборки и сверяет вид.
- Пишет их `run-testbed.sh <имя> --exec FeatherKit.Tests.LiveBuilder.LiveBuilderUpgradeFixtures.WriteAll` — заменяет все образцы.
  После правок правил не запускать: образцы — запись «как было».
- Эталон вида эталонного уровня — `Baselines/ReferenceLevelLook.txt`, тест `Meshes_LookSameAsBaseline`. Переписывает `WriteLookBaseline` с флагом `-writeLiveBuilderBaselines` — только по указанию менеджера.

## Известное

- Проверки инструментов зависят от шага сетки Unity (`EditorSnapSettings.gridSize`): на стенде он 1 м, у автора мельче.
  `BoxTool_OnBalconySlab_AttachesToBuildingToo` падал из-за этого (плита 0,2 м прилипала в ноль) — теперь сам ставит сетку 0,1 м.
- Эталон уровня записан до доработки: `Tests/Editor/LiveBuilder/Baselines/ReferenceLevel.txt`. Не перезаписывать без
  прямого указания менеджера: `Meshes_MatchBaseline` — проверка «меши те же».
