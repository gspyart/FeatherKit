using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Лестница», как «Коробка», в два шага: протянул прямоугольник по полу — это и есть лестница, длина и ширина — какие протянул;
    /// отпустил — мышью вверх-вниз высота (по 0,2 — подъём ступени, сначала — высота этажа), клик или Enter — готово.
    /// Низ — там, где начал; подъём — вдоль длинной стороны, к концу протягивания. Направление запоминается по первым 0,4 м
    /// и дальше не прыгает, Tab — повернуть на четверть оборота. Почти не протянул вдоль — длина по метрикам (ступени 0,2 × 0,3),
    /// почти не протянул поперёк — ширина по умолчанию. Глубина ступени подгоняется под длину.
    /// Перекрытие над маршем пробивает сразу («Перекрытие над» в настройках лестницы).
    /// Лестница встаёт в объект, на чьём полу начали, или в выделенный; иначе — новым объектом (его можно повернуть на любой угол).
    /// В здании по контуру — в его объект, высота сначала — его этаж; «И этажи выше» — лестничная клетка до верхнего этажа.
    /// На площадке, которая уже часть здания, — тоже его часть: второй марш встаёт к этажам, как и первый.
    /// Форма «С площадкой»: прямоугольник — вся лестничная клетка. Марши по метрикам и средняя площадка (не мельче ширины марша) — у дальнего конца,
    /// у ближнего остаётся этажная площадка — пол, перекрытие над ней цело; клетка короткая — ступени мельче. Первый марш — с той стороны, откуда начал тянуть.
    /// </summary>
    public sealed class LiveBuilderStairsGesture
    {
        // Сколько протянуть, чтобы направление запомнилось, м.
        private const float DirectionLockDistance = 0.4f;

        // Протянул меньше — длина по метрикам или ширина по умолчанию, м.
        private const float MinDrawnLength = 1f;
        private const float MinDrawnWidth = 0.5f;

        // Ступень по метрикам, м.
        private const float StandardStepHeight = 0.2f;
        private const float StandardStepDepth = 0.3f;

        // У лестницы с площадкой: площадка не мельче, зазор между маршами, м.
        private const float MinLandingDepth = 1.2f;
        private const float StandardFlightGap = 0.2f;

        private readonly LiveBuilderFloorRectangle rectangle = new();

        // Точная высота с клавиатуры: набрал «2,4», Enter — лестница ровно такой высоты.
        private readonly LiveBuilderTypedLength typedHeight = new();

        private BoxModelStairsDirection? lockedDirection;

        // Второй шаг: прямоугольник готов, мышью выбирается высота.
        private bool isChoosingHeight;
        private float chosenHeight;
        private float heightAtStart;
        private Vector2 heightMouseStart;


        public bool IsActive => rectangle.IsActive;

        public bool IsChoosingHeight => rectangle.IsActive && isChoosingHeight;

        // Высота, с которой начинается выбор: в здании — его этаж, иначе — из настроек.
        private float StoreyStairsHeight => rectangle.BuildingContext != null ? rectangle.BuildingContext.Building.StoreyHeight : LiveBuilderStairsSettings.Height;

        // Высота лестницы сейчас: выбранная мышью или та, с которой начнём.
        private float StairsHeight => isChoosingHeight ? chosenHeight : StoreyStairsHeight;

        public string Hint => isChoosingHeight && typedHeight.IsTyping
            ? "Высота " + typedHeight.Text + " м: Enter — готово, Backspace — стереть цифру, Esc — стереть число"
            : isChoosingHeight
            ? "Мышь вверх-вниз — высота: " + LiveBuilderSceneText.FormatMeters(chosenHeight) + " м (этаж — " + LiveBuilderSceneText.FormatMeters(StoreyStairsHeight) +
              "); цифры + Enter — точно. Клик или Enter — готово, Tab — повернуть, Esc — отмена"
            : rectangle.TypedText.Length > 0
            ? "Набрано " + rectangle.TypedText + " м: X — вторая сторона (по X и по Z); отпусти — ровно такая. Esc — стереть набор"
            : IsSwitchback
            ? "Вверх — " + DirectionName(CurrentDirection) + ". Тяни всю лестничную клетку — марши и площадка встанут сами; «4 X 8» — точно. Tab — повернуть. Отпусти — дальше высота. Esc — отмена"
            : "Вверх — " + DirectionName(CurrentDirection) + ". Тяни прямоугольник — длина и ширина лестницы; «1,2 X 4» — точно. Tab — повернуть. Отпусти — дальше высота. Esc — отмена";

        // Куда пойдёт вверх: запомненное, а пока не запомнилось — куда тянут сильнее.
        private BoxModelStairsDirection CurrentDirection => lockedDirection ?? DirectionOf(rectangle.End - rectangle.Start);

        private static bool IsSwitchback => LiveBuilderStairsSettings.Shape == BoxModelStairsShape.Switchback;


        // ── Жест ─────────────────────────────────────────────────────────

        // Сначала сброс: прошлый жест мог кончиться сам (удалили здание) и оставить выбор высоты.
        public bool TryBegin(Ray worldRay)
        {
            Cancel();
            return rectangle.TryBegin(worldRay);
        }


        public void Update(Ray worldRay)
        {
            rectangle.Update(worldRay);

            var delta = rectangle.End - rectangle.Start;

            if (lockedDirection == null && Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.z)) >= DirectionLockDistance)
                lockedDirection = DirectionOf(delta);
        }


        // Отпустил мышь — прямоугольник готов, дальше высота. Не протянул — ничего.
        public void FinishFootprint(Vector2 mousePosition)
        {
            if (!TryGetShape(out _, out _, out _, out _))
            {
                Cancel();
                return;
            }

            chosenHeight = StoreyStairsHeight;
            heightAtStart = chosenHeight;
            heightMouseStart = mousePosition;
            isChoosingHeight = true;
        }


        // Пока выбираешь высоту: цифры — точная высота, Esc при наборе — стереть число. Enter с набранным — эта высота и готово.
        // Возвращает, забрал ли клавишу.
        public bool HandleKey(KeyCode key)
        {
            // Пока тянешь прямоугольник: цифры и X — ширина и длина (<see cref="LiveBuilderFloorRectangle.HandleKey"/>).
            if (!IsChoosingHeight)
                return rectangle.HandleKey(key);

            if (typedHeight.TryType(key))
                return true;

            if (key == KeyCode.Escape && typedHeight.IsTyping)
            {
                typedHeight.Clear();
                return true;
            }

            if (key is not (KeyCode.Return or KeyCode.KeypadEnter) || !typedHeight.IsTyping)
                return false;

            // Набрано не то — набор стёрт, лестница ждёт дальше, как и коробка.
            if (!typedHeight.TryTake(out var meters))
                return true;

            chosenHeight = Mathf.Max(StandardStepHeight, meters);
            Commit();
            return true;
        }


        // Мышь вверх-вниз от точки, где отпустил, — высота по подъёму ступени.
        public void UpdateHeight(Vector2 mousePosition)
        {
            if (!IsChoosingHeight || typedHeight.IsTyping || !TryGetShape(out var min, out _, out _, out _))
                return;

            var worldStart = rectangle.ToWorld(min);
            var worldUp = rectangle.Plane.FrameToWorld.MultiplyVector(Vector3.up).normalized;
            var translation = HandleUtility.CalcLineTranslation(heightMouseStart, mousePosition, worldStart, worldUp);
            // Шаг — от начальной высоты, а не от нуля: высота этажа 3,5 при первом движении не превращается в 3,6.
            chosenHeight = Mathf.Max(StandardStepHeight, heightAtStart + Mathf.Round(translation / StandardStepHeight) * StandardStepHeight);
        }


        // Tab во время протягивания: на четверть оборота по часовой, если смотреть сверху.
        public void RotateDirection()
        {
            lockedDirection = BoxModelStairsTurns.Clockwise(CurrentDirection);
        }


        // Кладёт лестницу и выделяет её. Не протянул — ничего.
        public void Commit()
        {
            if (!TryGetShape(out var min, out var direction, out var width, out var length))
            {
                Cancel();
                return;
            }

            Undo.SetCurrentGroupName("Create Stairs");
            var height = StairsHeight;
            var isFirstFlightOnOtherSide = IsStartOnFarSideAcross(min, direction, width);
            var landingDepth = IsSwitchback ? FitSwitchbackIntoWell(ref min, direction, width, ref length, height) : 0f;
            var stepDepth = StepDepthFor(height, length);
            BoxModelStairs stairs;
            BoxModel createdIn;

            if (rectangle.BuildingContext != null)
            {
                stairs = CreateInBuilding(rectangle.BuildingContext, min, direction, width, height, stepDepth);
                createdIn = rectangle.BuildingContext.Model;
            }
            else
            {
                createdIn = rectangle.TargetModel;

                if (createdIn == null)
                {
                    createdIn = BoxModelFactory.CreateModel(rectangle.ToWorld(min));
                    min = Vector3.zero;
                }

                stairs = BoxModelFactory.CreateStairs(createdIn, min, direction, width, height, stepDepth,
                    LiveBuilderStairsSettings.CutsAbove && rectangle.TargetModel != null);

                if (rectangle.SurfaceBuilding != null)
                    AttachToBuilding(stairs, rectangle.SurfaceBuilding);
            }

            if (stairs != null)
            {
                if (IsSwitchback)
                    MakeSwitchback(stairs, length, landingDepth, isFirstFlightOnOtherSide);

                Selection.activeGameObject = stairs.gameObject;
                RebuildModelsOf(stairs, createdIn);
            }

            Cancel();
        }


        public void Cancel()
        {
            rectangle.Cancel();
            typedHeight.Clear();
            lockedDirection = null;
            isChoosingHeight = false;
        }

        // В здании: лестница в его объекте; «И этажи выше» — повтором до верхнего этажа. Высота во весь этаж следит за зданием, своя — остаётся.
        // Проём в полу — «Перекрытие над» (сразу включено). У здания нет объекта из коробок — null.
        private static BoxModelStairs CreateInBuilding(LiveBuilderBuildingContext context, Vector3 buildingLocalMin, BoxModelStairsDirection direction, float width,
            float height, float stepDepth)
        {
            var model = context.Model;

            if (model == null)
                return null;

            var building = context.Building;
            var localMin = model.transform.InverseTransformPoint(building.transform.TransformPoint(buildingLocalMin));
            var stairs = BoxModelFactory.CreateStairs(model, localMin, direction, width, height, stepDepth, LiveBuilderStairsSettings.CutsAbove);
            AttachToBuilding(stairs, building);
            return stairs;
        }

        // Два марша с площадкой: форма, площадка, зазор, сторона первого марша — и глубина ступени, чтобы всё легло в длину.
        private static void MakeSwitchback(BoxModelStairs stairs, float length, float landingDepth, bool isFirstFlightOnOtherSide)
        {
            stairs.Shape = BoxModelStairsShape.Switchback;
            stairs.LandingDepth = landingDepth;
            stairs.FlightGap = StandardFlightGap;
            stairs.IsFirstFlightOnOtherSide = isFirstFlightOnOtherSide;
            stairs.StepDepth = stairs.StepDepthForLength(length);
        }

        // Лестница — часть здания: в его интерьер (или выделенную группу) и к его этажам; «И этажи выше» — копии до предпоследнего этажа.
        private static void AttachToBuilding(BoxModelStairs stairs, SplineBuilding building)
        {
            Undo.SetTransformParent(stairs.transform, BoxModelFactory.InteriorTargetOf(building), true, "Create Stairs");

            // В группе, привязанной к этажам, лестницу повторяет группа — своя привязка перемножила бы копии.
            if (!BoxModelShapeCollector.IsPieceInsideStoreyCopies(stairs.transform))
                SplineBuildingLinkSync.Attach(stairs.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys, true);
        }

        // Сразу, а не после паузы: объект, где лестницу создали, и тот, куда она переехала.
        private static void RebuildModelsOf(BoxModelStairs stairs, BoxModel createdIn)
        {
            BoxModelRebuilder.Rebuild(createdIn);
            var ownerModel = stairs.GetComponentInParent<BoxModel>();

            if (ownerModel != createdIn)
                BoxModelRebuilder.Rebuild(ownerModel);
        }


        // ── Форма ────────────────────────────────────────────────────────

        // Угол лестницы, направление, ширина и длина — по протянутому прямоугольнику.
        private bool TryGetShape(out Vector3 min, out BoxModelStairsDirection direction, out float width, out float length)
        {
            var delta = rectangle.End - rectangle.Start;
            direction = CurrentDirection;
            var runAxis = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.NegativeX ? 0 : 2;
            var crossAxis = runAxis == 0 ? 2 : 0;
            var runsTowardsPlus = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ;

            width = Mathf.Abs(delta[crossAxis]);
            length = Mathf.Abs(delta[runAxis]);
            min = rectangle.Start;

            if (width < MinDrawnWidth)
            {
                width = IsSwitchback ? 2f * LiveBuilderStairsSettings.DefaultWidth + StandardFlightGap : LiveBuilderStairsSettings.DefaultWidth;
                min[crossAxis] = rectangle.Start[crossAxis] - width / 2f;
            }
            else
            {
                min[crossAxis] = Mathf.Min(rectangle.Start[crossAxis], rectangle.End[crossAxis]);
            }

            if (length < MinDrawnLength)
            {
                length = StandardLengthFor(StairsHeight);
                min[runAxis] = runsTowardsPlus ? rectangle.Start[runAxis] : rectangle.Start[runAxis] - length;
            }
            else
            {
                min[runAxis] = Mathf.Min(rectangle.Start[runAxis], rectangle.End[runAxis]);
            }

            return lockedDirection != null || delta.magnitude > 0.01f;
        }

        // Ось и знак, куда тянут сильнее.
        private static BoxModelStairsDirection DirectionOf(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
                return delta.x >= 0f ? BoxModelStairsDirection.PositiveX : BoxModelStairsDirection.NegativeX;

            return delta.z >= 0f ? BoxModelStairsDirection.PositiveZ : BoxModelStairsDirection.NegativeZ;
        }

        private static string DirectionName(BoxModelStairsDirection direction)
        {
            return direction switch
            {
                BoxModelStairsDirection.PositiveX => "+X",
                BoxModelStairsDirection.NegativeX => "−X",
                BoxModelStairsDirection.PositiveZ => "+Z",
                _ => "−Z"
            };
        }

        // Длина лестницы на эту высоту, если её не протянули: ступени по метрикам; у лестницы с площадкой — длинный марш и площадка.
        private static float StandardLengthFor(float height)
        {
            return IsSwitchback
                ? LongestFlightStepsFor(height) * StandardStepDepth + Mathf.Max(MinLandingDepth, LiveBuilderStairsSettings.DefaultWidth)
                : StepCountFor(height) * StandardStepDepth;
        }

        // Глубина ступени, чтобы ступени прямой лестницы легли ровно в протянутую длину.
        private static float StepDepthFor(float height, float length)
        {
            return Mathf.Max(BoxModelStairs.MinStepDepth, length / StepCountFor(height));
        }

        // Ступеней на высоту по метрикам; у лестницы с площадкой — не меньше двух, по одной на марш.
        private static int StepCountFor(float height)
        {
            return Mathf.Max(IsSwitchback ? 2 : 1, Mathf.RoundToInt(height / StandardStepHeight));
        }

        // Ступеней в длинном марше лестницы с площадкой: в первом половина, во втором остальное.
        private static int LongestFlightStepsFor(float height)
        {
            var stepCount = StepCountFor(height);
            return stepCount - stepCount / 2;
        }


        // Лестница с площадкой в протянутую клетку: марши по метрикам и площадка не мельче ширины марша — у дальнего конца,
        // остаток у ближнего — этажная площадка (min сдвигается, length — только лестница). Клетка короче — вся под лестницу, площадка мельче, ступени тоже.
        // Возвращает глубину площадки.
        private static float FitSwitchbackIntoWell(ref Vector3 min, BoxModelStairsDirection direction, float width, ref float length, float height)
        {
            var flightWidth = Mathf.Max(0.25f, (width - StandardFlightGap) / 2f);
            var landingDepth = Mathf.Max(MinLandingDepth, flightWidth);
            var stairsLength = LongestFlightStepsFor(height) * StandardStepDepth + landingDepth;

            if (length < stairsLength)
                return Mathf.Min(landingDepth, length * 0.4f);

            if (IsTowardsPlus(direction))
                min[RunAxisOf(direction)] += length - stairsLength;

            length = stairsLength;
            return landingDepth;
        }

        // Тянуть начали у дальней поперёк подъёма стороны — первый марш там.
        private bool IsStartOnFarSideAcross(Vector3 min, BoxModelStairsDirection direction, float width)
        {
            var crossAxis = RunAxisOf(direction) == 0 ? 2 : 0;
            return rectangle.Start[crossAxis] > min[crossAxis] + width / 2f + 0.001f;
        }

        private static int RunAxisOf(BoxModelStairsDirection direction)
        {
            return direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.NegativeX ? 0 : 2;
        }

        private static bool IsTowardsPlus(BoxModelStairsDirection direction)
        {
            return direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Габарит будущей лестницы, профиль ступеней сбоку и надпись.
        public void DrawPreview()
        {
            if (!rectangle.IsActive || !TryGetShape(out var min, out var direction, out var width, out var length))
                return;

            if (IsSwitchback)
            {
                DrawSwitchbackPreview(min, direction, width, length);
                return;
            }

            var height = StairsHeight;
            var runAxis = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.NegativeX ? 0 : 2;
            var size = new Vector3(width, height, width);
            size[runAxis] = length;

            using (new Handles.DrawingScope(rectangle.Plane.FrameToWorld))
            {
                Handles.color = BoxModelPartHandles.AddColor;
                Handles.DrawWireCube(min + size / 2f, size);

                var bottom = min;
                var top = min + size;
                bottom[runAxis] = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ ? min[runAxis] : min[runAxis] + length;
                top[runAxis] = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ ? min[runAxis] + length : min[runAxis];
                top[runAxis == 0 ? 2 : 0] = bottom[runAxis == 0 ? 2 : 0];
                Handles.DrawLine(bottom, top, 2f);
                DrawUpArrow(min, size, runAxis, direction);

                var stepCount = StepCountFor(height);
                LiveBuilderSceneText.DrawLabel(top, stepCount + " ступеней по " + LiveBuilderSceneText.FormatMeters(StepDepthFor(height, length)) + " м, " +
                                                    LiveBuilderSceneText.FormatMeters(length) + " × " +
                                                    LiveBuilderSceneText.FormatMeters(width) + " м, вверх на " + LiveBuilderSceneText.FormatMeters(height) + " м · Tab — повернуть");
            }
        }

        // Лестница с площадкой: клетка — плоской рамкой на полу, лестница — рамкой габарита, стрелки первого марша вверх и второго обратно, надпись.
        private void DrawSwitchbackPreview(Vector3 wellMin, BoxModelStairsDirection direction, float width, float wellLength)
        {
            var height = StairsHeight;
            var runAxis = RunAxisOf(direction);
            var crossAxis = runAxis == 0 ? 2 : 0;
            var stairsMin = wellMin;
            var stairsLength = wellLength;
            var landingDepth = FitSwitchbackIntoWell(ref stairsMin, direction, width, ref stairsLength, height);
            var flightWidth = Mathf.Max(0.25f, (width - StandardFlightGap) / 2f);
            var isFirstOnOtherSide = IsStartOnFarSideAcross(wellMin, direction, width);

            var wellSize = new Vector3(width, 0.02f, width);
            wellSize[runAxis] = wellLength;
            var stairsSize = new Vector3(width, height, width);
            stairsSize[runAxis] = stairsLength;

            // Марши — у ближнего к началу края лестницы, площадка — у дальнего.
            var flightsMin = stairsMin;
            var flightSize = stairsSize;
            flightSize[runAxis] = stairsLength - landingDepth;
            flightSize[crossAxis] = flightWidth;

            if (!IsTowardsPlus(direction))
                flightsMin[runAxis] += landingDepth;

            var firstMin = flightsMin;
            var secondMin = flightsMin;
            firstMin[crossAxis] += isFirstOnOtherSide ? width - flightWidth : 0f;
            secondMin[crossAxis] += isFirstOnOtherSide ? 0f : width - flightWidth;

            using (new Handles.DrawingScope(rectangle.Plane.FrameToWorld))
            {
                Handles.color = BoxModelPartHandles.AddColor;
                Handles.DrawWireCube(wellMin + wellSize / 2f, wellSize);
                Handles.DrawWireCube(stairsMin + stairsSize / 2f, stairsSize);
                DrawUpArrow(firstMin, flightSize, runAxis, direction);
                DrawUpArrow(secondMin, flightSize, runAxis, BoxModelStairsTurns.Opposite(direction));

                LiveBuilderSceneText.DrawLabel(stairsMin + stairsSize, StepCountFor(height) + " ступеней в двух маршах, площадка " + LiveBuilderSceneText.FormatMeters(landingDepth) +
                                                                     " м, вверх на " + LiveBuilderSceneText.FormatMeters(height) + " м · Tab — повернуть");
            }
        }

        // Толстая стрелка на полу по середине марша: от нижней ступени к верхней.
        private static void DrawUpArrow(Vector3 min, Vector3 size, int runAxis, BoxModelStairsDirection direction)
        {
            var crossAxis = runAxis == 0 ? 2 : 0;
            var isPlus = direction is BoxModelStairsDirection.PositiveX or BoxModelStairsDirection.PositiveZ;
            var from = min + Vector3.up * 0.02f;
            from[crossAxis] += size[crossAxis] / 2f;
            var to = from;
            from[runAxis] = isPlus ? min[runAxis] + 0.2f : min[runAxis] + size[runAxis] - 0.2f;
            to[runAxis] = isPlus ? min[runAxis] + size[runAxis] - 0.2f : min[runAxis] + 0.2f;

            var forward = (to - from).normalized;
            var side = Vector3.Cross(Vector3.up, forward) * Mathf.Min(0.6f, size[crossAxis] * 0.3f);
            var headBase = to - forward * Mathf.Min(1f, size[runAxis] * 0.3f);

            Handles.DrawLine(from, headBase, 6f);
            Handles.DrawAAConvexPolygon(to, headBase + side, headBase - side);
        }
    }
}
