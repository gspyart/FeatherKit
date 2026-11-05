using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Live Builder» в окне сцены (клавиша B). Что делает мышь — выбранный в палитре инструмент (<see cref="LiveBuilderTool"/>):
    /// здесь — только общее для всех:
    /// - нажатие: клик это или протягивание; клик, который инструмент не забрал, выделяет своё под мышью (<see cref="LiveBuilderClickSelection"/>)
    /// - ручки выделенного (<see cref="LiveBuilderSelectionHandles"/>) и точки контура (<see cref="LiveBuilderContourHandles"/>) — у того, что правит инструмент
    /// - подсветка грани под мышью и прицел на сетке — если инструмент их просит
    /// - подсказка внизу окна сцены и окна Live Builder: этаж и что делать сейчас
    /// Клавиши: [ и ] — шаг сетки, PageUp и PageDown — этаж выше и ниже, цифры — инструмент, Tab — повернуть выделенную лестницу,
    /// Esc — отменить начатое, а если ничего не начато — на «Выделение», в нём — снять выделение.
    /// </summary>
    [EditorTool("Live Builder")]
    public class LiveBuilderSceneTool : EditorTool
    {
        // Сдвиг мыши, после которого нажатие — уже рисование, а не клик, пикс.
        private const float DragThresholdPixels = 4f;

        private readonly LiveBuilderContourHandles contourHandles = new();

        private Vector2 mouseDownPosition;
        private bool isMouseDown;
        private bool isDoubleClick;
        private BoxModelSurfaceHit? hoveredSurface;

        // Здание по контуру под мышью — для подсказки: где сейчас строишь.
        private LiveBuilderBuildingContext hoveredBuildingContext;
        private (BoxModelDrawPlane Plane, Vector3 Point)? hoveredSnapPoint;


        // Подсказка поменялась — окно Live Builder показывает её у себя внизу.
        public static event System.Action HintChanged;


        // Подсказка внизу окна сцены — что делать сейчас.
        public static string CurrentHintText { get; private set; } = "";

        public override GUIContent toolbarIcon => new(EditorGUIUtility.IconContent("PreMatCube").image, "Live Builder: коробки, комнаты, стены, проёмы, лестницы (B)");

        private static LiveBuilderTool CurrentTool => LiveBuilderToolPalette.Current;

        // Контур выделенного виден везде, где инструмент его показывает, кроме уже начатой новой линии; ручки — если его правит инструмент.
        // Ручки ближе к мыши забирают клик себе, клик по пустому месту в инструменте линии начинает новую линию.
        private static bool CanShowContour => CurrentTool.ShowsContour && (CurrentTool.PathGesture == null || !CurrentTool.PathGesture.IsActive);


        // ── Включение ────────────────────────────────────────────────────

        [Shortcut("FeatherKit/Live Builder", typeof(SceneView), KeyCode.B)]
        private static void Activate()
        {
            ToolManager.SetActiveTool<LiveBuilderSceneTool>();
        }


        public override void OnActivated()
        {
            LiveBuilderToolPalette.Changed += CancelAll;
            LiveBuilderRoomSettings.ShapeChanged += CancelAll;
        }


        public override void OnWillBeDeactivated()
        {
            LiveBuilderToolPalette.Changed -= CancelAll;
            LiveBuilderRoomSettings.ShapeChanged -= CancelAll;
            CancelAll();
            LiveBuilderBoxTool.IsNewModelRequested = false;
        }

        // Всё начатое отменяется у всех инструментов; начатая линия остаётся такой, как нарисована.
        private void CancelAll()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            contourHandles.Cancel();
            hoveredSurface = null;
            hoveredSnapPoint = null;
            isMouseDown = false;
        }


        // ── Окно сцены ───────────────────────────────────────────────────

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView)
                return;

            var tool = CurrentTool;
            LiveBuilderSelectionHandles.Draw(tool);

            if (CanShowContour)
                contourHandles.OnSceneGUI(owner => tool.CanEdit(owner), tool.PathGesture == null);

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
                    tool.DrawPreview();
                    PublishHint(CurrentHint());
                    LiveBuilderSceneText.DrawHint(sceneView, CurrentHintText);
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

        // Этаж — здания под мышью, если инструмент строит в нём, иначе уровня; дальше — что говорит инструмент
        // и, если он выделяет вглубь, «клик ещё раз — глубже» у мыши. Зовут только при отрисовке сцены — тут есть Event.current.
        private string CurrentHint()
        {
            var tool = CurrentTool;
            var buildingStorey = hoveredBuildingContext is { IsBuildingAlive: true } ? tool.BuildingStoreyHint(hoveredBuildingContext) : null;
            var storey = buildingStorey ?? "Этаж " + LiveBuilderStoreys.CurrentStorey + (LiveBuilderStoreys.IsSectionEnabled ? ", разрез" : "");
            var depthClickHint = tool.ShowsDepthClickHint ? LiveBuilderDepthClick.HintAt(Event.current.mousePosition, tool) : "";
            return storey + " · " + tool.Hint + depthClickHint;
        }


        // ── Мышь ─────────────────────────────────────────────────────────

        // Инструмент не занят мышью сам — ищем грань под ней и точку на сетке для подсветки и прицела.
        private void OnMouseMove(Event current, SceneView sceneView)
        {
            TakeKeyboardFromWindow(sceneView);
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            hoveredBuildingContext = LiveBuilderBuildingContext.TryFind(ray, out var context) ? context : null;

            if (!CurrentTool.OnMouseMove(ray, current))
            {
                hoveredSurface = BoxModelPicker.TryPickSurface(ray, out var hit) ? hit : null;
                hoveredSnapPoint = BoxModelDrawPlaneFinder.TryFind(ray, LiveBuilderBoxTool.IsNewModelRequested, out var plane, out var point) ? (plane, point) : null;
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


        // Инструмент не забрал нажатие — ждём: клик это или протягивание.
        private void OnMouseDown(Event current, int controlId)
        {
            if (CurrentTool.OnMouseDown(HandleUtility.GUIPointToWorldRay(current.mousePosition), current))
            {
                current.Use();
                return;
            }

            isMouseDown = true;
            mouseDownPosition = current.mousePosition;
            isDoubleClick = current.clickCount == 2;
            GUIUtility.hotControl = controlId;
            current.Use();
        }


        private void OnMouseDrag(Event current)
        {
            var hasMovedEnough = (current.mousePosition - mouseDownPosition).magnitude > DragThresholdPixels;
            var downRay = HandleUtility.GUIPointToWorldRay(mouseDownPosition);
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            CurrentTool.OnMouseDrag(downRay, ray, isMouseDown && hasMovedEnough);
            current.Use();
        }


        // Отпускание не закончило жест инструмента — это клик: выделить своё под мышью.
        private void OnMouseUp(Event current)
        {
            GUIUtility.hotControl = 0;

            // Esc отменил начатое, пока держали мышь: отпускание — уже не клик, выделение не трогаем.
            if (!isMouseDown)
            {
                current.Use();
                return;
            }

            isMouseDown = false;

            if (!CurrentTool.OnMouseUp(current))
                LiveBuilderClickSelection.Select(current, CurrentTool, isDoubleClick);

            current.Use();
        }


        // ── Клавиши ──────────────────────────────────────────────────────

        // Сначала клавиши инструмента, потом общие: инструмент по цифре, шаг сетки, этаж, отмена.
        private void OnKeyDown(Event current)
        {
            if (CurrentTool.OnKeyDown(current))
            {
                current.Use();
                return;
            }

            // Цифра — инструмент из палитры, пока ничего не рисуешь: во время рисования линии цифры — её длина.
            if (!IsAnythingStarted() && !current.control && !current.alt && LiveBuilderToolPalette.TryGetByHotkey(current.keyCode, out var tool))
            {
                LiveBuilderToolPalette.Current = tool;
                current.Use();
                return;
            }

            switch (current.keyCode)
            {
                case KeyCode.LeftBracket:
                    LiveBuilderGrid.ChangeStep(false);
                    break;

                case KeyCode.RightBracket:
                    LiveBuilderGrid.ChangeStep(true);
                    break;

                case KeyCode.PageUp:
                    LiveBuilderStoreys.CurrentStorey++;
                    break;

                case KeyCode.PageDown:
                    LiveBuilderStoreys.CurrentStorey--;
                    break;

                // Выделенная лестница — Tab поворачивает её на месте.
                case KeyCode.Tab when Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<BoxModelStairs>(out var selectedStairs):
                    BoxModelStairsTurns.Turn(selectedStairs, BoxModelStairsTurns.Clockwise(selectedStairs.Direction));
                    break;

                case KeyCode.Escape when IsAnythingStarted():
                    CancelAll();
                    LiveBuilderBoxTool.IsNewModelRequested = false;
                    break;

                // Ничего не начато — Esc уводит на «Выделение»; в нём — снимает выделение: окно показывает настройки нового.
                case KeyCode.Escape when CurrentTool is not LiveBuilderSelectTool:
                    LiveBuilderToolPalette.Current = LiveBuilderToolPalette.Get<LiveBuilderSelectTool>();
                    break;

                case KeyCode.Escape when Selection.activeGameObject != null:
                    BoxModelFaceSelection.Clear();
                    Selection.activeGameObject = null;
                    break;

                default:
                    return;
            }

            current.Use();
        }

        // У любого инструмента что-то начато или тянут точку контура.
        private bool IsAnythingStarted()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                if (tool.IsBusy)
                    return true;

            return contourHandles.IsGestureActive;
        }


        // ── Подсветка под мышью ──────────────────────────────────────────

        // Подсветка грани под курсором и прицел на сетке — куда придётся клик или начало рисования.
        // Мышь над ручкой коробки — ни того ни другого: нажатие достанется ручке.
        private void DrawHover(int controlId)
        {
            var tool = CurrentTool;

            if (!tool.DrawsSurfaceHover || HandleUtility.nearestControl != controlId)
                return;

            if (hoveredSurface is { } hover && hover.Model != null)
            {
                // В режиме «Грани» видно кусок грани под мышью — ровно то, что выделит клик.
                if (BoxModelSelectionModeSetting.IsSelectingFaces && tool.CanEditFaces && BoxModelPartFace.TryFindUnder(hover, out var partFace))
                    BoxModelFaceMoveHandle.DrawFace(partFace, new Color(1f, 0.85f, 0.2f, 0.08f), new Color(1f, 0.85f, 0.2f, 0.6f));
                else
                    DrawSurfaceHighlight(hover);
            }

            if (hoveredSnapPoint is { } snapPoint && tool.DrawsSnapCursor)
                LiveBuilderSnapCursor.Draw(snapPoint.Plane, snapPoint.Point);
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
