# Заметки по ходу этапа 3

Временный файл на время доработки: что выяснилось на шагах этапа 3 и что важно следующим шагам. Читать вместе с [Stage3and5and6.md](Stage3and5and6.md).

## После 3.1

**Как устроено**
- Ярлыки — `Editor/FeatherKit/LiveBuilder/Shortcuts/`. Метод ярлыка — однострочник: `LiveBuilderShortcutContext.KeyboardOf(arguments).…`;
  что делает клавиша — `LiveBuilderSceneKeyboard` (часть `LiveBuilderSceneTool`, `sceneTool.Keyboard`).
- Контексты (`LiveBuilderShortcutContexts`) ставятся в `OnActivated` и ещё раз (без повтора) в `OnToolGUI`: перезагрузка скриптов их стирает.
- Ярлык выбора инструмента — в файле инструмента: `[Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "<Id>", typeof(LiveBuilderIdleShortcutContext), KeyCode.AlphaN)]`
  → `LiveBuilderToolPalette.Select<T>()`. Имя ярлыка — `ToolsPrefix + Id` инструмента.
- Сработавший ярлык Unity гасит событие до `OnToolGUI`, поэтому каждое действие клавиатуры само зовёт `SceneView.RepaintAll()`.
- Подпись клавиши уже из привязки: `LiveBuilderToolPalette.HotkeyOf` и подсказка значка инструмента (`LiveBuilderShortcutIds.KeyLabelOf`).
  Шпаргалка (`LiveBuilderSceneKeys.CommonKeys`) и список клавиш в окне — ещё строками (шаг 3.6).

**Отступления от плана**
- **Esc и Enter ярлыком не сделать.** Unity 6000.6.4 отбрасывает такой `[Shortcut]` с предупреждением «Binding uses invalid key code Escape / Return».
  `Live Builder/Confirm` и `Live Builder/Cancel` из таблицы 3.4 нет: Enter, Enter цифрового блока и Esc ловит окно сцены → `LiveBuilderSceneKeyboard.HandleUnboundKey`
  → `Confirm()` / `Cancel()`. В окне Shortcuts их нет, переназначить нельзя. Шаг 3.6: подписи Enter и Esc — постоянные строки.
- **Обычный свой контекст не сильнее окна сцены** — проверено в фоне обработчиком ярлыков самой Unity с окном сцены в фокусе: «2» находит
  и `Live Builder/Tools/Box`, и `Scene View/Toggle 2D Mode`, и Unity не выполняет ни один, а спрашивает окном; так же S (`Overlays/Show Tool Settings`)
  и стрелки (`3D Viewport/Fly Mode … (Alt)`). Контекст «тип инструмента» (`typeof(EditorTool)`) Unity не принимает вовсе: «context type must …
  derive from EditorWindow, or implement IShortcutContext». У ProBuilder и Splines тоже обычные `IShortcutContext` — они спорят только с глобальными.
  Выигрывает без спора только **приоритетный** контекст (внутренний `PriorityContextAttribute`, у Unity он у полёта камеры). Поэтому наши
  контексты ставятся приоритетными через отражение — `LiveBuilderPriorityShortcutContexts` (`ContextManager.RegisterPriorityContext`, ищется
  один раз). Не нашлось — обычная регистрация, и только тогда предупреждение в консоли и меню `Resolve Shortcut Conflicts` имеют смысл.
- **Пока камера летит** (`Tools.viewTool == ViewTool.FPS`, правая кнопка + WASD), контексты Live Builder выключены: полёт тоже приоритетный,
  и S «назад» иначе заспорила бы с S «прилипание».
- **Окно сцены в фокусе** — по `SceneView.hasFocus`, а не по `EditorWindow.focusedWindow`: в фоновом режиме второе пусто, а проверки клавиш идут там.
- **Пятый контекст `ContourPoints`**: стрелки работают, только когда точки контура выбраны. Иначе при любой выделенной стене стрелки отнимались
  бы у камеры Unity.

**Для 3.2–3.6**
- 3.2: Shift + цифра — второй `[Shortcut]` в контексте Idle с `ShortcutModifiers.Shift` (в окне сцены Unity Shift + цифра не занята);
  `Select<T>()` → `SelectToolOrNextMode<T>()`. При Room → Box, Opening → Cut, Module — поправить ярлыки в файлах инструментов: тесты
  `AllLiveBuilderShortcuts_AreRegistered` и `DefaultBindings_AreAsSpecified` берут Id из палитры и ждут ровно 8 инструментов с клавишей «место + 1».
- Тесты зовут метод ярлыка отражением по имени из `[Shortcut]` с контекстом в аргументах (`LiveBuilderShortcutTests.InvokeShortcut`) — пригодится для Shift + цифра.
- Кто выигрывает клавишу у Unity — `LiveBuilderShortcutPriorityTests` через обработчик ярлыков самой Unity (`UnityShortcutKeyboard`, отражение).
  Новая клавиша по умолчанию, совпавшая с ярлыком окна сцены, — добавить туда строку «включён — наш, выключен — Unity».
- **Профили клавиш общие на все проекты человека** (`~/Library/Preferences/Unity/Editor-5.x/shortcuts`), активный профиль — тоже. Тест 3.6
  `KeyHints_ShowCurrentBinding` с «временным профилем» тронет профили автора: только с `try/finally`, вернуть активный и удалить временный —
  или проверять подпись без смены профиля.

**Временные решения и долги**
- Ярлыки Live Builder используют внутренний приоритетный контекст Unity через отражение; при обновлении Unity проверить тест
  `LiveBuilderShortcutPriorityTests.PriorityContextMethod_IsFound` (и весь `LiveBuilderShortcutPriorityTests`).

## После 3.5

**Как устроено**
- `LiveBuilderWindow` — только прокрутка: `LiveBuilderLevelPage` (Level Check, Storeys, Wall Types, Materials, Mesh Files).
  Шапки вкл/выкл, списка клавиш, палитры, подсказки внизу, вкладок и «Вкладок сами» больше нет. Меню и `LiveBuilderWindow.Open()` — те же.
- Разделы-свёртки — `LiveBuilderWindowControls.FoldoutSection(key, title, summary, zone, isOpenByDefault)` → `using` со `IsOpen`; раскрытость —
  `EditorPrefs` «FeatherKit.LiveBuilder.Section.<key>»; ими же рисует секции карточка панели.
- Цвета зон — `LiveBuilderAccentColors.ColorOf/TintOf(LiveBuilderAccentColor.Tool / Selection / Level)`; `LiveBuilderTabBar`, `LiveBuilderTabColor` удалены.
- Materials — `LiveBuilderMaterialSettingsDrawer` (в плане 5 — `LiveBuilderMaterialTilingDrawer`): материал новых построек, кнопка Level Palette
  (пункт меню `FeatherKit/Level Palette`, только с ProBuilder — сборка палитры от него зависит) и таблица плиток. Правка пишется в ProjectSettings
  через `SerializedObject`, как страница Project Settings, → `LiveBuilderProjectSettings.Changed` → пересборка построек с этим материалом.
- «?» убраны: `LiveBuilderWindowControls.Explanation` ничего не рисует; вызовы в `Tools/*.cs` — убрать в 3.6.

## После 3.2

**Как устроено**
- Палитра — ровно 8: Select 1, Box 2, Cut 3, Wall 4, Building 5, Stairs 6, Module 7, Ruler 8. `LiveBuilderRoomTool` удалён, `LiveBuilderOpeningTool` → `LiveBuilderCutTool` (Id `Cut`).
- Режимы — `Tools/LiveBuilderToolModes` (подписи — `LiveBuilderToolModeLabel`: имя, значок или null, подсказка). Текущий режим живёт в настройке самого инструмента:
  Select — `BoxModelSelectionModeSetting` (Objects / Faces), Box — `LiveBuilderBoxModeSettings.Mode` (Solid / Room / Ramp / Column), Cut — `LiveBuilderCutterSettings.Shape`
  (Rect / Arch / Circle), Stairs — `LiveBuilderStairsSettings.Shape` (Straight / Switchback, без значков). У остальных `Modes == null`.
- Клавиши: `[Shortcut]` выбора → `LiveBuilderToolPalette.SelectToolOrNextMode<T>()`, второй — `ToolsPrefix + Id + PreviousModeSuffix` («… (Previous Mode)»), Shift + цифра →
  `SelectToolOrPreviousMode<T>()`. Shift у другого инструмента — только выбрать. `LiveBuilderShortcutIds.AllIds` — по два на инструмент.
- «Коробка» — диспетчер (`Tools/LiveBuilderBoxTool.cs`), части-режимы — `Tools/Box/`: `LiveBuilderBoxToolMode` (абстрактная), `LiveBuilderSolidBoxMode`
  (+ наследники `LiveBuilderRampBoxMode`, `LiveBuilderColumnBoxMode` в том же файле — тот же жест другой формы), `LiveBuilderRoomBoxMode` (тело бывшей «Комнаты»).
  «Новый» (`LiveBuilderBoxTool.IsNewModelRequested`) — общий на режимы. `CanEdit` «Коробки» — коробки, вырез-коробки, комнаты, колонны в любом режиме.
  `LiveBuilderBoxShapeSettings` → `LiveBuilderBoxModeSettings` (режим под ключом `Box.Mode`; пока его нет — читается старый `Box.Shape`).
- Миграция выбранного: `LiveBuilderToolPalette.MigrateStoredId` — `Room` → `Box` + режим Room, `Opening` → `Cut`; старые номера — туда же.
- Cut: Shift + клик по стене по линии или зданию — `LiveBuilderCutRow.Repeat`: `LiveBuilderArray` на самом вырезе (Along Wall, Fill, Length 0, шаг/отступ —
  `LiveBuilderCutterSettings.RowStep/RowMargin`, под «Ещё»), сразу на первое место стороны (`LiveBuilderArrayWallPath.MoveToFirstPlace`, вынесен из модуля).
  С To top повтор по этажам остаётся на том же компоненте. Клик теперь берёт луч нажатия (`OnMouseDown` не забирает) — проверяется драйвером.
- «Into: …» — `LiveBuilderTool.IntoTarget` (null — ничего не ставит: Select, Ruler) из `Tools/LiveBuilderIntoTarget`: `OfStorey()` (здание под мышью › Storey N,
  иначе `Level › Storey N`), `OfBox(isNew)` (объект под мышью / этаж здания / выделенный / `New object`), `OfWall(стена под мышью)` у Cut; `PathOf(Transform)` —
  постройки Live Builder и этажи над объектом. Что под мышью пишет `LiveBuilderSceneTool.OnMouseMove` (`RememberHover`).
- To top — одна кнопка (`LiveBuilderOptionIcon.AllStoreysAbove`) вместо «Только этот / И этажи выше»; хранится там же — `LiveBuilderBuildingContext.IsOnAllStoreys`.
- Строка инструмента — `LiveBuilderToolSettingsFields.DrawToolLine(tool)` (теперь её рисует панель, см. «После 3.3»).
- Новые значки: `LiveBuilderOptionIcon.Room`, `Ramp`, `Column`.

**Отступления от плана**
- Ряд проёмов по Shift — только в стене по линии и здании (у них есть линия для Along Wall). В стене объекта из коробок Shift + клик ставит один проём.
- В здании проём кликом встаёт на этаж, выбранный в окне (`LiveBuilderStoreys.CurrentStorey`), а не на тот, куда навёл, — как было; тест ставит на первый этаж.
- Лестница ставится только в здание внутри объекта из коробок (нужен объект) — как было; `ToTop_SetsCountYToBuildingTop` строит здание в объекте.

**Клики (таблица 3.9), в нынешнем окне**
- Дешевле: ряд проёмов вдоль стены (3 → Shift + 1 клик), сменить форму проёма (вкладка + 1 → клавиша 3 ещё раз или 1 клик по Mode).
- Так же: проём кликом (7 + 1 → 3 + 1), проёмы на всех этажах, шахта, балкон и козырёк (To top — 1 клик, помнится), здание, лестница, линейка, модуль (9 → 7),
  Room → Solid (2 → Shift+2). Box → Room — «2» ещё раз (1 нажатие, как бывшая «3»); из другого инструмента — 1 нажатие, если Room был последним, иначе 2.

**Проверки**: `LiveBuilderShortcutTests` (+ `ToolLineup_AtMostEight_InSpecifiedOrder`, `ToolKeyAgain_NextMode_ShiftKey_PreviousMode`, `StoredRoomTool_BecomesBoxInRoomMode`,
Shift-привязки в `DefaultBindings_AreAsSpecified`), `LiveBuilderCutToolTests` (клик, Shift + клик, To top у Cut, Box, Wall, Stairs),
`RoomMode_ClickWithoutDrag_HintsWallOrBuilding`; тесты `RoomTool_*` / `Opening*` перенесены на «Коробку» в режиме Room и Cut.

## После 3.3

**Как устроено**
- Панель — `Panel/LiveBuilderPanelOverlay` (`Overlay`, id `featherkit-live-builder-panel`, правая колонка, Panel, ширина 300, мин. 220) → `IMGUIContainer`
  → `LiveBuilderPanelView`: ряд 8 значков (клавиша — `HotkeyOf`) + ⚙ (окно уровня) и ⏻ справа; строка инструмента (`LiveBuilderToolSettingsFields.DrawToolLine`:
  имя и режимы, `DrawSettings`, Into + To top + «⋯» → `LiveBuilderMoreSettingsPopup`, точка на «⋯» — `AreMoreSettingsChanged`); карточка в прокрутке
  (высота окна сцены − 230); нижняя строка `LiveBuilderCommonSettingsBar.Draw(isNarrow)`. ≤ 260 px — инструменты 2 × 4, `wideMode` выключен, пол этажа и каркасы спрятаны, вид — список.
- Ширина панели для IMGUI — `LiveBuilderWindowControls.InPanel(width)` / `AvailableWidth` (`currentViewWidth` в оверлее — ширина всего окна сцены);
  `Card(...)` в панели рисуется без рамки и заголовка — настройки инструментов не переписывались.
- Перерисовка: события выделения, палитры, этажа, Undo, вкл/выкл, `LiveBuilderIntoTarget.HoverChanged` (место под мышью сменилось); на перерисовке сцены —
  только если сменился отпечаток (сетка, прилипание, вид, этаж, `IsBusy`, `SceneVersion`, каркасы): иначе панель и сцена перерисовывали бы друг друга.
- Карточка: `LiveBuilderSelectionKinds` (вид объекта, счёт видов, «2 Box · 1 Cut»), `LiveBuilderSelectionCardSet` (секции по порядку; разные виды — только
  `IsSharedByAllKinds`), `LiveBuilderCardHeader` (вид · имя · размер, путь `LiveBuilderIntoTarget.PathLinksOf` ссылками с сокращением «… ›», плашки видов,
  предупреждения: `WarningsOf` повтора, контур, поворот здания). Секции — наследники `LiveBuilderSelectionCard` (`Order`, `Title`, `ShowsFor`, `IsEmptyFor` → «+ Add»,
  `SummaryOf`, `Draw(IReadOnlyList<GameObject>)`, `Add`): Construction 10 (+ `LiveBuilderBuildingPartCard`), Level Storeys 15 (только старое здание не на этажах),
  Points 20, Inside 30, Cut 40, Material 50, Chamfer 60, Array 70, Move & Copies 80, Group 90.
- Construction: коробка — Size с «—» (`LiveBuilderMixedValue`), Make Cut, угол под «More»; этаж — номер, пол, Select Building; остальное — инспектор на всех
  выделенных (`ILiveBuilderCompactInspector.DrawMainFields`: вырез, здание, стена, лестница; комната, колонна, модуль, объект — весь инспектор).
  `GeometryCutterEditor.DrawCutFields` — секция Cut. Предупреждения здания — только в полном инспекторе, в панели — в заголовке.
- Material: стороны — `LiveBuilderMaterialSlots.PlacesOf` (`LiveBuilderMaterialPlace`), числа подгонки — `LiveBuilderTextureFitFields` (Offset, Scale, Rotation с «—»),
  World/Object — поле `textureAlignment` постройки (у коробки — её объекта), Fit — `LiveBuilderTextureMapping.FitToFace` (одна плитка на сторону, с мировым
  сдвигом и поворотом), Reset — `Fit.Set(null)`, материал остаётся.
- Array (`LiveBuilderArrayCard`, вместо `LiveBuilderRepeatCard`): Along, X Count/Fill (Length, Margin, Min step), «Y — up: To top», Step = storey, Z, Skip,
  узор под «More», итог копий, «Also On Top Storey» у лестницы (`LiveBuilderArrayEditor.DrawTopStoreyToggle`), Remove; «+ Add» — меню To top / Along X / Along Z /
  Along wall (`LiveBuilderArrayActions`: `RepeatUp`, `AddAlongAxis`, `AddAlongWall`), у нескольких — «+ Add to each».
- `BoxModelChamferPanel.Draw` — без своей карточки; Cut-инструмент: готовый размер — список «Click size», а не сетка кнопок.

**Удалено**: `LiveBuilderSceneOverlay`, `Window/LiveBuilderTemporarySections`, `Window/LiveBuilderSelectionStrip`, `Cards/LiveBuilderOwnComponentCard`,
`Cards/LiveBuilderBoxPartCard` (→ Construction, Cut, Material, Chamfer), `Cards/LiveBuilderRepeatCard` (→ `LiveBuilderArrayCard`). Окно — только `LiveBuilderLevelPage`.

**Отступления от плана и макета**
- Заголовок оверлея рисует Unity — ⚙ и ⏻ стоят справа от ряда инструментов, а не в заголовке.
- Move & Copies нет у выреза (его двигают свои ручки, `LiveBuilderMoveHandle.MovableOf` его не берёт) — и у смешанного выделения с вырезом.
- Fit — только у сторон коробки; у выреза, комнаты, стены, здания кнопка выключена (нужна геометрия их граней).
- Скосы нескольких коробок — только «N of M», правка — у одной. Значок вида в заголовке (P2) не сделан.
- Подписи `DrawSettings` / `DrawMoreSettings` «Стены», «Здания», «Комнаты», «Линейки» — ещё русские (3.6); `DrawSettings` не переименован в `DrawOptions`.
- Drawing (F) и полосы (H) — шаг 3.4.

**Проверки**: `LiveBuilderPanelCardTests` (порядок секций коробки, выреза, здания, чужого объекта; «+ Add»; путь `Section › Building › Storey 2 › Window`;
2 Box + 1 Cut — плашки и общие секции; Size X «—»; Make Cut / Make Box по Ctrl+Z; Into над вторым этажом; `FitToFace_OneTilePerSide`; `Reset_ClearsFitOnly`;
Array Along X), `LiveBuilderSectionFromScratchTests.ReferenceSection_FromScratch_WithToolsOnly` — «Готово, когда» драйвером: контур 24 × 12, 9 этажей полем
как в Construction, ряд окон To top + Shift + клик, лестница, шахта New + To top, две квартиры Room, дверь Cut; окно Live Builder не открывается.

**Проверить автору руками**
- Панель появилась справа в колонке; старой полосы сверху нет; панель тянется, сворачивается, прячется (`), в узкой (≤ 260) — 2 ряда инструментов.
- Нет постоянной перерисовки (загрузка процессора в покое); Into меняется при движении мыши по зданию; `[` `]`, PgUp/PgDn, S — сразу в нижней строке.
- «⋯» у Box, Cut, Wall, Building, Stairs — окошко, точка, когда изменено; длинная карточка прокручивается, ряд и нижняя строка на месте.
- Клик по звену пути, плашки видов, правка нескольких зданий и коробок с «—»; Material: сторона, Fit, Reset, World/Object; Array «+ Add» и поля.
- Тёмная и светлая тема; таблица кликов 3.9 мышью, окно не открывая.


## После 3.4

**Как устроено**
- Отчёт рисования — `LiveBuilderDrawingReport` (класс, не структура: список размеров): `Title`, `StepNumber`/`StepCount` (0 — шагов сколько угодно, у линии точек),
  `StepName`, `Sizes` (`LiveBuilderDrawnSize`: подпись, метры, `IsTyped` — замок), `TypedText`, `EnterResult`/`EscResult` (null — клавиша ничего не делает), `Stamp()`.
  Заполняет жест методом `Report()`: `BoxModelDrawGesture` (Box/Ramp/Column), `LiveBuilderSurfaceBoxGesture`, `LiveBuilderRoomGesture`, `LiveBuilderStairsGesture`,
  `LiveBuilderCutterGesture`, `LiveBuilderPathGesture` (стена, здание: Segment, Total), `LiveBuilderRuler`. Инструмент отдаёт его свойством `LiveBuilderTool.DrawingReport`
  (по умолчанию — линия точек; «Коробка» — через режим `LiveBuilderBoxToolMode.DrawingReport`).
- Набранные стороны: `BoxModelDrawGesture` помнит их флагом `areSidesTyped` (после отпускания набор стирается), `LiveBuilderFloorRectangle.AreSidesTyped` — по набору.
- Панель: `LiveBuilderPanelView.DrawCardOrDrawing` — пока `IsBusy` и отчёт не null, вместо карточки `Panel/LiveBuilderDrawingSection`; отчёт берётся на `Layout`
  и держится до следующего (разметка и отрисовка IMGUI видят одни строки). Отпечаток оверлея включает `DrawingReport.Stamp()` — числа живые на каждом движении мыши.
- Полосы: `LiveBuilderPanelOverlay : ICreateHorizontalToolbar, ICreateVerticalToolbar` → `Panel/LiveBuilderPanelToolbarElements` (UI Toolkit): ⏻, 8 `EditorToolbarToggle`
  (клик по выключенному Live Builder — включает), этаж ◀ N ▶ / ▲ N ▼, в горизонтальной — сетка `EditorToolbarDropdown` со списком. Обновляется тем же `RepaintPanel`.
  Unity сама видит раскладки Panel + Horizontal + Vertical (проверка `PanelOverlay_SupportsBothToolbarLayouts` по внутреннему `supportedLayouts`).
- Правая часть подсказки (этаж, сетка, прилипание) уже сделана в 3.6 (`LiveBuilderSceneText.DrawHint`); она спрашивает `LiveBuilderPanelOverlay.IsFullPanelShown` —
  он учитывает и плавающую полосу, а не только `isInToolbar`. `OverlayId` — публичный.

**Отступления от плана**
- Строки отчётов — английские сразу; Enter у косой поверхности и у протягивания прямоугольника — null (готово кликом или отпусканием), в секции не пишется.
- Значок свёрнутой панели (`collapsedIcon`) не задан — Unity рисует свой.

**Проверки**: `LiveBuilderDrawingPanelTests` — `DrawingReport_BoxDepthStep_ShowsTypedAndEnterResult` («4X3X1,5», Enter — коробка 4 × 1,5 × 3),
`DrawingReport_BoxRectangleStep_TypedSidesStayLocked` (Hold + «4X3», отпустил — замки остались), `DrawingReport_StairsHeightStep_ShowsTypedHeight`,
`DrawingReport_WallLine_ShowsTypedSegmentLength`, `ToolbarElements_HaveEightTools_StoreyAndGrid`, `PanelOverlay_SupportsBothToolbarLayouts`.

**Проверить автору руками** (в фоне не проверить — окна сцены нет)
- Протянуть коробку, набрать «4 X 3 X 1,5»: в панели Drawing — шаг, размеры с замками, «Enter — box …»; отпустил Enter — карточка вернулась.
- Панель перетащить в полосу сверху и к краю: ряд значков, этаж, сетка (сверху); клик по значку — инструмент; свёрнутая — всплывает по клику.
  Справа внизу подсказки — этаж и сетка, пока полная панель не видна. Тёмная и светлая тема.

## После 3.6

**Как устроено**
- Подсказка внизу сцены — одна строка `tool.Hint` (+ «click again — deeper» у Select и Box), клавиши в ней плашками: `LiveBuilderHintKeys` метит клавишу внутри
  обычной строки (`Key("…")`, `Enter`, `Esc`, `Shift`, `Ctrl`, `Alt`, `Undo` — ⌘Z на Mac, `Bound(shortcutId)` — подпись из привязки, нет привязки — плашки нет),
  `PartsOf` режет строку на текст и плашки, `PlainTextOf` — без меток. Рисует `LiveBuilderSceneText.DrawHint` / `DrawLine` (одна строка, не влезло — обрезается).
  Справа — `Storey N · Grid 0.5 m · Snap on`, пока `LiveBuilderPanelOverlay.IsFullPanelShown` ложно.
- Этажа и здания в начале подсказки больше нет — где строишь, говорит строка Into. `BuildingStoreyHint`, `LiveBuilderBuildingContext.DescriptionOf`,
  `LiveBuilderSceneTool.HintChanged` / `CurrentHintText` удалены (никто не слушал).
- Шпаргалка (`LiveBuilderSceneKeys`, 250 px) — `tool.KeyHints` (2–3 строки) + выделенное: точки контура (←↑→↓, Delete, рамка, C у стены) или коробка
  (Alt, Ctrl, Shift). Общих строк нет; в заголовке «Box — keys ⟨/⟩ hide», спрятана — плашка «⟨/⟩ keys». `KeyHints` — свойство, каждый раз новый массив:
  подписи из нынешних привязок.
- Подписи клавиш везде из Shortcut Manager, кроме Enter, Esc, Delete, Backspace и Shift+H (не ярлыки Live Builder) — постоянные строки.
  Подсказки нижней строки (этаж, сетка, прилипание) и режимов инструмента тоже берут клавишу из привязки.
- `LiveBuilderSceneText.FormatMeters` — точкой при любой локали («1.5 m»).
- `LiveBuilderWindowControls.Explanation` и все вызовы удалены (с 3.5 ничего не рисовали); «встанет на выбранный этаж» у Building — в подсказку поля Storeys.

**Нарочно русским**: всплывающие подсказки (`GUIContent` второй аргумент, `Tooltip` инструментов и режимов, `[Tooltip]`, `[FDescription]`), комментарии,
слова поиска страницы Project Settings (к ним добавлены английские). `MoreSettingsSummary` — английские названия секций внутри русской подсказки «Ещё: …».

**Отступления**
- Готовые размеры проёмов по умолчанию — «Door 0.9 × 2.1» и т. п.; у проектов с уже сохранённым `ProjectSettings/FeatherKit/LiveBuilder.asset` имена остаются русскими.
- В `Panel/LiveBuilderPanelToolbarElements` и `Panel/LiveBuilderPanelView` в подсказках клавиши ещё строками («PageUp», «[ и ]», «1–8, S») — файлы шага 3.4, не трогал.

**Проверки**: `LiveBuilderShortcutTests.KeyHints_ShowCurrentBinding` (временный профиль: Box на Q, Rotate на R — значок «Q», шпаргалка лестницы «R»;
в `finally` активный профиль возвращается, временный удаляется), `Hint_SplitsIntoTextAndKeys`; `LiveBuilderLineGuideTests` — английские подписи направляющих.

**Проверить автору руками**: подсказка внизу — одна строка с плашками у каждого инструмента и шага (Box: прямоугольник → глубина → набор; Wall; Stairs; Cut);
справа этаж и сетка при свёрнутой или спрятанной панели; шпаргалка 2–5 строк, «/» прячет; плашки читаются в тёмной и светлой теме.
