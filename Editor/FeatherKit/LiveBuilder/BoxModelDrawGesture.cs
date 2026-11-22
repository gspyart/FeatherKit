using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Рисование коробки в два шага, как фигуры в ProBuilder: зажал и протянул прямоугольник на поверхности, отпустил —
    /// двигаешь мышь, задаёшь глубину, кликнул — готово. Всё прилипает к сетке, размеры подписаны. На шаге глубины числа с клавиатуры:
    /// «1,5» — глубина, «4×3» — стороны прямоугольника (глубина дальше мышью), «4×3×1,5» — всё сразу.
    /// Добавит коробка объём или вырежет дыру — по режиму <see cref="BoxModelDrawModeSetting"/>: в «Авто» наружу добавляет, внутрь вырезает.
    /// Рисуешь на объекте из коробок — коробка в нём; на прочей геометрии или в пустоте — в выделенном объекте или в новом.
    /// Новый объект на полу здания встаёт в его интерьер и привязывается к этажу целиком — вложенный объект (шахта, лестничная клетка).
    /// Коробка на коробке, которая уже часть здания (площадка, балконная плита), — тоже его часть: в интерьер и к этажам, как она.
    /// Коробка или новый объект на крыше здания — его часть, привязанная к крыше: добавил этажей — поднимется с ней.
    /// Вырезаешь в стене по линии или здании — встаёт вырез (<see cref="GeometryCutter"/>) в них, как у «Cut»: едет за их точками и этажами.
    /// Форма (<see cref="LiveBuilderBoxShape"/>): пандус — коробка с полным скосом (<see cref="BoxModelRamps"/>), колонна — отдельный объект в той же коробке.
    /// </summary>
    public sealed class BoxModelDrawGesture
    {
        private enum DrawStage
        {
            Idle,
            Rectangle,
            Depth
        }


        private DrawStage stage = DrawStage.Idle;
        private BoxModelDrawPlane plane;

        // Рисуем новый объект: коробка всегда добавляет, в какую сторону ни тяни.
        private bool isCreatingNewModel;

        // Что встанет: коробка, пандус или колонна; пандус и колонна всегда добавляют. Tab у пандуса — четверть оборота подъёма.
        private LiveBuilderBoxShape shape;
        private int rampQuarterTurns;

        // Стена по линии или здание, на котором начат прямоугольник: вырез в них — вырез-объект, а не коробка объекта вокруг. Иначе — null.
        private Component pathOwner;

        // Здание, частью которого стала коробка, на которой начат прямоугольник (площадка, балкон). Иначе — null.
        private SplineBuilding surfaceBuilding;

        private Vector3 rectangleStart;
        private Vector3 rectangleEnd;
        private Vector2 depthMouseStart;
        private float depth;

        // Готовый вид объекта с новой коробкой, пока задаёшь глубину.
        private readonly BoxModelPreview preview = new();

        // Точные размеры с клавиатуры, как в SketchUp: «1,5» — глубина, «4×3» — стороны, «4×3×1,5» — всё.
        private readonly LiveBuilderTypedLength typedSize = new(3);
        private readonly List<float> typedValues = new();

        // Стороны набраны и уже в прямоугольнике — в панели они с замком, мышь их не меняет.
        private bool areSidesTyped;


        public bool IsActive => stage != DrawStage.Idle;
        public bool IsChoosingDepth => stage == DrawStage.Depth;

        // Только что поставленная коробка, колонна или вырез; ничего — null.
        public Component LastCreated { get; private set; }

        // Что делать дальше — для строки-подсказки внизу окна сцены; у пандуса — куда он поднимается и Tab.
        public string Hint => shape == LiveBuilderBoxShape.Ramp && stage != DrawStage.Idle
            ? "Ramp rises toward " + BoxModelRamps.SideName(RampRiseFace()) + " " + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Rotate) + " turn · " + StageHint
            : StageHint;

        private static string TimesKey => LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Times);

        private string StageHint => stage switch
        {
            DrawStage.Rectangle when typedSize.IsTyping => "Typed " + typedSize.Text + " m " + TimesKey + " second side " + LiveBuilderHintKeys.Key("release") +
                                                           " exactly this " + LiveBuilderHintKeys.Esc + " clear typing",
            DrawStage.Rectangle => "Drag — rectangle, release — then depth " + LiveBuilderHintKeys.Key("4 X 3") + " exact " + LiveBuilderHintKeys.Esc + " cancel",
            DrawStage.Depth when typedSize.IsTyping => "Typed " + typedSize.Text + " m — depth; two numbers — sides, three — all " + TimesKey + " next number " +
                                                       LiveBuilderHintKeys.Enter + " apply " + LiveBuilderHintKeys.Esc + " clear typing",
            DrawStage.Depth => (BoxModelDrawModeSetting.Current == BoxModelDrawMode.Auto && !isCreatingNewModel
                                   ? "Move the mouse — depth: outward adds, inward cuts, click — done "
                                   : "Move the mouse — depth, click — done ") +
                               LiveBuilderHintKeys.Key("digits") + " exact " + LiveBuilderHintKeys.Enter + " done " + LiveBuilderHintKeys.Esc + " cancel",
            _ => string.Empty
        };

        // Что рисуется — в секции Drawing панели.
        private string ShapeName => shape switch
        {
            LiveBuilderBoxShape.Ramp => "Ramp",
            LiveBuilderBoxShape.Column => "Column",
            _ => "Box"
        };

        // Объект, стену или здание, на которых начат прямоугольник, удалили посреди жеста: его числа — в осях уже несуществующего объекта.
        private bool IsTargetLost => (plane.Model is not null && plane.Model == null) || (pathOwner is not null && pathOwner == null);


        // ── Шаги жеста ───────────────────────────────────────────────────

        // Начать прямоугольник там, куда нажали: на поверхности объекта из коробок, на прочей геометрии или в пустоте.
        // Для нового объекта коробка не встаёт в тот, на котором рисуешь, а станет отдельным объектом.
        public bool TryBegin(Ray worldRay, bool isForNewModel, LiveBuilderBoxShape drawnShape = LiveBuilderBoxShape.Box)
        {
            if (!BoxModelDrawPlaneFinder.TryFind(worldRay, isForNewModel, out plane, out rectangleStart))
                return false;

            isCreatingNewModel = isForNewModel;
            shape = drawnShape;
            rampQuarterTurns = 0;
            pathOwner = isForNewModel ? null : PathOwnerUnder(worldRay);
            surfaceBuilding = isForNewModel ? null : SurfaceBuildingUnder(worldRay);
            rectangleEnd = rectangleStart;
            typedSize.Clear();
            areSidesTyped = false;
            stage = DrawStage.Rectangle;
            return true;
        }

        // Ближайшее под мышью — стена по линии или здание, а не объект из коробок.
        private static Component PathOwnerUnder(Ray worldRay)
        {
            var hit = LiveBuilderSceneHit.Find(worldRay);
            return hit.IsPathOwnerCloser ? hit.PathOwner : null;
        }

        // Коробка под мышью — часть здания: новая на ней — тоже.
        private static SplineBuilding SurfaceBuildingUnder(Ray worldRay)
        {
            return BoxModelPicker.TryPickSurface(worldRay, out var hit) ? LiveBuilderBuildingContext.BuildingOfSurface(hit) : null;
        }


        public void UpdateRectangle(Ray worldRay)
        {
            if (stage == DrawStage.Rectangle && plane.TryIntersect(worldRay, out var localPoint))
                rectangleEnd = plane.SnapOnPlane(localPoint);

            ApplyTypedSides();
        }


        // Прямоугольник готов — дальше глубина. Пустой прямоугольник — жест отменяется.
        public bool TryFinishRectangle(Vector2 mousePosition)
        {
            ApplyTypedSides();

            if (stage != DrawStage.Rectangle || !HasArea())
            {
                Cancel();
                return false;
            }

            // Набраны ровно стороны — они уже в прямоугольнике; одно или три числа ждут Enter на шаге глубины.
            if (typedSize.TryPeekAll(typedValues) && typedValues.Count == 2)
            {
                typedSize.Clear();
                areSidesTyped = true;
            }

            depthMouseStart = mousePosition;
            depth = 0f;
            stage = DrawStage.Depth;
            return true;
        }


        // Пока выбираешь глубину: цифры — точные размеры, Enter — применить набранное (два числа — стороны, глубина дальше мышью; одно или три — готово)
        // или готово как есть, Esc при наборе — стереть набор. Возвращает, забрал ли клавишу; committed — коробка встала.
        public bool HandleKey(KeyCode key, out bool committed)
        {
            committed = false;

            if (stage == DrawStage.Rectangle)
                return HandleRectangleKey(key);

            if (stage != DrawStage.Depth)
                return false;

            if (typedSize.TryType(key))
                return true;

            if (key == KeyCode.Escape && typedSize.IsTyping)
            {
                typedSize.Clear();
                return true;
            }

            if (key is not (KeyCode.Return or KeyCode.KeypadEnter))
                return false;

            if (typedSize.IsTyping)
            {
                // Набрано не то — набор стёрт, коробка ждёт дальше.
                if (!typedSize.TryTakeAll(typedValues))
                    return true;

                if (typedValues.Count >= 2)
                {
                    ResizeRectangle(typedValues[0], typedValues[1]);
                    areSidesTyped = true;
                }

                if (typedValues.Count != 2)
                    SetTypedDepth(typedValues[typedValues.Count - 1]);

                RebuildPreview();

                if (typedValues.Count == 2)
                    return true;
            }

            committed = Commit();
            return true;
        }

        // Пока тянешь прямоугольник: цифры и X — его стороны прямо на ходу, Backspace — стереть, Esc при наборе — стереть набор.
        private bool HandleRectangleKey(KeyCode key)
        {
            if (typedSize.TryType(key))
            {
                ApplyTypedSides();
                return true;
            }

            if (key != KeyCode.Escape || !typedSize.IsTyping)
                return false;

            typedSize.Clear();
            return true;
        }

        // Набраны хотя бы две стороны — прямоугольник ровно такой, в сторону мыши.
        private void ApplyTypedSides()
        {
            if (stage == DrawStage.Rectangle && typedSize.TryPeekAll(typedValues) && typedValues.Count >= 2)
                ResizeRectangle(typedValues[0], typedValues[1]);
        }

        // Стороны прямоугольника — набранные, от угла, где начали тянуть, в ту же сторону. Порядок — как в подписи «2 × 3».
        private void ResizeRectangle(float firstSize, float secondSize)
        {
            GetSideAxes(out var firstAxis, out var secondAxis);
            rectangleEnd[firstAxis] = rectangleStart[firstAxis] + Mathf.Sign(rectangleEnd[firstAxis] - rectangleStart[firstAxis]) * firstSize;
            rectangleEnd[secondAxis] = rectangleStart[secondAxis] + Mathf.Sign(rectangleEnd[secondAxis] - rectangleStart[secondAxis]) * secondSize;
        }

        // Внутрь уже тянули — набранная глубина тоже внутрь.
        private void SetTypedDepth(float meters)
        {
            depth = depth < 0f ? -meters : meters;
        }


        public void UpdateDepth(Vector2 mousePosition)
        {
            if (stage != DrawStage.Depth || typedSize.IsTyping)
                return;

            if (IsTargetLost)
            {
                Cancel();
                return;
            }

            var worldCenter = plane.FrameToWorld.MultiplyPoint3x4((rectangleStart + rectangleEnd) / 2f);
            var worldOutward = plane.FrameToWorld.MultiplyVector(plane.OutwardDirection).normalized;
            var translation = HandleUtility.CalcLineTranslation(depthMouseStart, mousePosition, worldCenter, worldOutward);
            var newDepth = LiveBuilderGrid.Snap(translation, plane.Axis);

            if (Mathf.Approximately(newDepth, depth))
                return;

            depth = newDepth;
            RebuildPreview();
        }


        // Пандус поднимается в другую сторону — на четверть оборота по часовой. Рисуется не пандус — false.
        public bool TurnRamp()
        {
            if (shape != LiveBuilderBoxShape.Ramp || stage == DrawStage.Idle)
                return false;

            rampQuarterTurns++;
            return true;
        }

        // Куда поднимается рисуемый пандус: вдоль длинной стороны к концу протягивания и повороты Tab.
        private int RampRiseFace()
        {
            return BoxModelRamps.RiseFaceOf(rectangleEnd - rectangleStart, plane.Axis, rampQuarterTurns);
        }


        // Кладёт коробку, пандус, колонну или вырез и выделяет. Глубина ноль или вырезать нечего — ничего не делает и возвращает false.
        public bool Commit()
        {
            var mode = CurrentPartMode();
            var model = plane.Model;
            LastCreated = null;
            var isCuttingPathWall = mode == BoxModelPartMode.Cut && pathOwner != null;

            if (stage != DrawStage.Depth || IsTargetLost || Mathf.Approximately(depth, 0f) || (mode == BoxModelPartMode.Cut && model == null && !isCuttingPathWall))
            {
                Cancel();
                return false;
            }

            GetBox(out var min, out var max);

            if (isCuttingPathWall)
            {
                LastCreated = CreateCutterInPathWall(min, max);
                Selection.activeGameObject = LastCreated.gameObject;
                Cancel();
                return true;
            }

            if (shape == LiveBuilderBoxShape.Column)
            {
                LastCreated = CreateColumn(model, min, max);
                Selection.activeGameObject = LastCreated.gameObject;
                Cancel();
                return true;
            }

            var isNewModel = model == null;

            if (isNewModel)
            {
                model = BoxModelFactory.CreateModel(min);
                max -= min;
                min = Vector3.zero;
            }

            // Вырез — вырез-коробка: режет свой объект точно, а стены и здания вокруг — по своему правилу.
            var piece = mode == BoxModelPartMode.Add
                ? BoxModelFactory.CreatePart(model, min, max, mode).transform
                : BoxModelFactory.CreateCutterBox(model, min, max).transform;
            var localMin = Vector3.Min(min, max);
            var size = Vector3.Max(min, max) - localMin;

            if (shape == LiveBuilderBoxShape.Ramp && piece.TryGetComponent<BoxModelPart>(out var rampPart))
                BoxModelRamps.MakeRamp(rampPart, RampRiseFace());

            if (isNewModel)
                AttachModelToBuilding(model.transform, model.transform.TransformPoint(localMin + size / 2f), BottomCenterOf(model.transform, localMin, size));
            else
                AttachToBuilding(model, piece, localMin, size, pathOwner as SplineBuilding, surfaceBuilding);

            LastCreated = piece;
            Selection.activeGameObject = piece.gameObject;

            // Сразу, а не после паузы пересборки: иначе после предпросмотра на миг мелькнёт старый вид.
            BoxModelRebuilder.Rebuild(model, LiveBuilderBuildQuality.Draft);
            Cancel();
            return true;
        }

        // Коробка жеста — вырез той же формы в стене или здании: лицом — прямоугольник, глубина — по нормали поверхности, опора — низ посередине.
        private GeometryCutter CreateCutterInPathWall(Vector3 min, Vector3 max)
        {
            var frame = plane.FrameToWorld;
            var depthAxis = plane.Axis;
            var upAxis = depthAxis == 1 ? 2 : 1;
            var widthAxis = 3 - depthAxis - upAxis;
            var forward = ((Vector3)frame.GetColumn(depthAxis)).normalized;
            var up = ((Vector3)frame.GetColumn(upAxis)).normalized;

            var size = max - min;
            var width = size[widthAxis] * ((Vector3)frame.GetColumn(widthAxis)).magnitude;
            var height = size[upAxis] * ((Vector3)frame.GetColumn(upAxis)).magnitude;
            var cutDepth = size[depthAxis] * ((Vector3)frame.GetColumn(depthAxis)).magnitude;
            var bottom = frame.MultiplyPoint3x4((min + max) / 2f) - up * (height / 2f);

            var cutter = BoxModelFactory.CreateCutter(pathOwner.transform, bottom, Quaternion.LookRotation(forward, up), new Vector2(width, height), cutDepth);
            LiveBuilderCutterGesture.AttachToOwner(cutter, forward);

            if (pathOwner is SplineBuilding building)
                SplineBuildingRebuilder.Rebuild(building, LiveBuilderBuildQuality.Draft);
            else
                SplineWallRebuilder.Rebuild((SplineWall)pathOwner, LiveBuilderBuildQuality.Draft);

            return cutter;
        }


        // Коробка в здании — его часть, как перегородка: в его этаж. Коробка снаружи на стене здания (балкон, козырёк) — тоже, если по высоте
        // она в здании; и коробка на коробке, которая уже часть здания (ограждение на плите). С «И этажи выше» — повтор до верха здания.
        // Коробка на крыше здания (будка выхода, вентиляция) — в «Roof»: едет за крышей, когда меняются этажи.
        private static void AttachToBuilding(BoxModel model, Transform piece, Vector3 localMin, Vector3 size, SplineBuilding wallBuilding,
            SplineBuilding surfaceBuilding)
        {
            var center = model.transform.TransformPoint(localMin + size / 2f);
            var building = LiveBuilderBuildingContext.BuildingContaining(model, center);

            if (building == null && IsOnOuterWallOf(wallBuilding, model, center))
                building = wallBuilding;

            if (building == null && surfaceBuilding != null && SplineWallRebuilder.OuterModelOf(surfaceBuilding) == model)
                building = surfaceBuilding;

            if (building == null)
            {
                AttachToRoof(piece, LiveBuilderBuildingContext.BuildingWithRoofAt(model, BottomCenterOf(model.transform, localMin, size)));
                return;
            }

            SplineBuildingInteriorPlacement.PutOnStorey(piece, building, LiveBuilderBuildingContext.IsOnAllStoreys, false, "Create Box");
        }

        // Коробку рисовали на стене здания этого же объекта, и её середина по высоте в здании: не на крыше и не под ним.
        private static bool IsOnOuterWallOf(SplineBuilding wallBuilding, BoxModel model, Vector3 worldCenter)
        {
            return wallBuilding != null && SplineWallRebuilder.OuterModelOf(wallBuilding) == model
                                        && LiveBuilderBuildingContext.IsWithinHeight(wallBuilding, worldCenter);
        }


        // Колонна в коробке жеста: в объекте, на котором рисовали, — его ребёнком и, как коробка, в здании; в пустоте — отдельно, как новый объект.
        private Component CreateColumn(BoxModel model, Vector3 min, Vector3 max)
        {
            var column = LiveBuilderColumnFactory.CreateInBox(model, plane.FrameToWorld, min, max);
            var size = column.Size;

            if (model == null)
                AttachModelToBuilding(column.transform, column.transform.TransformPoint(size / 2f), column.transform.TransformPoint(new Vector3(size.x / 2f, 0f, size.z / 2f)));
            else
                AttachToBuilding(model, column.transform, column.transform.localPosition, size, pathOwner as SplineBuilding, surfaceBuilding);

            LiveBuilderColumnRebuilder.Rebuild(column, LiveBuilderBuildQuality.Draft);
            return column;
        }


        // Новый объект на полу здания — вложенный объект в нём (шахта, лестничная клетка): в его этаж целиком;
        // с «И этажи выше» — сразу на всех этажах выше, одним мешем. Новый объект на крыше (чердак) — в «Roof».
        private static void AttachModelToBuilding(Transform node, Vector3 worldCenter, Vector3 worldBottomCenter)
        {
            var building = LiveBuilderBuildingContext.BuildingContaining(worldCenter);

            if (building == null)
            {
                AttachToRoof(node, LiveBuilderBuildingContext.BuildingWithRoofAt(worldBottomCenter));
                return;
            }

            SplineBuildingInteriorPlacement.PutOnStorey(node, building, LiveBuilderBuildingContext.IsOnAllStoreys, false, "Create Box Model");
        }


        public void Cancel()
        {
            preview.Clear();
            typedSize.Clear();
            areSidesTyped = false;
            stage = DrawStage.Idle;
            plane = default;
            isCreatingNewModel = false;
            pathOwner = null;
            surfaceBuilding = null;
            shape = LiveBuilderBoxShape.Box;
            rampQuarterTurns = 0;
        }


        // Стоит на крыше здания — в её «Roof». Здания нет — ничего.
        private static void AttachToRoof(Transform node, SplineBuilding roofBuilding)
        {
            if (roofBuilding != null)
                SplineBuildingInteriorPlacement.PutOnRoof(node, roofBuilding, "Create Box");
        }

        // Середина низа коробки в мире.
        private static Vector3 BottomCenterOf(Transform space, Vector3 localMin, Vector3 size)
        {
            return space.TransformPoint(localMin + new Vector3(size.x / 2f, 0f, size.z / 2f));
        }


        // ── Предпросмотр ─────────────────────────────────────────────────

        // Рамка жеста и размеры; сам объект с новой коробкой показывает BoxModelPreview.
        public void DrawPreview()
        {
            if (stage == DrawStage.Idle)
                return;

            using (new Handles.DrawingScope(plane.FrameToWorld))
            {
                if (stage == DrawStage.Rectangle)
                {
                    Handles.color = Color.cyan;
                    var size = rectangleEnd - rectangleStart;
                    Handles.DrawWireCube((rectangleStart + rectangleEnd) / 2f, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
                    LiveBuilderSceneText.DrawLabel(rectangleEnd, RectangleSizeText() + " m");
                    return;
                }

                GetBox(out var min, out var max);
                var mode = CurrentPartMode();
                Handles.color = mode == BoxModelPartMode.Add ? BoxModelPartHandles.AddColor : BoxModelPartHandles.CutColor;
                Handles.DrawWireCube((min + max) / 2f, max - min);
                DrawRampSlope(min, max);
                LiveBuilderSceneText.DrawLabel(max, DepthText(mode));
            }
        }

        // Пандус — линия ската: от низа стороны начала до верха стороны подъёма, посередине.
        private void DrawRampSlope(Vector3 min, Vector3 max)
        {
            if (shape != LiveBuilderBoxShape.Ramp)
                return;

            var riseFace = RampRiseFace();
            var axis = BoxFaces.Axis(riseFace);
            var low = (min + max) / 2f;
            var high = low;
            low[axis] = BoxFaces.IsPositive(riseFace) ? min[axis] : max[axis];
            high[axis] = BoxFaces.IsPositive(riseFace) ? max[axis] : min[axis];
            low.y = min.y;
            high.y = max.y;
            Handles.DrawLine(low, high, 3f);
        }


        // «2 × 3» — стороны прямоугольника по осям плоскости.
        private string RectangleSizeText()
        {
            GetSideAxes(out var firstAxis, out var secondAxis);
            var size = rectangleEnd - rectangleStart;
            return LiveBuilderSceneText.FormatMeters(size[firstAxis]) + " × " + LiveBuilderSceneText.FormatMeters(size[secondAxis]);
        }

        // «2 × 3 m, height 1,5 m — add»: у пола и потолка — высота, у стен — глубина.
        private string DepthText(BoxModelPartMode mode)
        {
            var depthName = plane.Axis == 1 ? "height" : "depth";
            var action = mode == BoxModelPartMode.Add ? "add" : plane.Model == null && pathOwner == null ? "nothing to cut" : "cut";
            if (typedSize.IsTyping)
                return "typed " + typedSize.Text + "… m — " + action;

            return RectangleSizeText() + " m, " + depthName + " " + LiveBuilderSceneText.FormatMeters(depth) + " m — " + action;
        }


        // Показывает объект с коробкой нужной глубины; глубина ноль — объект как есть. Вырез в стене по линии или здании и колонна — только рамкой.
        private void RebuildPreview()
        {
            var mode = CurrentPartMode();

            if (Mathf.Approximately(depth, 0f) || (mode == BoxModelPartMode.Cut && pathOwner != null) || shape == LiveBuilderBoxShape.Column)
            {
                preview.Hide();
                return;
            }

            GetBox(out var min, out var max);
            var box = MillimeterBox.FromMeters(min, max - min);
            preview.Show(plane.Model, mesh => BoxModelRebuilder.FillPreviewMesh(mesh, plane.Model, box, mode));
        }


        // ── Отчёт для панели ─────────────────────────────────────────────

        // Шаг, стороны и глубина для секции Drawing (<see cref="LiveBuilderDrawingReport"/>); числа из набора — вместо мышиных. Не рисуешь — null.
        public LiveBuilderDrawingReport Report()
        {
            if (stage == DrawStage.Idle)
                return null;

            GetSideAxes(out var firstAxis, out var secondAxis);
            var typedCount = typedSize.TryPeekAll(typedValues) ? typedValues.Count : 0;
            var hasTypedSides = typedCount >= 2;
            var hasTypedDepth = stage == DrawStage.Depth && typedCount is 1 or 3;
            var firstSide = hasTypedSides ? typedValues[0] : Mathf.Abs(rectangleEnd[firstAxis] - rectangleStart[firstAxis]);
            var secondSide = hasTypedSides ? typedValues[1] : Mathf.Abs(rectangleEnd[secondAxis] - rectangleStart[secondAxis]);
            var shownDepth = hasTypedDepth ? typedValues[typedCount - 1] : Mathf.Abs(depth);
            var depthName = plane.Axis == 1 ? "Height" : "Depth";

            var report = new LiveBuilderDrawingReport(ShapeName, stage == DrawStage.Rectangle ? 1 : 2, 2, stage == DrawStage.Rectangle ? "Rectangle" : depthName)
                .WithSize("Width", firstSide, hasTypedSides || areSidesTyped)
                .WithSize(plane.Axis == 1 ? "Length" : "Height", secondSide, hasTypedSides || areSidesTyped);

            if (stage == DrawStage.Depth)
                report.WithSize(depthName, shownDepth, hasTypedDepth);

            return report.WithTyped(typedSize.Text)
                .WithKeys(ReportEnterResult(typedCount, firstSide, secondSide, shownDepth, depthName),
                    typedSize.IsTyping ? "clear typing" : "drop the " + ShapeName.ToLowerInvariant());
        }

        // Пока тянешь прямоугольник, Enter ничего не делает; на глубине — применяет набранное или кладёт как есть.
        private string ReportEnterResult(int typedCount, float firstSide, float secondSide, float shownDepth, string depthName)
        {
            if (stage != DrawStage.Depth)
                return null;

            if (typedSize.IsTyping && (typedCount == 0 || typedSize.Text.EndsWith("×")))
                return "erase typing";

            if (typedCount == 2)
                return "sides " + LiveBuilderDrawingReport.FormatSizes(firstSide, secondSide) + ", " + depthName.ToLowerInvariant() + " by mouse";

            if (!typedSize.IsTyping && Mathf.Approximately(depth, 0f))
                return "nothing — " + depthName.ToLowerInvariant() + " is 0";

            var mode = CurrentPartMode();
            var action = mode == BoxModelPartMode.Add ? "adds" : plane.Model == null && pathOwner == null ? "nothing to cut" : "cuts";
            return ShapeName.ToLowerInvariant() + " " + LiveBuilderDrawingReport.FormatSizes(firstSide, secondSide, shownDepth) + " — " + action;
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Новый объект, пандус и колонна — всегда добавить; иначе — по режиму в окне Live Builder и направлению глубины.
        private BoxModelPartMode CurrentPartMode()
        {
            return isCreatingNewModel || shape != LiveBuilderBoxShape.Box ? BoxModelPartMode.Add : BoxModelDrawModeSetting.PartModeFor(depth);
        }

        // Коробка жеста в осях рамки: прямоугольник плюс глубина по нормали.
        private void GetBox(out Vector3 min, out Vector3 max)
        {
            var depthEnd = rectangleStart;
            depthEnd[plane.Axis] = plane.Coordinate + depth * plane.OutwardSign;
            min = Vector3.Min(Vector3.Min(rectangleStart, rectangleEnd), depthEnd);
            max = Vector3.Max(Vector3.Max(rectangleStart, rectangleEnd), depthEnd);
        }

        // Оси сторон прямоугольника по порядку, как в рамке выделения: сначала по горизонтали (X, у стены вдоль Z — Z), высота — последней.
        private void GetSideAxes(out int firstAxis, out int secondAxis)
        {
            (firstAxis, secondAxis) = plane.Axis switch
            {
                0 => (2, 1),
                1 => (0, 2),
                _ => (0, 1)
            };
        }

        private bool HasArea()
        {
            for (var axis = 0; axis < 3; axis++)
                if (axis != plane.Axis && Mathf.Approximately(rectangleStart[axis], rectangleEnd[axis]))
                    return false;

            return true;
        }
    }
}
