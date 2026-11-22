using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Live Builder» в окне сцены (клавиша B). Что делает мышь — выбранный в палитре инструмент (<see cref="LiveBuilderTool"/>):
    /// здесь — только общее для всех:
    /// - нажатие: клик это или протягивание; клик, который инструмент не забрал, выделяет своё под мышью (<see cref="LiveBuilderClickSelection"/>)
    /// - ручки выделенного (<see cref="LiveBuilderSelectionHandles"/>) и точки контура (<see cref="LiveBuilderContourHandles"/>) — у того, что правит инструмент
    /// - подсветка грани под мышью и прицел на сетке — если инструмент их просит
    /// - подсказка внизу окна сцены: что делать сейчас (<see cref="LiveBuilderSceneText.DrawHint"/>); шпаргалка клавиш в углу (<see cref="LiveBuilderSceneKeys"/>);
    ///   что под мышью — для строки «Into» (<see cref="LiveBuilderIntoTarget"/>)
    /// - клавиши: ставит контексты ярлыков на время инструмента (<see cref="LiveBuilderShortcutContexts"/>); что делают — <see cref="LiveBuilderSceneKeyboard"/>
    /// </summary>
    [EditorTool("Live Builder")]
    public class LiveBuilderSceneTool : EditorTool
    {
        // Сдвиг мыши, после которого нажатие — уже рисование, а не клик, пикс.
        private const float DragThresholdPixels = 4f;

        private readonly LiveBuilderContourHandles contourHandles = new();

        private LiveBuilderSceneKeyboard keyboard;
        private LiveBuilderShortcutContexts shortcutContexts;

        private Vector2 mouseDownPosition;
        private bool isMouseDown;
        private bool isDoubleClick;
        private BoxModelSurfaceHit? hoveredSurface;

        // Здание по контуру под мышью — для строки Into: где сейчас строишь.
        private LiveBuilderBuildingContext hoveredBuildingContext;
        private (BoxModelDrawPlane Plane, Vector3 Point)? hoveredSnapPoint;


        // Контур выделенного виден везде, где инструмент его показывает, кроме уже начатой новой линии; ручки — если его правит инструмент.
        // Ручки ближе к мыши забирают клик себе, клик по пустому месту в инструменте линии начинает новую линию.
        internal static bool CanShowContour => CurrentTool.ShowsContour && (CurrentTool.PathGesture == null || !CurrentTool.PathGesture.IsActive);

        private static LiveBuilderTool CurrentTool => LiveBuilderToolPalette.Current;

        public override GUIContent toolbarIcon => new(EditorGUIUtility.IconContent("PreMatCube").image,
            "Live Builder: коробки, комнаты, стены, проёмы, лестницы (" + LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.ToggleLiveBuilder) + ")");

        public LiveBuilderSceneKeyboard Keyboard => keyboard ??= new LiveBuilderSceneKeyboard(this);

        // Мышь держит чужая ручка (тянешь грань, выдавливаешь) — клавиши её. Обновляется на каждом событии окна сцены.
        internal bool IsOtherHandleDragged { get; private set; }

        // У выбранного инструмента что-то начато или тянут точку контура. Начатое у прочих снимает смена инструмента.
        internal bool IsAnythingStarted => CurrentTool.IsBusy || contourHandles.IsGestureActive;

        private LiveBuilderShortcutContexts ShortcutContexts => shortcutContexts ??= new LiveBuilderShortcutContexts(Keyboard);


        // ── Включение ────────────────────────────────────────────────────

        public override void OnActivated()
        {
            ShortcutContexts.Register();
            LiveBuilderShortcutConflicts.WarnOncePerSession();
            LiveBuilderToolPalette.Changed += CancelAll;
            Undo.undoRedoPerformed += CancelAfterUndo;
            EditorSceneManager.activeSceneChangedInEditMode += CancelOnSceneChange;
            AssemblyReloadEvents.beforeAssemblyReload += CancelAll;
        }


        public override void OnWillBeDeactivated()
        {
            ShortcutContexts.Unregister();
            LiveBuilderToolPalette.Changed -= CancelAll;
            Undo.undoRedoPerformed -= CancelAfterUndo;
            EditorSceneManager.activeSceneChangedInEditMode -= CancelOnSceneChange;
            AssemblyReloadEvents.beforeAssemblyReload -= CancelAll;
            CancelAll();
        }

        // Всё начатое отменяется у всех инструментов; начатая линия остаётся такой, как нарисована. Зовёт и цепочка Esc.
        internal void CancelAll()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            contourHandles.Cancel();
            hoveredSurface = null;
            hoveredSnapPoint = null;
            isMouseDown = false;
        }

        // Ctrl+Z мог убрать объект, на котором начата фигура: она встала бы не туда. Линии точками рисуются дальше.
        private void CancelAfterUndo()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.CancelAfterUndo();

            hoveredSurface = null;
            hoveredSnapPoint = null;
        }

        // Другая сцена — начатое на прежней бросаем.
        private void CancelOnSceneChange(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
        {
            CancelAll();
        }


        // ── Окно сцены ───────────────────────────────────────────────────

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView)
                return;

            // Контексты живут в памяти скриптов: перезагрузка их стирает, а OnActivated после неё может не прийти.
            ShortcutContexts.Register();
            var tool = CurrentTool;

            // Пока рисуешь, ручки выделенного спрятаны: клик «готово» не должен достаться ручке грани под мышью.
            LiveBuilderSelectionHandles.Draw(tool, !tool.IsBusy);

            if (CanShowContour)
                contourHandles.OnSceneGUI(owner => tool.CanEdit(owner), tool.PathGesture == null);

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            IsOtherHandleDragged = GUIUtility.hotControl != 0 && GUIUtility.hotControl != controlId && !contourHandles.IsGestureActive;

            // Клавиши с ярлыками сюда не доходят — их забрал Shortcut Manager. Остаток (Enter, Esc, хвост набора числа) — напрямую,
            // не через фильтр элемента: тот отбрасывает их, пока мышь держит другой элемент.
            if (current.type == EventType.KeyDown)
            {
                if (!IsOtherHandleDragged && Keyboard.HandleUnboundKey(current.keyCode))
                    current.Use();

                return;
            }

            // Пипетка текстуры — Alt + клик: нажатие с Alt забрала камера, поэтому до раздачи событий и по rawType.
            if (LiveBuilderTexturePipette.HandleEvent(current))
                return;

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
                    if (!tool.IsBusy)
                        LiveBuilderSelectionBounds.Draw();

                    DrawHover(controlId);
                    tool.DrawPreview();
                    LiveBuilderSceneText.DrawHint(sceneView, CurrentHint());
                    LiveBuilderSceneKeys.Draw(sceneView, tool, CanShowContour && LiveBuilderContourHandles.IsShown);
                    break;
            }
        }

        // Что говорит инструмент и, если он выделяет вглубь, «click again — deeper» у мыши. Где строишь — строка Into в панели.
        // Зовут только при отрисовке сцены — тут есть Event.current.
        private static string CurrentHint()
        {
            var tool = CurrentTool;
            var depthClickHint = tool.ShowsDepthClickHint ? LiveBuilderDepthClick.HintAt(Event.current.mousePosition, tool) : "";
            return tool.Hint + depthClickHint;
        }


        // ── Мышь ─────────────────────────────────────────────────────────

        // Инструмент не занят мышью сам — ищем грань под ней и точку на сетке для подсветки и прицела.
        // Каждый поиск обходит сцену, поэтому — только то, что инструмент покажет: здание под мышью — тем, кто ставит в него,
        // грань и прицел — тем, кто их рисует.
        private void OnMouseMove(Event current, SceneView sceneView)
        {
            TakeKeyboardFromWindow(sceneView);
            var tool = CurrentTool;
            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            hoveredBuildingContext = tool.PlacesIntoBuilding && LiveBuilderBuildingContext.TryFind(ray, out var context) ? context : null;
            hoveredSurface = null;
            hoveredSnapPoint = null;

            if (!tool.OnMouseMove(ray, current) && tool.DrawsSurfaceHover)
            {
                hoveredSurface = BoxModelPicker.TryPickSurface(ray, out var hit) ? hit : null;

                if (tool.DrawsSnapCursor)
                    hoveredSnapPoint = BoxModelDrawPlaneFinder.TryFind(ray, LiveBuilderBoxTool.IsNewModelRequested, out var plane, out var point) ? (plane, point) : null;
            }

            LiveBuilderIntoTarget.RememberHover(hoveredBuildingContext, hoveredSurface?.Model);
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
