# Этапы 5 и 6 — заметки между шагами

Временный файл: что каждый шаг оставил следующим. Читать перед своим шагом, дописывать в конце своего.

## После 5.1–5.3, 5.5

**Как устроено**
- Данные (Runtime): `LiveBuilderTextureFit` (сдвиг м, масштаб, четверти оборота; `IsDefault`, `Copy`), `LiveBuilderTextureAlignment` (`Object` = 0, `World` = 1),
  `LiveBuilderTextureFitList<TPlace>` — обобщённый разреженный список: `Get` (нет — null), `Set` («как есть» убирает строку), `Clear`, `ClearAll`, `Entries`.
  Места — перечисления в `LiveBuilderTextureFitList.cs`: `BoxModelSideSlot` (коробка и вырез), `SplineWallTextureSurface`, `SplineBuildingTextureSurface`, `BoxModelRoomTextureSurface`.
- Поля: `BoxModel.TextureAlignment`, `SplineWall.TextureAlignment` + `TextureFits`, `SplineBuilding.TextureAlignment` + `TextureFits`, `BoxModelPart.TextureFits`,
  `GeometryCutter.TextureFits`, `BoxModelRoom.TextureFits`, `BoxModelStairs.TextureFit` (одно место — одна подгонка, не список).
  Старым данным — `Object` и пусто; фабрика и `Reset()` (у стены `Reset` добавлен) ставят `World`. Эталонный уровень явно `Object`: `LiveBuilderReferenceLevel.TextureAlignment`.
- Поля видны в обычном инспекторе (группы «Texture» / «Texture Fit», подсказки по-русски) — так ими можно пользоваться до карточки. План просил `[HideInInspector]`:
  когда карточка 5.4 их заменит, решить — прятать или оставить «тонкой» правкой.
- Плитки: `LiveBuilderProjectSettings.MaterialTileSizes` (строка — `LiveBuilderMaterialTileSize`), видна на странице Project Settings сама (страница рисует все поля);
  `SetMaterialTileSize` — в памяти, без записи на диск (для кода и проверок); событие `LiveBuilderProjectSettings.Changed` → `LiveBuilderMaterialTiling` забывает таблицу,
  забывает результаты коробок и просит проход очереди. Чтение — `LiveBuilderMaterialTiling.SizeOf`, `HasAnySize`.
- Формула — `LiveBuilderTextureMapping.Map(planar, worldShift, fit, metersPerTile)`; всё по умолчанию возвращает `planar` как есть (байт в байт).
  `PeriodOf` — через сколько метров повторяется рисунок по осям грани (с поворотом).
- Мир — `LiveBuilderTextureSpace.Of(transform, alignment)` / `OfMeshOwner`: корень — открытый, загруженный или файл префаба (`transform.root`), иначе самый внешний
  экземпляр префаба, иначе мир. `ShiftAlong(направление)` — сдвиг до мира по направлению постройки.
- Коробки: подгонки едут внутри `BoxModelSideMaterials` (`Fits`, `WithFits`) — см. отступления. `BoxModelSolverBox.FitForSurface`; `BoxModelMeshBuilder.Fill(…, textureSpace)`.
  Вырез-коробка — `GeometryCutterSolverBoxes.SideMaterialsIn` (подгонки по сторонам объекта, `OwnFitOf`), комната — `BoxModelRoomShell`, лестница — `BoxModelStairsLayout`.
  Make Cut / Make Box переносят подгонки (`GeometryCutterBoxConversion.CopyFits`).
- Стены и здания: `SplineWallGeometry.SetTextureAxes` — у каждой грани её оси до мира (`SplineWallTextureAxes`: сторона, план, поперёк, край дыры) и вид (`SplineWallFaceKind`);
  развёртка — при записи `WriteTo(mesh, SplineWallTexturing)`: память нерезаных частей здания от места здания не зависит. Места: стена — часть 0 сторона → Sides, иначе Ends;
  здание — `SplineWallTexturing.BuildingSurfaceOf`.
- Новые грани после вычитания — `LiveBuilderFaceUnwrapper` (свойство `MeshSubtractionPolygons.Unwrapper`; нет — прежний `UnwrapFlat`): у выреза — подгонка его стороны,
  у фаски и скосов — плитка материала части меша, подгонка «как есть».
- Подписи: подгонки, `World`, плитки — только не по умолчанию (старые постройки не пересобираются, версии не поднимались). Мировой ключ (`|w…`) стоит перед ключом
  вырезов (`|x…`): `LiveBuilderTextureSignature.WorldKeyFor` — чистовая считает нынешний, черновая берёт из собранной подписи (`WorldKeyIn`). Объект —
  `BoxModelSignature.WorldKeyOf`, стена — `SplineWallSignature.WorldKeyOf`, здание — `SplineBuildingPartKeys.WorldKeyOf` (в подписи каждой части).
- Пипетка — `LiveBuilderTexturePipette`: `TryPick(ray)`, `TryApply(ray)`, `HandleEvent(Event)` — крючок в `LiveBuilderSceneTool.OnToolGUI` после клавиш, по `rawType`,
  клик без протягивания < 4 px; Alt + ContextClick гасится. Место — `LiveBuilderMaterialSlots.TryFindUnder`; у `LiveBuilderMaterialSlot` теперь `ReadPaint`,
  `TryAssign(material, fit)` (материал нельзя — подгонка ложится, ответ false) и `Fit` (`LiveBuilderTextureFitAccess`: Get — своя или по запасному порядку, Set).

**Для 5.4 (карточка в панели)**
- Сторона карточки = место `LiveBuilderMaterialSlot`: материал — `ReadPaint`/`TryAssign`, подгонка — `slot.Fit.Get()/Set()`; одним Undo — как `TryAssign(material, fit)`
  (`Undo.RecordObject(Owner)` один раз). Грань из режима Faces → `LiveBuilderMaterialSlots.ForSelected`.
- Reset — `Fit.Set(null)` (место уходит из списка; у «всей коробки» Set чистит все стороны — как у материала).
- World/Object — поле постройки (`BoxModel` / `SplineWall` / `SplineBuilding`); у коробки, выреза, комнаты, лестницы — их объекта (`GetComponentInParent<BoxModel>`).
- **Fit не сделан**: нужен `LiveBuilderTextureMapping.FitToFace(faceMin, faceMax, metersPerTile, quarterTurns)` — масштаб и сдвиг так, чтобы одна плитка легла на всю
  сторону коробки (`Box` коробки расчёта по `BoxFaces.SurfaceAxisU/V`); у `World` сдвиг до мира учитывать (`LiveBuilderTextureSpace.ShiftAlongAxis`) — иначе плитка съедет.
  Тесты плана `FitToFace_OneTilePerSide`, `Reset_ClearsFitOnly` — вместе с карточкой.
- Список плиток на странице «Материалы» окна (`LiveBuilderMaterialTilingDrawer` плана) не сделан: окно переделывает этап 3. В Project Settings список уже есть.

**Отступления от плана**
- `BoxModelSideFits` не отдельным полем `BoxModelSolverBox`, а внутри `BoxModelSideMaterials` (`Fits`): так сборщик (`BoxModelShapeCollector`), который правит миграция 2.6,
  не тронут — `BoxModelSideMaterials.Of(part)` сам берёт подгонки. Ключ материалов дописывает подгонки только если они есть.
- `BoxModelStairs` — одно поле `LiveBuilderTextureFit`, а не список на одно место.
- Мировой ключ стены и здания — сдвиги по осям и по направлению каждого отрезка (путь режут стены здания — сторона может начаться с любого), а не только первого.
- Копии повтора стен и зданий (`LiveBuilderArray`) развёрнуты как оригинал, без своего сдвига до мира — как было в 2.4.
- Фаска и скосы берут подгонку «как есть», а не «как у материала рядом»: при подгонке на стороне полоска фаски может не совпасть с гранью.
- Две проверки переноса (`MovingCommonParent_KeepsWallSignature`, `MovingWallWithChildCutters_KeepsSignature`) ставят стене `Object`: с `World` перенос законно меняет подпись.

**Для миграции 2.6 (важно)**
- `AddComponent<BoxModel/SplineWall/SplineBuilding>` в редакторе зовёт `Reset()` → `World`, фабрика тоже ставит `World`. Объект, который миграция создаёт
  вместо старого (группа, этаж-объект, здание, стена), должен получить `TextureAlignment` старого (у старых — `Object`), иначе развёртка после обновления другая.
  Отпечаток «по виду» (`LiveBuilderLookDigest`) развёртку не сверяет — такую разницу он не поймает.

**Проверить в редакторе (автор)**
- Alt + протянуть — камера как была; Alt + клик — «Picked …»; Alt + правый клик — «Applied …», контекстного меню нет. Если Unity не отдаёт отпускание с Alt —
  запасной вариант плана: ярлыки `Live Builder/Texture/Pick` (I) и `…/Apply` (Shift+I), они зовут те же `TryPick`/`TryApply`.
- Передвинуть World-здание — текстура «встаёт на место» через ~0,3 с после отпускания.

## После 6.1–6.4

**Как устроено**
- **Open.** `BoxModelStairsStyle.SeparateSteps` → `Open` (число 0, в сценах ничего не меняется), подписи «Open / Solid», значок `LiveBuilderOptionIcon.OpenSteps`.
  Инструмент «Лестница» и инспектор лестницы переведены на английский (подсказки — русские).
- **Полный скос.** `BoxModelEdgeChamfer.Full` = `+∞` в размере стороны; `SizeOnFirstFaceIn(boxSize)` / `SizeOnSecondFaceIn` — обрезанный по коробке,
  `IsFullOnFirstFace` / `IsFullOnSecondFace`. Срез (`BoxModelChamferCutter`), ручки и карточка «Chamfers» берут обрезанное; в подписи — `+∞` как есть.
  Потянул ручку или поменял число — скос становится обычным числом.
- **Пандус** — `BoxModelRamps`: `MakeRamp(part, risesTowardFace)` (полный скос ребра верх / сторона напротив подъёма), `TurnRamp` (по часовой сверху, своё Undo),
  `IsRamp`, `RiseFaceOf(протягивание, ось плоскости, повороты Tab)` — вдоль длинной горизонтальной стороны к концу протягивания; на стене — по её горизонтали.
  Стороны — номера `BoxFaces` (подъём только к ±X, ±Z).
- **Режимы «Коробки».** `LiveBuilderBoxShape` (Box / Ramp / Column) и `LiveBuilderBoxShapeSettings` (форма, стороны новой колонны, помнятся) — `LiveBuilderShapeSettings.cs`.
  В окне — переключатель «Shape» под «Mode». `BoxModelDrawGesture.TryBegin(…, shape)`: пандус и колонна всегда добавляют; Tab — `TurnRamp()` пока рисуешь,
  в превью — линия ската; колонна — без превью меша, только рамка. Готовое — `LastCreated`. На косой поверхности жест «по нормали» — только у Box.
  Tab вне рисования (`LiveBuilderSceneKeyboard.Rotate`) поворачивает выделенный пандус, как лестницу.
- **Развёртка ската.** `LiveBuilderFaceUnwrapper.ForSlopes()` — у постройки **World** косая грань скоса по своей плоскости: U — вдоль ребра, V — вверх по скату
  (у стоячего среза — по горизонтали и вверх), сдвиг до мира — по этим же направлениям. У **Object** — как раньше (ближайшая плоскость меша).
- **Gable Roof** — `SplineBuildingGableRoof`: `CanBuild(building, out reason)` (одна замкнутая линия, 4 точки, прямые стороны под прямым углом, без скруглений,
  здание версии 2), `Build(building, pitch, overhang)` — объект из коробок «Gable Roof» в `SplineBuildingStoreys.RoofOf`, X вдоль длинной стороны, два пандуса
  спиной к спине; ширина — по наружной стороне стен (`ContourSide`), свес со всех сторон; конёк — половина ширины × tg уклона над крышей, низ ската на свесе
  опускается ниже крыши на свес × tg. Повторное нажатие — тот же объект, коробки заново; парапет выключается тем же Undo. Кнопка, уклон и свес — секция
  «Gable Roof» в `SplineBuildingEditor` (уклон и свес — `LiveBuilderGableRoofSettings`, помнятся). Материал — `RoofMaterial` здания, если задан.
- **Колонна** — `LiveBuilderColumn` (Runtime): стороны 4–32, `Size` (X, Z — прямоугольник основания, Y — высота, опора — нижний угол), материалы бока / верха / низа,
  фаска (размер, куски), коллайдер, `TextureAlignment` + `TextureFits` (места `LiveBuilderColumnSurface`: Sides, Top, Bottom). `Reset()` и фабрика — World.
  - меш — `LiveBuilderColumnMeshBuilder`: многоугольник, описанный вокруг эллипса (при 4, 8, 12… сторонах грани касаются сторон прямоугольника),
    кольца фаски, гладкий бок от 12 сторон, U бока — длина по кругу, V — высота; крышки по плану; копии массива в том же меше;
  - сборка — `LiveBuilderColumnRebuilder` (подпись `c…`, черновик/чистовая как у стен, `LiveBuilderMeshWriter.Write` с пустым списком вырезов, коллайдер, префабы);
  - шаг очереди `ColumnPass` — сразу после `BoxModelPass` (`LiveBuilderRebuildStage.ColumnPass`);
  - создание — `LiveBuilderColumnFactory.CreateInBox`: в объекте из коробок — его ребёнок с его материалом и фаской; без объекта — отдельно, туда же, куда новый
    объект (`BoxModelFactory.PutIntoLevelGeometry` стал `internal`); в здании — тем же размещением, что коробка (этаж, «и этажи выше» — `LiveBuilderArray`);
  - ручки граней — `LiveBuilderColumnHandles` (размер по сетке, опора едет за нижним углом), перенос — стрелки выделенного.

**Чек-лист 5.3 для колонны**
- `LiveBuilderPathOwners.OwnerOf` — коллайдер колонны не стена (даже в этаже здания) — сделано.
- `LiveBuilderClickSelection` / `LiveBuilderSceneHit` — `Column`, `IsColumnCloser`: клик выделяет колонну, если она ближе грани объекта — сделано.
- `LiveBuilderSelectionHandles` — ручки колонны (`LiveBuilderColumnHandles`) — сделано.
- `LiveBuilderSelectionInfo` — рамка «Column, sides: N» по её размеру — сделано.
- `LiveBuilderMaterialSlots` — под мышью бок / верх / низ, «вся колонна» по выделенному; пипетка работает через них — сделано.
- `LiveBuilderPointSnap` — центр и 4 крайние точки внизу и вверху — сделано (подписи привязки переведены: corner, midpoint, point, on edge).
- `LiveBuilderTool.CanEdit(GameObject)` — колонна; правят «Коробка» и «Выделение» — сделано.
- `LiveBuilderConstructionCard` — класса ещё нет (этап 3). Сейчас: инспектор колонны в окне — `LiveBuilderSelectionCard.OwnComponentOf`
  и `LiveBuilderSelectionTab.IsLiveBuilderObject` — сделано.
- `BoxModelMeshSaving` (UV2 при сохранении) — сделано. `BoxModelMeshCleanup` — по папкам файлов, колонна ложится в те же — правка не нужна.
- Префабы — `BoxModelPrefabInstances.TryUsePrefabGeometry` у колонны и пересборка префаба (`RebuildPrefab`) — сделано.
- Ещё: `LiveBuilderDraftBuilds.Finish`, `LiveBuilderRebuildScope.IsConstruction`, `LiveBuilderTextureSpace.AlignmentOf`, `LiveBuilderMoveHandle.MovableOf` — сделано.
- Не трогал: `LiveBuilderPivotMover` (пивот колонны — всегда нижний угол), `LiveBuilderLevelCheck`, изоляция (меш колонны на ней самой — видна и так).

**Тесты** — `LiveBuilderOutdoorShapesTests` (19): Open из старых чисел; пандус — протягивание, Tab, растяжение коробки, коллайдер по скату, V ската = длина ската;
Gable Roof — 12 × 8, повторное нажатие и новый этаж, Г-образный контур, один Ctrl+Z; колонна — режим Column, 4 и 32 стороны, кольца фаски, не режется вырезом,
материал на бок и верх, общий меш экземпляров префаба, повтор в своём меше, клик и коллайдер; «Готово, когда» — двор с пандусом, 4 колоннами, аркой-вырезом
и навесом с Gable Roof без ProBuilder.

**Отступления от плана**
- **Скат по своей плоскости — только у World.** У Object (все старые объекты, эталонный уровень) — прежняя развёртка: иначе изменились бы UV старых пандусов
  и эталон `Meshes_MatchBaseline` (его отпечаток включает UV).
- `LiveBuilderRiseDirection` не вынесен из лестницы: у пандуса направление проще (длинная сторона к концу протягивания + Tab) — `BoxModelRamps.RiseFaceOf`.
  Стрелка на полу — линия ската в превью. Режимы — не части-наследники `Tools/Box/…` (этап 3, шаг 3.2): переключатель формы в нынешнем инструменте.
- `BoxModelDrawResultKind` не заведён — тот же `LiveBuilderBoxShape`; `LastCreatedPart` → `LastCreated` (коробка, колонна или вырез).
- `LiveBuilderColumnSettings` нет — стороны новой колонны в `LiveBuilderBoxShapeSettings`; `LiveBuilderGableRoofSettings` — в том же файле.
- `LiveBuilderMeshWriter.Write` не менялся: «без вырезов» — пустой список вырезов.
- Кнопка Gable Roof — в инспекторе здания (`SplineBuildingEditor`), не в `LiveBuilderConstructionCard`; уклон и свес — там же.
- Колонна на стене (плоскость рисования — стена) всё равно вертикальная в осях объекта; на косой поверхности — как на плоскости, найденной жестом коробки.

**Для 6.5 (эталон и документ)**
- Двор эталона (`LiveBuilderReferenceYard`): пандус — `BoxModelRamps.MakeRamp` вместо скоса числами (размер `Full`); 4 колонны `LiveBuilderColumn`
  (рядом с колоннадой коробок, её не заменять — на ней 300 коробок замера); арка — `GeometryCutter` с `TopCornerRadius = ширина / 2` в стенке двора;
  навес — здание 6 × 4 в один этаж рядом с двором + `SplineBuildingGableRoof.Build(…, 30°, 0,3)`. Проверку `Build_HasAllPieces` дополнить
  (колонны, пандус `IsRamp`, арка, крыша «Gable Roof» в `Roof`). Отпечатки `WriteBaseline` / `WriteLookBaseline` — записать заново по указанию менеджера.
  Двор эталона — Object: развёртка ската там прежняя; если двор сделать World — UV ската изменятся (так и задумано).
- LiveBuilder.md: режимы «Коробки» (Box / Ramp / Column, Tab), колонна, Gable Roof, Open; из «Ловушек» убрать «Крыша здания — только плоская».
  Автору: колонна — отдельный объект и в вырезах не участвует (арка сквозь колонну её не прорежет); «Open» был и раньше («Ступени»), переименован.

**Проверить в редакторе (автор)**
- «Коробка» → Shape Ramp: протянуть 2 × 6, Tab во время протягивания и на шаге высоты, клик — пандус; Tab с выделенным пандусом — поворот.
- Shape Column: колонна на полу, на объекте из коробок (ребёнок объекта), в здании с «и этажи выше»; ручки граней, перенос стрелками, перетаскивание материала на бок и верх.
- Здание-прямоугольник → инспектор, секция «Gable Roof»: уклон, свес, кнопка; поменять контур — «Rebuild Gable Roof»; добавить этаж — крыша едет; Ctrl+Z.
