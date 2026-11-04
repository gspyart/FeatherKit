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
    /// Перекрытие не пробивает: проём над лестницей ставят вручную вырезающей коробкой.
    /// Лестница встаёт в объект, на чьём полу начали, или в выделенный; иначе — новым объектом (его можно повернуть на любой угол).
    /// В здании по контуру — в его объект, высота сначала — его этаж; «этот и все выше» — лестничная клетка до верхнего этажа.
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

        private readonly LiveBuilderFloorRectangle rectangle = new();
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

        public string Hint => isChoosingHeight
            ? "Мышь вверх-вниз — высота: " + LiveBuilderSceneText.FormatMeters(chosenHeight) + " м (этаж — " + LiveBuilderSceneText.FormatMeters(StoreyStairsHeight) +
              "). Клик или Enter — готово, Tab — повернуть, Esc — отмена"
            : "Вверх — " + DirectionName(CurrentDirection) + ". Тяни прямоугольник — длина и ширина лестницы. Tab — повернуть. Отпусти — дальше высота. Esc — отмена";

        // Куда пойдёт вверх: запомненное, а пока не запомнилось — куда тянут сильнее.
        private BoxModelStairsDirection CurrentDirection => lockedDirection ?? DirectionOf(rectangle.End - rectangle.Start);


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


        // Мышь вверх-вниз от точки, где отпустил, — высота по подъёму ступени.
        public void UpdateHeight(Vector2 mousePosition)
        {
            if (!IsChoosingHeight || !TryGetShape(out var min, out _, out _, out _))
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
            var stepDepth = StepDepthFor(StairsHeight, length);

            if (rectangle.BuildingContext != null)
            {
                CommitInBuilding(rectangle.BuildingContext, min, direction, width, StairsHeight, stepDepth);
                Cancel();
                return;
            }

            var model = rectangle.TargetModel;

            if (model == null)
            {
                model = BoxModelFactory.CreateModel(rectangle.ToWorld(min));
                min = Vector3.zero;
            }

            var stairs = BoxModelFactory.CreateStairs(model, min, direction, width, StairsHeight, stepDepth,
                LiveBuilderStairsSettings.CutsAbove && rectangle.TargetModel != null);
            Selection.activeGameObject = stairs.gameObject;
            BoxModelRebuilder.Rebuild(model);
            Cancel();
        }


        public void Cancel()
        {
            rectangle.Cancel();
            lockedDirection = null;
            isChoosingHeight = false;
        }

        // В здании: лестница в его объекте; «все выше» — повтором до верхнего этажа. Высота во весь этаж следит за зданием, своя — остаётся. Проём в полу — вручную.
        private static void CommitInBuilding(LiveBuilderBuildingContext context, Vector3 buildingLocalMin, BoxModelStairsDirection direction, float width,
            float height, float stepDepth)
        {
            var model = context.Model;

            if (model == null)
                return;

            var building = context.Building;
            var localMin = model.transform.InverseTransformPoint(building.transform.TransformPoint(buildingLocalMin));
            var stairs = BoxModelFactory.CreateStairs(model, localMin, direction, width, height, stepDepth, LiveBuilderStairsSettings.CutsAbove);
            Undo.SetTransformParent(stairs.transform, BoxModelFactory.InteriorOf(building), true, "Create Stairs");
            SplineBuildingLinkSync.Attach(stairs.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys, true);
            Selection.activeGameObject = stairs.gameObject;

            BoxModelRebuilder.Rebuild(model);
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
                width = LiveBuilderStairsSettings.DefaultWidth;
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

        // Длина марша на эту высоту, если её не протянули: ступени по метрикам.
        private static float StandardLengthFor(float height)
        {
            return StepCountFor(height) * StandardStepDepth;
        }

        // Глубина ступени, чтобы ступени легли ровно в протянутую длину.
        private static float StepDepthFor(float height, float length)
        {
            return Mathf.Max(BoxModelStairs.MinStepDepth, length / StepCountFor(height));
        }

        private static int StepCountFor(float height)
        {
            return Mathf.Max(1, Mathf.RoundToInt(height / StandardStepHeight));
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Габарит будущей лестницы, профиль ступеней сбоку и надпись.
        public void DrawPreview()
        {
            if (!rectangle.IsActive || !TryGetShape(out var min, out var direction, out var width, out var length))
                return;

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
