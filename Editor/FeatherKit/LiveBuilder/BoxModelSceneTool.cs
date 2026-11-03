using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Live Builder» в окне сцены (клавиша B). Что делает мышь — выбранный в окне Live Builder инструмент (<see cref="LiveBuilderTool"/>):
    /// - «Коробка» — протянул прямоугольник, мышью глубина, клик — коробка (<see cref="BoxModelDrawGesture"/>)
    /// - «Комната» и «Лестница» — протянул по полу, отпустил — готово (<see cref="LiveBuilderRoomGesture"/>, <see cref="LiveBuilderStairsGesture"/>)
    /// - «Стена» и «Здание» — кликами точки (<see cref="LiveBuilderPathGesture"/>), «Линейка» — два клика (<see cref="LiveBuilderRuler"/>)
    /// - «Проём» — клик по стене — проём выбранного размера (<see cref="LiveBuilderOpeningPlacer"/>), протянул по поверхности — своего (<see cref="LiveBuilderCutterGesture"/>)
    /// Клик без протягивания в «Коробке», «Комнате» и «Лестнице» — выделить то, что под мышью (<see cref="LiveBuilderClickSelection"/>): коробку, комнату, лестницу, стену, здание, проём.
    /// У выделенной коробки — ручки граней, у выделенных граней — стрелки. Внизу окна — подсказка, что делать сейчас.
    /// Клавиши: [ и ] — шаг сетки, PageUp и PageDown — этаж выше и ниже, Esc — отменить начатое.
    /// </summary>
    [EditorTool("Live Builder")]
    public class BoxModelSceneTool : EditorTool
    {
        // Сдвиг мыши, после которого нажатие — уже рисование, а не клик, пикс.
        private const float DragThresholdPixels = 4f;

        private const string IdleHint = "Протяни по поверхности — новая коробка. Клик по грани — выделить коробку";
        private const string FaceSelectionHint = "Клик — выделить грань, Shift — добавить или убрать. Стрелки — двигать выделенные грани. Протяни — новая коробка";
        private const string NewModelHint = "Новый объект: протяни прямоугольник на поверхности или земле. Esc — отмена";

        private readonly BoxModelDrawGesture drawGesture = new();
        private readonly LiveBuilderSurfaceBoxGesture surfaceBoxGesture = new();
        private readonly LiveBuilderRoomGesture roomGesture = new();
        private readonly LiveBuilderStairsGesture stairsGesture = new();
        private readonly LiveBuilderPathGesture wallGesture = new(LiveBuilderPathKind.Wall);
        private readonly LiveBuilderPathGesture buildingGesture = new(LiveBuilderPathKind.Building);
        private readonly LiveBuilderPathGesture roomContourGesture = new(LiveBuilderPathKind.Room);
        private readonly LiveBuilderOpeningPlacer openingPlacer = new();
        private readonly LiveBuilderCutterGesture cutterGesture = new();
        private readonly LiveBuilderRuler ruler = new();
        private readonly LiveBuilderContourHandles contourHandles = new();

        private Vector2 mouseDownPosition;
        private bool isMouseDown;
        private BoxModelSurfaceHit? hoveredSurface;

        // Здание по контуру под мышью — для подсказки: где сейчас строишь.
        private LiveBuilderBuildingContext hoveredBuildingContext;
        private (BoxModelDrawPlane Plane, Vector3 Point)? hoveredSnapPoint;


        // Подсказка поменялась — окно Live Builder показывает её у себя внизу.
        public static event System.Action HintChanged;


        // Кнопка «Новый»: следующая нарисованная коробка — отдельный новый объект, даже если рисуешь на другом объекте из коробок.
        public static bool IsNewModelRequested { get; set; }

        // Подсказка внизу окна сцены — что делать сейчас.
        public static string CurrentHintText { get; private set; } = "";

        public override GUIContent toolbarIcon => new(EditorGUIUtility.IconContent("PreMatCube").image, "Live Builder: коробки, комнаты, стены, проёмы, лестницы (B)");

        private static LiveBuilderTool CurrentTool => LiveBuilderToolSetting.Current;

        // Инструмент, где кликами ставят точки линии: стена, здание, комната по контуру. Иначе — null.
        private LiveBuilderPathGesture CurrentPathGesture => CurrentTool switch
        {
            LiveBuilderTool.Wall => wallGesture,
            LiveBuilderTool.Building => buildingGesture,
            LiveBuilderTool.Room when LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour => roomContourGesture,
            _ => null
        };

        // Точки контура выделенного можно тянуть везде, кроме инструментов, где клик ставит точку: там он начал бы новую линию, а не схватил старую.
        private bool CanEditContour => CurrentPathGesture == null && CurrentTool != LiveBuilderTool.Ruler;


        // ── Включение ────────────────────────────────────────────────────

        [Shortcut("FeatherKit/Live Builder", typeof(SceneView), KeyCode.B)]
        private static void Activate()
        {
            ToolManager.SetActiveTool<BoxModelSceneTool>();
        }


        public override void OnActivated()
        {
            LiveBuilderToolSetting.Changed += CancelAll;
            LiveBuilderRoomSettings.ShapeChanged += CancelAll;
        }


        public override void OnWillBeDeactivated()
        {
            LiveBuilderToolSetting.Changed -= CancelAll;
            LiveBuilderRoomSettings.ShapeChanged -= CancelAll;
            CancelAll();
            IsNewModelRequested = false;
        }

        // Всё начатое отменяется; начатая стена остаётся такой, как нарисована.
        private void CancelAll()
        {
            drawGesture.Cancel();
            surfaceBoxGesture.Cancel();
            roomGesture.Cancel();
            stairsGesture.Cancel();
            wallGesture.Finish();
            buildingGesture.Finish();
            roomContourGesture.Finish();
            cutterGesture.Cancel();
            ruler.Cancel();
            hoveredSurface = null;
            hoveredSnapPoint = null;
            isMouseDown = false;
        }


        // ── Окно сцены ───────────────────────────────────────────────────

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView)
                return;

            LiveBuilderSelectionHandles.Draw();

            if (CanEditContour)
                contourHandles.OnSceneGUI();

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;

            // Клавиши — напрямую, не через фильтр элемента: тот отбрасывает их, пока мышь держит другой элемент, и Tab терялся.
            if (current.type == EventType.KeyDown)
            {
                OnKeyDown(current);
                return;
            }

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseMove:
                    OnMouseMove(current, sceneView);
                    break;

                case EventType.MouseDown when current.button == 0 && !current.alt && HandleUtility.nearestControl == controlId:
                    OnMouseDown(current, controlId);
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    OnMouseDrag(current);
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    OnMouseUp(current);
                    break;

                case EventType.Repaint:
                    DrawHover(controlId);
                    DrawGesturePreviews();
                    PublishHint(CurrentHint());
                    BoxModelSceneText.DrawHint(sceneView, CurrentHintText);
                    break;
            }
        }

        private static void PublishHint(string hint)
        {
            if (hint == CurrentHintText)
                return;

            CurrentHintText = hint;
            HintChanged?.Invoke();
        }

        private string CurrentHint()
        {
            var storey = hoveredBuildingContext is { IsBuildingAlive: true } && CurrentTool != LiveBuilderTool.Building
                ? hoveredBuildingContext.DescriptionOf(CurrentTool == LiveBuilderTool.Stairs) + " · "
                : "Этаж " + LiveBuilderStoreys.CurrentStorey + (LiveBuilderStoreys.IsSectionEnabled ? ", разрез" : "") + " · ";

            // Здесь клик ставит новую точку — точки уже выделенного правят в «Коробке»: подсказать, где.
            if (CurrentPathGesture is { } pathGesture)
                return storey + pathGesture.Hint + (!pathGesture.IsActive && LiveBuilderContourHandles.SelectedOwner() != null
                    ? " · Точки выделенного — в «Коробке» (" + LiveBuilderToolSetting.HotkeyOf(LiveBuilderTool.Box) + ")"
                    : "");

            return storey + CurrentTool switch
            {
                LiveBuilderTool.Room => roomGesture.IsActive ? roomGesture.Hint : "Протяни по полу — комната. Клик — выделить",
                LiveBuilderTool.Stairs => stairsGesture.IsActive ? stairsGesture.Hint : "Протяни по полу прямоугольник лестницы, потом мышью высоту. Клик — выделить",
                LiveBuilderTool.Opening => cutterGesture.IsActive ? cutterGesture.Hint : openingPlacer.Hint,
                LiveBuilderTool.Ruler => ruler.Hint,
                _ => BoxHint()
            };
        }

        // Выделено здание, стена или комната по контуру — подсказка про его точки.
        private string BoxHint()
        {
            if (drawGesture.IsActive)
                return drawGesture.Hint;

            if (surfaceBoxGesture.IsActive)
                return surfaceBoxGesture.Hint;

            if (IsNewModelRequested)
                return NewModelHint;

            if (contourHandles.IsShown)
                return contourHandles.Hint;

            return BoxModelSelectionModeSetting.IsSelectingFaces ? FaceSelectionHint : IdleHint;
        }


        // ── Мышь ─────────────────────────────────────────────────────────

        private void OnMouseMove(Event current, SceneView sceneView)
        {
            TakeKeyboardFromWindow(sceneView);
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            hoveredBuildingContext = LiveBuilderBuildingContext.TryFind(ray, out var context) ? context : null;

            if (CurrentPathGesture is { } pathGesture)
            {
                pathGesture.UpdateHover(ray);
                sceneView.Repaint();
                return;
            }

            switch (CurrentTool)
            {
                case LiveBuilderTool.Opening:
                    openingPlacer.UpdateHover(LiveBuilderSceneHit.Find(ray));
                    break;

                case LiveBuilderTool.Ruler:
                    ruler.UpdateHover(ray);
                    break;

                case LiveBuilderTool.Box when drawGesture.IsChoosingDepth:
                    drawGesture.UpdateDepth(current.mousePosition);
                    break;

                case LiveBuilderTool.Box when surfaceBoxGesture.IsChoosingDepth:
                    surfaceBoxGesture.UpdateDepth(current.mousePosition);
                    break;

                case LiveBuilderTool.Stairs when stairsGesture.IsChoosingHeight:
                    stairsGesture.UpdateHeight(current.mousePosition);
                    break;

                default:
                    hoveredSurface = BoxModelPicker.TryPickSurface(ray, out var hit) ? hit : null;
                    hoveredSnapPoint = BoxModelDrawPlaneFinder.TryFind(ray, IsNewModelRequested, out var plane, out var point) ? (plane, point) : null;
                    break;
            }

            sceneView.Repaint();
        }

        // Мышь пришла в сцену из окна Live Builder — клавиатура сцене: иначе Tab, цифры и PageUp уходят в окно, где только что нажал кнопку.
        // Число в окне сейчас набирается — не трогаем.
        private static void TakeKeyboardFromWindow(SceneView sceneView)
        {
            if (EditorWindow.focusedWindow is LiveBuilderWindow && !EditorGUIUtility.editingTextField)
                sceneView.Focus();
        }

        private void OnMouseDown(Event current, int controlId)
        {
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            if (CurrentPathGesture is { } pathGesture)
            {
                pathGesture.Click(ray, current.shift);
                current.Use();
                return;
            }

            switch (CurrentTool)
            {
                case LiveBuilderTool.Ruler:
                    ruler.Click(ray);
                    current.Use();
                    return;

                case LiveBuilderTool.Box when drawGesture.IsChoosingDepth:
                    if (drawGesture.Commit())
                        IsNewModelRequested = false;

                    current.Use();
                    return;

                case LiveBuilderTool.Box when surfaceBoxGesture.IsChoosingDepth:
                    surfaceBoxGesture.Commit();
                    current.Use();
                    return;

                case LiveBuilderTool.Stairs when stairsGesture.IsChoosingHeight:
                    stairsGesture.Commit();
                    current.Use();
                    return;
            }

            isMouseDown = true;
            mouseDownPosition = current.mousePosition;
            GUIUtility.hotControl = controlId;
            current.Use();
        }

        private void OnMouseDrag(Event current)
        {
            var hasMovedEnough = (current.mousePosition - mouseDownPosition).magnitude > DragThresholdPixels;
            var downRay = HandleUtility.GUIPointToWorldRay(mouseDownPosition);
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            switch (CurrentTool)
            {
                case LiveBuilderTool.Room:
                    if (isMouseDown && !roomGesture.IsActive && hasMovedEnough)
                        roomGesture.TryBegin(downRay);

                    if (roomGesture.IsActive)
                        roomGesture.Update(ray);

                    break;

                case LiveBuilderTool.Stairs:
                    if (isMouseDown && !stairsGesture.IsActive && hasMovedEnough)
                        stairsGesture.TryBegin(downRay);

                    if (stairsGesture.IsActive)
                        stairsGesture.Update(ray);

                    break;

                // Протянул — проём своего размера по любой поверхности; просто клик — готовый размер, на отпускании.
                case LiveBuilderTool.Opening:
                    if (isMouseDown && !cutterGesture.IsActive && hasMovedEnough)
                        cutterGesture.TryBegin(downRay);

                    if (cutterGesture.IsActive)
                        cutterGesture.Update(ray);

                    break;

                // Косая поверхность — коробка или вырез по её нормали; иначе — обычная коробка по осям объекта.
                case LiveBuilderTool.Box:
                    if (isMouseDown && !drawGesture.IsActive && !surfaceBoxGesture.IsActive && hasMovedEnough)
                    {
                        if (LiveBuilderSurfaceBoxGesture.IsAngledSurface(downRay, IsNewModelRequested))
                            surfaceBoxGesture.TryBegin(downRay);
                        else
                            drawGesture.TryBegin(downRay, IsNewModelRequested);
                    }

                    if (surfaceBoxGesture.IsActive)
                        surfaceBoxGesture.UpdateRectangle(ray);
                    else
                        drawGesture.UpdateRectangle(ray);

                    break;
            }

            current.Use();
        }

        private void OnMouseUp(Event current)
        {
            GUIUtility.hotControl = 0;
            isMouseDown = false;

            if (CurrentTool == LiveBuilderTool.Room && roomGesture.IsActive)
                roomGesture.Commit();
            else if (CurrentTool == LiveBuilderTool.Stairs && stairsGesture.IsActive)
                stairsGesture.FinishFootprint(current.mousePosition);
            else if (CurrentTool == LiveBuilderTool.Box && drawGesture.IsActive)
                drawGesture.TryFinishRectangle(current.mousePosition);
            else if (CurrentTool == LiveBuilderTool.Box && surfaceBoxGesture.IsActive)
                surfaceBoxGesture.FinishRectangle(current.mousePosition);
            else if (CurrentTool == LiveBuilderTool.Opening && cutterGesture.IsActive)
                cutterGesture.Commit();
            else if (CurrentTool == LiveBuilderTool.Opening && CanPlaceOpening(current))
                openingPlacer.Commit();
            else
                LiveBuilderClickSelection.Select(current);

            current.Use();
        }

        // Клик в «Проёме» по стене ставит проём; по дыре готового проёма — выделяет его.
        private bool CanPlaceOpening(Event current)
        {
            var hit = LiveBuilderSceneHit.Find(HandleUtility.GUIPointToWorldRay(current.mousePosition));

            if (hit.IsHoleCutterInFront)
                return false;

            openingPlacer.UpdateHover(hit);
            return openingPlacer.HasTarget;
        }


        // ── Клавиши ──────────────────────────────────────────────────────

        // Шаг сетки, этаж, отмена — и клавиши рисования стены, здания и комнаты по контуру.
        private void OnKeyDown(Event current)
        {
            var pathGesture = CurrentPathGesture;

            if (pathGesture != null && pathGesture.HandleKey(current.keyCode))
            {
                current.Use();
                return;
            }

            // Цифра — инструмент из палитры, пока ничего не рисуешь: во время рисования линии цифры — её длина.
            if (!IsAnythingStarted() && !current.control && !current.alt && LiveBuilderToolSetting.TryGetByHotkey(current.keyCode, out var tool))
            {
                LiveBuilderToolSetting.Current = tool;
                current.Use();
                return;
            }

            switch (current.keyCode)
            {
                case KeyCode.LeftBracket:
                    BoxModelGrid.ChangeStep(false);
                    break;

                case KeyCode.RightBracket:
                    BoxModelGrid.ChangeStep(true);
                    break;

                case KeyCode.PageUp:
                    LiveBuilderStoreys.CurrentStorey++;
                    break;

                case KeyCode.PageDown:
                    LiveBuilderStoreys.CurrentStorey--;
                    break;

                case KeyCode.Tab when stairsGesture.IsActive:
                    stairsGesture.RotateDirection();
                    break;

                // Выделенная лестница — Tab поворачивает её на месте.
                case KeyCode.Tab when Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<BoxModelStairs>(out var selectedStairs):
                    BoxModelStairsTurns.Turn(selectedStairs, BoxModelStairsTurns.Clockwise(selectedStairs.Direction));
                    break;

                case KeyCode.Return or KeyCode.KeypadEnter when stairsGesture.IsChoosingHeight:
                    stairsGesture.Commit();
                    break;

                case KeyCode.Escape when IsAnythingStarted():
                    CancelAll();
                    IsNewModelRequested = false;
                    break;

                // Ничего не начато — Esc снимает выделение: окно показывает настройки нового.
                case KeyCode.Escape when Selection.activeGameObject != null:
                    Selection.activeGameObject = null;
                    break;

                default:
                    return;
            }

            current.Use();
        }

        private bool IsAnythingStarted()
        {
            return drawGesture.IsActive || surfaceBoxGesture.IsActive || IsNewModelRequested || roomGesture.IsActive || stairsGesture.IsActive
                   || cutterGesture.IsActive || ruler.IsActive;
        }


        // ── Рисование ────────────────────────────────────────────────────

        private void DrawGesturePreviews()
        {
            if (CurrentPathGesture is { } pathGesture)
            {
                pathGesture.DrawPreview();
                return;
            }

            switch (CurrentTool)
            {
                case LiveBuilderTool.Room:
                    roomGesture.DrawPreview();
                    break;

                case LiveBuilderTool.Stairs:
                    stairsGesture.DrawPreview();
                    break;

                case LiveBuilderTool.Opening when cutterGesture.IsActive:
                    cutterGesture.DrawPreview();
                    break;

                case LiveBuilderTool.Opening:
                    openingPlacer.DrawPreview();
                    break;

                case LiveBuilderTool.Ruler:
                    ruler.DrawPreview();
                    break;

                default:
                    drawGesture.DrawPreview();
                    surfaceBoxGesture.DrawPreview();
                    break;
            }
        }


        // Подсветка грани под курсором и прицел на сетке — куда придётся клик или начало рисования.
        // Мышь над ручкой коробки — ни того ни другого: нажатие достанется ручке.
        private void DrawHover(int controlId)
        {
            var isDrawingTool = CurrentTool is LiveBuilderTool.Box or LiveBuilderTool.Room or LiveBuilderTool.Stairs && CurrentPathGesture == null;
            var isGestureActive = drawGesture.IsActive || roomGesture.IsActive || stairsGesture.IsActive;

            if (!isDrawingTool || isGestureActive || HandleUtility.nearestControl != controlId)
                return;

            if (hoveredSurface is { } hover && hover.Model != null)
            {
                // В режиме «Грани» видно грань коробки целиком — ровно то, что выделит клик.
                if (BoxModelSelectionModeSetting.IsSelectingFaces && BoxModelPartFace.TryFindUnder(hover, out var partFace))
                    BoxModelFaceMoveHandle.DrawFace(partFace, new Color(1f, 0.85f, 0.2f, 0.08f), new Color(1f, 0.85f, 0.2f, 0.6f));
                else
                    DrawSurfaceHighlight(hover);
            }

            if (hoveredSnapPoint is { } snapPoint)
                BoxModelSnapCursor.Draw(snapPoint.Plane, snapPoint.Point);
        }

        private static void DrawSurfaceHighlight(BoxModelSurfaceHit hover)
        {
            var surface = hover.Surface;
            var axis = BoxFaces.Axis(surface.Face);
            var axisU = BoxFaces.SurfaceAxisU(surface.Face);
            var axisV = BoxFaces.SurfaceAxisV(surface.Face);
            var corners = new Vector3[4];
            var area = surface.Area;
            var cornerCoordinates = new[] { (area.xMin, area.yMin), (area.xMax, area.yMin), (area.xMax, area.yMax), (area.xMin, area.yMax) };

            for (var index = 0; index < corners.Length; index++)
            {
                var corner = Vector3.zero;
                corner[axis] = MillimeterBox.ToMeters(surface.Plane);
                corner[axisU] = MillimeterBox.ToMeters(cornerCoordinates[index].Item1);
                corner[axisV] = MillimeterBox.ToMeters(cornerCoordinates[index].Item2);
                corners[index] = hover.Model.transform.TransformPoint(corner);
            }

            Handles.DrawSolidRectangleWithOutline(corners, new Color(1f, 0.85f, 0.2f, 0.08f), new Color(1f, 0.85f, 0.2f, 0.6f));
        }
    }
}
