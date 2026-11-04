using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона инструмента окна Live Builder: настройки выбранного инструмента — только те, что на него действуют, карточками:
    /// как рисовать (форма, точки) → размер → материалы; что выделяет клик — у «Выделения» и «Коробки»;
    /// «в здании — этот этаж или все выше» — у инструментов, которые ставят своё в здание.
    /// Уже созданное не меняет: его числа — в зоне выделенного (<see cref="LiveBuilderSelectionPanel"/>).
    /// Числа хранятся у каждого человека свои — настройки инструментов, например <see cref="LiveBuilderRoomSettings"/>.
    /// </summary>
    public static class LiveBuilderToolSettingsPanel
    {
        public static void Draw()
        {
            DrawCurrentToolSettings();

            if (CanPlaceIntoBuilding(LiveBuilderToolSetting.Current))
                DrawBuildingStoreys();
        }

        // Эти инструменты, наведённые на здание по контуру, ставят своё в него, на его этажи (<see cref="LiveBuilderBuildingContext"/>).
        private static bool CanPlaceIntoBuilding(LiveBuilderTool tool)
        {
            return tool is LiveBuilderTool.Box or LiveBuilderTool.Room or LiveBuilderTool.Wall or LiveBuilderTool.Stairs or LiveBuilderTool.Opening;
        }

        private static void DrawBuildingStoreys()
        {
            using (LiveBuilderWindowControls.Card("В здании"))
                LiveBuilderBuildingContext.IsOnAllStoreys = LiveBuilderWindowControls.Switch(
                    new GUIContent("Этажи", "Наведёшь на здание — поставленное встанет на этот этаж здания, до его потолка, с его толщинами. " +
                                            "И все выше — то же на каждом этаже выше: перегородки, лестничная клетка, окна по фасаду. После перезапуска Unity — снова «только этот»"),
                    LiveBuilderBuildingContext.IsOnAllStoreys, ("Только этот", LiveBuilderOptionIcon.ThisStorey), ("И все выше", LiveBuilderOptionIcon.AllStoreysAbove));
        }

        private static void DrawCurrentToolSettings()
        {
            switch (LiveBuilderToolSetting.Current)
            {
                case LiveBuilderTool.Room:
                    DrawRoomSettings();
                    break;

                case LiveBuilderTool.Building:
                    DrawBuildingSettings();
                    break;

                case LiveBuilderTool.Wall:
                    DrawWallSettings();
                    break;

                case LiveBuilderTool.Opening:
                    DrawOpeningSettings();
                    break;

                case LiveBuilderTool.Stairs:
                    DrawStairsSettings();
                    break;

                case LiveBuilderTool.Ruler:
                    DrawRulerSettings();
                    break;

                case LiveBuilderTool.Select:
                    DrawSelectSettings();
                    break;

                default:
                    DrawBoxSettings();
                    break;
            }
        }


        // ── Выделение ────────────────────────────────────────────────────

        private static void DrawSelectSettings()
        {
            using (LiveBuilderWindowControls.Card("Клик"))
                DrawSelectionModeSwitch();

            LiveBuilderWindowControls.Explanation("Select", "Клик выделяет что угодно: коробку, комнату, лестницу, стену, здание, проём; Shift — добавить или убрать. " +
                                           "У выделенного — все его ручки: грани коробок (с Ctrl — выдавить новую), точки стен и контуров, ручки проёмов, повтор. " +
                                           "Остальные инструменты создают своё и правят только своё. Esc в них — сюда.");
        }


        // ── Коробка ──────────────────────────────────────────────────────

        private static void DrawBoxSettings()
        {
            using (LiveBuilderWindowControls.Card("Рисование"))
            {
                var drawMode = (BoxModelDrawMode)LiveBuilderWindowControls.Switch(
                    new GUIContent("Режим", "Что делает нарисованная коробка. Авто — наружу добавляет, внутрь вырезает"),
                    (int)BoxModelDrawModeSetting.Current,
                    ("Авто", LiveBuilderOptionIcon.BoxAuto), ("Добавить", LiveBuilderOptionIcon.BoxAdd), ("Вырезать", LiveBuilderOptionIcon.BoxCut));

                if (drawMode != BoxModelDrawModeSetting.Current)
                    BoxModelDrawModeSetting.Current = drawMode;

                DrawSelectionModeSwitch();
                DrawTargetModel();
            }

            LiveBuilderWindowControls.Explanation("Box", "Протяни по поверхности, мышью глубина, клик — готово. Клик по грани — выделить коробку или кусок грани, " +
                                           "смотря что выбрано в «Клик выделяет». Здесь правятся только коробки; остальное — в своих инструментах или в «Выделении».");
        }

        // Куда встанет коробка на пустом месте: в выделенный объект, иначе — новый. По чужой поверхности — всегда в тот объект.
        // «Новый объект» нажат — следующая коробка точно отдельным объектом; нажать ещё раз — отмена.
        private static void DrawTargetModel()
        {
            var selectedModel = BoxModelActions.SelectedModel();
            var isNewRequested = BoxModelSceneTool.IsNewModelRequested;
            var targetName = isNewRequested ? "новый, отдельный" : selectedModel != null ? selectedModel.name : "новый (ничего не выделено)";

            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(new GUIContent("Объект", "Куда встанет коробка, нарисованная на пустом месте. " +
                                                                     "Рисуешь по поверхности другого объекта — она встанет в него"));
                LiveBuilderWindowControls.ValueChip(new GUIContent(targetName, targetName), GUILayout.MinWidth(40f));

                var isPressed = LiveBuilderWindowControls.IconToggle(isNewRequested, LiveBuilderOptionIcon.NewObject, "Новый",
                    "Следующая коробка — отдельный объект, даже поверх другого. Нажать ещё раз — отмена");

                if (isPressed == isNewRequested)
                    return;

                if (isPressed)
                    BoxModelActions.BeginNewModelDrawing();
                else
                    BoxModelSceneTool.IsNewModelRequested = false;

                SceneView.RepaintAll();
            }
        }


        // ── Комната ──────────────────────────────────────────────────────

        private static void DrawRoomSettings()
        {
            using (LiveBuilderWindowControls.Card("Форма"))
            {
                LiveBuilderRoomSettings.Shape = (LiveBuilderRoomShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Рисовать", "Прямоугольник — протянул по полу. По контуру — кликами точки, любая форма; " +
                                               "в доме из коробок по контуру встают стены до его потолка, а не вырез"),
                    (int)LiveBuilderRoomSettings.Shape, ("Прямоугольник", LiveBuilderOptionIcon.Rectangle), ("По контуру", LiveBuilderOptionIcon.Contour));

                if (LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour)
                    DrawKnotModeSwitch();

                LiveBuilderRoomSettings.WallSide = (LiveBuilderRoomWallSide)LiveBuilderWindowControls.Switch(
                    new GUIContent("Стены", "Куда встают стены от того, что рисуешь. Наружу — нарисованное и есть комната изнутри. " +
                                            "Внутрь — нарисованное — габарит вместе со стенами. По центру — линия идёт по середине стен. " +
                                            "Прямоугольник в доме из коробок вырезается — своих стен у него нет"),
                    (int)LiveBuilderRoomSettings.WallSide, "Наружу", "Внутрь", "По центру");
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderRoomSettings.WallThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина стен, м", "Стены вокруг комнаты, нарисованной в пустоте. Внутри дома стены — то, что осталось от его объёма"),
                    LiveBuilderRoomSettings.WallThickness);

                LiveBuilderRoomSettings.SlabThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина перекрытия, м", "Пол и потолок. Высота комнаты — этаж минус перекрытие"), LiveBuilderRoomSettings.SlabThickness);

                LiveBuilderWindowControls.Computed("Высота комнаты", BoxModelSceneText.FormatMeters(LiveBuilderRoomSettings.ClearHeight) + " м", "Этаж минус перекрытие");
                DrawPartitionThickness();
            }

            using (LiveBuilderWindowControls.Card("Материалы"))
            {
                LiveBuilderRoomSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Пол", "Пусто — материал объекта", LiveBuilderRoomSettings.FloorMaterial);
                LiveBuilderRoomSettings.WallMaterial = LiveBuilderWindowControls.MaterialField("Стены", "Пусто — материал объекта", LiveBuilderRoomSettings.WallMaterial);
                LiveBuilderRoomSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Потолок", "Пусто — материал объекта", LiveBuilderRoomSettings.CeilingMaterial);
            }
        }


        // ── Стена ────────────────────────────────────────────────────────

        private static void DrawWallSettings()
        {
            using (LiveBuilderWindowControls.Card("Линия"))
            {
                DrawKnotModeSwitch();

                LiveBuilderWallSettings.Alignment = (SplineWallAlignment)LiveBuilderWindowControls.Switch(
                    new GUIContent("Где стена", "Линия посередине стены или по её краю — удобно обводить по краю пола"),
                    (int)LiveBuilderWallSettings.Alignment, "По центру", "Слева", "Справа");
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderWallSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Тип", "Толщина и материал — из типа, общие для всех стен этого типа: поменял тип — перестроились все. Без типа — свои числа ниже"),
                    LiveBuilderWallSettings.WallType);

                if (LiveBuilderWallSettings.WallType == null)
                    LiveBuilderWallSettings.Thickness = EditorGUILayout.FloatField(new GUIContent("Толщина, м", "Наружная — 0,3, перегородка — 0,15"),
                        LiveBuilderWallSettings.Thickness);

                LiveBuilderWallSettings.IsStoreyHigh = !LiveBuilderWindowControls.Switch(new GUIContent("Высота", "До потолка — этаж минус перекрытие. Своя — число ниже"),
                    !LiveBuilderWallSettings.IsStoreyHigh, "До потолка", "Своя");

                if (LiveBuilderWallSettings.IsStoreyHigh)
                    LiveBuilderWindowControls.Computed(" ", BoxModelSceneText.FormatMeters(LiveBuilderWallSettings.Height) + " м", "Этаж минус перекрытие");
                else
                    LiveBuilderWallSettings.CustomHeight = EditorGUILayout.FloatField(new GUIContent("Своя высота, м", "Высота стены над линией"),
                        LiveBuilderWallSettings.CustomHeight);

                DrawPartitionThickness();
            }

            if (LiveBuilderWallSettings.WallType == null)
                using (LiveBuilderWindowControls.Card("Материал"))
                    LiveBuilderWallSettings.Material = LiveBuilderWindowControls.MaterialField("Стена", "Пусто — серый материал набора", LiveBuilderWallSettings.Material);
        }


        // ── Здание ───────────────────────────────────────────────────────

        private static void DrawBuildingSettings()
        {
            using (LiveBuilderWindowControls.Card("Контур"))
            {
                DrawKnotModeSwitch();

                LiveBuilderBuildingSettings.ContourSide = (SplineBuildingContourSide)LiveBuilderWindowControls.Switch(
                    new GUIContent("Контур — это", "Снаружи — контур это габарит здания. Внутри — стены комнаты изнутри. Середина — ось стен"),
                    (int)LiveBuilderBuildingSettings.ContourSide, "Снаружи", "Внутри", "Середина");
            }

            using (LiveBuilderWindowControls.Card("Этажи"))
            {
                LiveBuilderBuildingSettings.StoreyCount = EditorGUILayout.IntSlider(new GUIContent("Этажей", "Перекрытия между этажами ставятся сами"),
                    LiveBuilderBuildingSettings.StoreyCount, 1, SplineBuilding.MaxStoreys);
                LiveBuilderWindowControls.Computed("Высота этажа", BoxModelSceneText.FormatMeters(LiveBuilderStoreys.StoreyHeight) + " м",
                    "Как у этажей уровня — в зоне «Уровень»");
            }

            using (LiveBuilderWindowControls.Card("Стены и перекрытия"))
            {
                LiveBuilderBuildingSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Тип стен", "Толщина и материал снаружи — из типа, общие со всеми стенами этого типа. Без типа — свои числа"),
                    LiveBuilderBuildingSettings.WallType);

                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.WallThickness = EditorGUILayout.FloatField(new GUIContent("Толщина стен, м", "Наружная — 0,3"),
                        LiveBuilderBuildingSettings.WallThickness);

                LiveBuilderBuildingSettings.SlabThickness = EditorGUILayout.FloatField(new GUIContent("Толщина перекрытий, м", "Пол, перекрытия и крыша"),
                    LiveBuilderBuildingSettings.SlabThickness);

                using (new EditorGUILayout.HorizontalScope())
                {
                    LiveBuilderWindowControls.PrefixLabel(new GUIContent("Есть", "Нажата — есть, бледная — нет"));
                    LiveBuilderBuildingSettings.HasFloor = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasFloor, LiveBuilderOptionIcon.Floor,
                        "Пол", "Плита пола первого этажа");
                    LiveBuilderBuildingSettings.HasRoof = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasRoof, LiveBuilderOptionIcon.Roof,
                        "Крыша", "Плоская крыша сверху. Нет — сверху видно этажи");
                }
            }

            using (LiveBuilderWindowControls.Card("Материалы"))
            {
                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.OuterWallMaterial = LiveBuilderWindowControls.MaterialField("Стены снаружи", "Пусто — серый материал набора",
                        LiveBuilderBuildingSettings.OuterWallMaterial);

                LiveBuilderBuildingSettings.InnerWallMaterial = LiveBuilderWindowControls.MaterialField("Стены изнутри", "Пусто — как снаружи", LiveBuilderBuildingSettings.InnerWallMaterial);
                LiveBuilderBuildingSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Пол", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.FloorMaterial);
                LiveBuilderBuildingSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Потолок", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.CeilingMaterial);
                LiveBuilderBuildingSettings.RoofMaterial = LiveBuilderWindowControls.MaterialField("Крыша", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.RoofMaterial);
            }

            LiveBuilderWindowControls.Explanation("Building", "Здание встанет на выбранный этаж: его пол — на полу этажа, этажи — те же, что в окне.");
        }


        // ── Проём ────────────────────────────────────────────────────────

        // Форма — общая для клика и протягивания; размер — для клика; глубина — для протягивания.
        private static void DrawOpeningSettings()
        {
            using (LiveBuilderWindowControls.Card("Форма"))
                LiveBuilderCutterSettings.Shape = (LiveBuilderCutterShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Новый проём", "Прямоугольник — дверь, окно. Арка — верх полукругом. Круг — круглое окно; не квадрат — овал. " +
                                                  "Потом всё меняется у самого проёма: размер, скругление углов, форма"),
                    (int)LiveBuilderCutterSettings.Shape,
                    ("Прямоугольник", LiveBuilderOptionIcon.OpeningRectangle), ("Арка", LiveBuilderOptionIcon.OpeningArch), ("Круг", LiveBuilderOptionIcon.OpeningCircle));

            DrawOpeningSizeSettings();

            using (LiveBuilderWindowControls.Card("Протянуть — свой размер"))
            {
                LiveBuilderCutterSettings.IsThrough = !LiveBuilderWindowControls.Switch(
                    new GUIContent("Резать", "Насквозь — через всю толщину стены, пола или перекрытия, на котором рисуешь. Ниша — на свою глубину от поверхности"),
                    !LiveBuilderCutterSettings.IsThrough, "Насквозь", "Нишу");

                if (!LiveBuilderCutterSettings.IsThrough)
                    LiveBuilderCutterSettings.NicheDepth = EditorGUILayout.FloatField(new GUIContent("Глубина ниши, м", "От поверхности внутрь"),
                        LiveBuilderCutterSettings.NicheDepth);
            }

            using (LiveBuilderWindowControls.Card("Вид"))
                LiveBuilderCutterSettings.RevealMaterial = LiveBuilderWindowControls.MaterialField("Стенки дыры", "Пусто — материал того, что режем",
                    LiveBuilderCutterSettings.RevealMaterial);

            LiveBuilderWindowControls.Explanation("Opening", "Клик по стене — проём этого размера. Протяни по любой поверхности, хоть по косой стене, — свой размер. " +
                                           "Проём — отдельный объект: двигай и поворачивай как угодно, дыра едет за ним; в стене по линии и здании — ещё и за их точками. " +
                                           "Режет коробки, стены и здания, но не свой префаб: дверь-префаб с проёмом внутри сама прорезает стену, куда её поставил.");
        }

        // Готовый размер или свой — для проёма кликом.
        private static void DrawOpeningSizeSettings()
        {
            using (LiveBuilderWindowControls.Card("Клик — готовый размер"))
            {
                LiveBuilderCutterSettings.IsSnappedToGrid = LiveBuilderWindowControls.Switch(
                    new GUIContent("Место", "Где навёл — середина проёма ровно под мышью. По сетке — левый край и низ прилипают к сетке: проёмы встают ровными рядами. " +
                                            "В здании с этажами низ — всегда от пола его этажа, на «Низ над полом»"),
                    LiveBuilderCutterSettings.IsSnappedToGrid, ("Где навёл", LiveBuilderOptionIcon.AtCursor), ("По сетке", LiveBuilderOptionIcon.Grid));

                var presets = LiveBuilderOpeningPresets.Presets;
                var names = new string[presets.Count + 1];

                for (var index = 0; index < presets.Count; index++)
                    names[index] = presets[index].DisplayName;

                names[presets.Count] = "Свой размер";

                var selectedIndex = LiveBuilderOpeningPresets.SelectedIndex;
                var gridIndex = selectedIndex == LiveBuilderOpeningPresets.CustomIndex ? presets.Count : selectedIndex;
                var chosen = GUILayout.SelectionGrid(gridIndex, names, 2, EditorStyles.miniButton);

                if (chosen != gridIndex)
                    LiveBuilderOpeningPresets.SelectedIndex = chosen == presets.Count ? LiveBuilderOpeningPresets.CustomIndex : chosen;

                EditorGUILayout.Space(2f);

                if (LiveBuilderOpeningPresets.SelectedIndex != LiveBuilderOpeningPresets.CustomIndex)
                {
                    var size = LiveBuilderOpeningPresets.Selected;
                    LiveBuilderWindowControls.Computed("Ширина × высота", BoxModelSceneText.FormatMeters(size.Width) + " × " + BoxModelSceneText.FormatMeters(size.Height) + " м");
                    LiveBuilderWindowControls.Computed("Низ над полом этажа", BoxModelSceneText.FormatMeters(size.Sill) + " м");
                    return;
                }

                LiveBuilderOpeningPresets.CustomWidth = EditorGUILayout.FloatField("Ширина, м", LiveBuilderOpeningPresets.CustomWidth);
                LiveBuilderOpeningPresets.CustomHeight = EditorGUILayout.FloatField("Высота, м", LiveBuilderOpeningPresets.CustomHeight);
                LiveBuilderOpeningPresets.CustomSill = EditorGUILayout.FloatField(new GUIContent("Низ над полом, м", "0 — дверь, 1 — окно"),
                    LiveBuilderOpeningPresets.CustomSill);
            }
        }


        // ── Лестница ─────────────────────────────────────────────────────

        private static void DrawStairsSettings()
        {
            using (LiveBuilderWindowControls.Card("Вид"))
            {
                var isSolid = LiveBuilderWindowControls.Switch(
                    new GUIContent("Ступени", "Отдельные — плиты, под лестницей пусто. Сплошная — ступенчатый объём от пола"),
                    LiveBuilderStairsSettings.Style == BoxModelStairsStyle.Solid, ("Отдельные", LiveBuilderOptionIcon.SeparateSteps), ("Сплошная", LiveBuilderOptionIcon.SolidSteps));
                LiveBuilderStairsSettings.Style = isSolid ? BoxModelStairsStyle.Solid : BoxModelStairsStyle.SeparateSteps;

                if (LiveBuilderStairsSettings.Style == BoxModelStairsStyle.SeparateSteps)
                    LiveBuilderStairsSettings.StepThickness = EditorGUILayout.FloatField(
                        new GUIContent("Толщина ступени, м", "0,2 — ступени сходятся углами; меньше — висят отдельно"), LiveBuilderStairsSettings.StepThickness);
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderStairsSettings.IsStoreyHigh = !LiveBuilderWindowControls.Switch(
                    new GUIContent("Высота сначала", "С какой высоты начинается выбор высоты мышью: с высоты этажа или со своей, например полэтажа 2 м. В здании — с его этажа"),
                    !LiveBuilderStairsSettings.IsStoreyHigh, "Этаж", "Своя");

                if (!LiveBuilderStairsSettings.IsStoreyHigh)
                    LiveBuilderStairsSettings.CustomHeight = EditorGUILayout.FloatField(new GUIContent("Своя высота, м", "Высота, с которой начинается выбор мышью"),
                        LiveBuilderStairsSettings.CustomHeight);

                LiveBuilderStairsSettings.DefaultWidth = EditorGUILayout.FloatField(
                    new GUIContent("Ширина по умолчанию, м", "Если протянул почти без ширины — просто в сторону подъёма. Иначе ширина — какую протянул"),
                    LiveBuilderStairsSettings.DefaultWidth);

                LiveBuilderStairsSettings.CutsAbove = LiveBuilderWindowControls.Switch(
                    new GUIContent("Перекрытие над", "Не пробивать — проём в полу ставишь сам вырезающей коробкой. Пробивать — режет над маршем от 2,1 м над полом лестницы до её верха"),
                    LiveBuilderStairsSettings.CutsAbove, "Не пробивать", "Пробивать");
            }

            LiveBuilderWindowControls.Explanation("Stairs", "Как кубик: протянул прямоугольник — длина и ширина, низ — где начал; отпустил — мышью высота, клик — готово. Tab — повернуть. " +
                                           "Готовую — ручки на гранях, Tab, «Повернуть» и «Развернуть»; на любой угол — поверни её объект.");
        }


        // ── Линейка ──────────────────────────────────────────────────────

        private static void DrawRulerSettings()
        {
            using (LiveBuilderWindowControls.Card("Как читать"))
                LiveBuilderWindowControls.Note("Зелёным — легко, жёлтым — на пределе или только в вис, красным — не допрыгнуть. " +
                                               "Таблица прыжков — в Project Settings → FeatherKit → Live Builder; там же она включается. " +
                                               "Цифры грубые, на глаз: точный расчёт — у настроек прыжка игры.");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Что выделяет клик — у «Выделения» и «Коробки»: коробку целиком или кусок грани.
        private static void DrawSelectionModeSwitch()
        {
            var selectionMode = (BoxModelSelectionMode)LiveBuilderWindowControls.Switch(
                new GUIContent("Клик выделяет", "Коробки — коробку целиком, тянешь точки на её гранях. " +
                                                "Грани — видимый кусок грани, двойной клик — грань целиком, Shift — несколько; стрелки двигают все"),
                (int)BoxModelSelectionModeSetting.Current, ("Коробки", LiveBuilderOptionIcon.Boxes), ("Грани", LiveBuilderOptionIcon.Faces));

            if (selectionMode != BoxModelSelectionModeSetting.Current)
                BoxModelSelectionModeSetting.Current = selectionMode;
        }

        // Угол или плавно — общий для стены, здания и комнаты по контуру.
        private static void DrawKnotModeSwitch()
        {
            LiveBuilderWallSettings.IsSmooth = LiveBuilderWindowControls.Switch(
                new GUIContent("Новая точка", "Угол — прямые отрезки. Плавно — кривая через точки. Shift на клике — наоборот"),
                LiveBuilderWallSettings.IsSmooth, ("Угол", LiveBuilderOptionIcon.Corner), ("Плавно", LiveBuilderOptionIcon.Smooth));
        }

        // Перегородка внутри здания — у стены и комнаты общая: тип или своя толщина.
        private static void DrawPartitionThickness()
        {
            LiveBuilderWallSettings.PartitionWallType = LiveBuilderWallTypes.Popup(
                new GUIContent("Перегородка в здании", "Тип стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionWallType);

            if (LiveBuilderWallSettings.PartitionWallType != null)
                return;

            LiveBuilderWallSettings.PartitionThickness = EditorGUILayout.FloatField(
                new GUIContent("Толщина перегородки, м", "Толщина стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionThickness);
        }
    }
}
