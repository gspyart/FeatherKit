using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Движение по холсту графа как в Figma и Miro — для тачпада Mac и для мыши.
    /// - прокрутка двумя пальцами или колесо — сдвиг холста, Shift + колесо — по горизонтали
    /// - щипок, Cmd + прокрутка, Ctrl + прокрутка — зум к курсору; пределы зума одни и для «Frame All»
    /// - тянуть с зажатым Пробелом, Alt или средней кнопкой — сдвиг; короткий Пробел — поиск нод
    /// - F — показать выделенное (нет выделения — всё), A — всё; Esc уводит фокус из поля на холст
    /// - пока набирают текст в поле, клавиши и команда Delete холста молчат
    /// </summary>
    internal class ScenarioCanvasNavigator : Manipulator
    {
        public const float MinScale = 0.15f;
        public const float MaxScale = 2f;

        private const float PanPixelsPerWheelUnit = 12f;
        private const float ScrollPixelsPerWheelUnit = 18f;
        private const float ZoomPerWheelUnit = 0.04f;
        private const float FramePadding = 40f;
        private const int MagnifyGestureEventType = 1000; // EditorGUIUtility.magnifyGestureEventType, он internal
        private const string DeleteCommand = "Delete";
        private const string SoftDeleteCommand = "SoftDelete";

        private readonly ScenarioGraphView graphView;
        private readonly IMGUIContainer gestureListener;

        private bool isSpaceHeld;
        private bool hasPannedWhileSpaceHeld;
        private bool isPanning;
        private Vector2 lastPanMousePosition;


        public ScenarioCanvasNavigator(ScenarioGraphView graphView)
        {
            this.graphView = graphView;

            // Невидимая точка в углу холста: IMGUI-события раздаются всем IMGUIContainer панели, клики она не ловит.
            gestureListener = new IMGUIContainer(HandleMagnifyGesture)
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0f, top = 0f, width = 1f, height = 1f }
            };
        }


        /// <summary>Последнее место мыши в координатах элемента холста (не его содержимого) — для поиска и заметки по клавише.</summary>
        public Vector2 LastMousePosition { get; private set; }

        /// <summary>Сдвиг «панель → экран» по самой Unity: у окна есть вкладка, и считать его по position окна ошибочно.</summary>
        public Vector2? PanelToScreenOffset { get; private set; }


        // ── Набор текста ─────────────────────────────────────────────────

        /// <summary>Фокус в поле ввода — клавиши холста и удаление ноды тогда молчат: буквы и Delete — это текст.</summary>
        public static bool IsTypingText(Focusable focusedElement)
        {
            if (focusedElement is IMGUIContainer)
                return EditorGUIUtility.editingTextField;

            for (var element = focusedElement as VisualElement; element != null; element = element.parent)
            {
                for (var type = element.GetType(); type != null; type = type.BaseType)
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TextInputBaseField<>))
                        return true;
                }
            }

            return false;
        }


        // GraphView удаляет выделенное по команде, не спрашивая, где фокус; клавиши Delete и Backspace поля — не команды, их не трогаем.
        private void BlockDeleteWhileTyping(CommandEventBase<ValidateCommandEvent> commandEvent)
        {
            if (IsDeleteCommand(commandEvent.commandName) && IsTypingText(target.panel?.focusController?.focusedElement))
                commandEvent.StopImmediatePropagation();
        }

        private void BlockDeleteWhileTyping(CommandEventBase<ExecuteCommandEvent> commandEvent)
        {
            if (IsDeleteCommand(commandEvent.commandName) && IsTypingText(target.panel?.focusController?.focusedElement))
                commandEvent.StopImmediatePropagation();
        }

        private static bool IsDeleteCommand(string commandName)
        {
            return commandName == DeleteCommand || commandName == SoftDeleteCommand;
        }


        // ── Подписка ─────────────────────────────────────────────────────

        // SetupZoom сам вешает ContentZoomer, а тот зумит обычным колесом — колесо перехватываем раньше него.
        protected override void RegisterCallbacksOnTarget()
        {
            graphView.SetupZoom(MinScale, MaxScale);
            target.Add(gestureListener);
            target.RegisterCallback<WheelEvent>(HandleWheel, TrickleDown.TrickleDown);
            target.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            target.RegisterCallback<KeyUpEvent>(HandleKeyUp, TrickleDown.TrickleDown);
            target.RegisterCallback<ValidateCommandEvent>(BlockDeleteWhileTyping, TrickleDown.TrickleDown);
            target.RegisterCallback<ExecuteCommandEvent>(BlockDeleteWhileTyping, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseDownEvent>(HandleMouseDown, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseMoveEvent>(HandleMouseMove, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseUpEvent>(HandleMouseUp, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseCaptureOutEvent>(HandleCaptureLost);
            target.RegisterCallback<FocusOutEvent>(HandleFocusLost);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            gestureListener.RemoveFromHierarchy();
            target.UnregisterCallback<WheelEvent>(HandleWheel, TrickleDown.TrickleDown);
            target.UnregisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<KeyUpEvent>(HandleKeyUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<ValidateCommandEvent>(BlockDeleteWhileTyping, TrickleDown.TrickleDown);
            target.UnregisterCallback<ExecuteCommandEvent>(BlockDeleteWhileTyping, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseDownEvent>(HandleMouseDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseMoveEvent>(HandleMouseMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseUpEvent>(HandleMouseUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseCaptureOutEvent>(HandleCaptureLost);
            target.UnregisterCallback<FocusOutEvent>(HandleFocusLost);
        }

        private void HandleCaptureLost(MouseCaptureOutEvent captureEvent)
        {
            isPanning = false;
        }

        private void HandleFocusLost(FocusOutEvent focusEvent)
        {
            isSpaceHeld = false;
        }


        // ── Прокрутка и щипок ────────────────────────────────────────────

        // Прокручиваемая панель внутри холста (переменные) крутится сама — за неё, раз событие перехвачено.
        private void HandleWheel(WheelEvent wheelEvent)
        {
            var delta = (Vector2)wheelEvent.delta;
            var isZoom = wheelEvent.actionKey || wheelEvent.ctrlKey || wheelEvent.commandKey;
            var scrollView = (wheelEvent.target as VisualElement)?.GetFirstAncestorOfType<ScrollView>();

            if (!isZoom && scrollView != null && target.Contains(scrollView))
                scrollView.scrollOffset += delta * ScrollPixelsPerWheelUnit;
            else if (isZoom)
                ZoomAround(target.WorldToLocal(wheelEvent.mousePosition), Mathf.Exp(-delta.y * ZoomPerWheelUnit));
            else if (wheelEvent.shiftKey && Mathf.Approximately(delta.x, 0f))
                PanBy(new Vector2(-delta.y, 0f) * PanPixelsPerWheelUnit);
            else
                PanBy(-delta * PanPixelsPerWheelUnit);

            wheelEvent.StopImmediatePropagation();
        }

        // Щипок тачпада UI Toolkit своим событием не отдаёт: он приходит только в IMGUI, увеличение — в delta.x.
        // Тут же, раз мы внутри IMGUI, запоминаем точный сдвиг панели на экране.
        private void HandleMagnifyGesture()
        {
            var gesture = Event.current;

            if (gesture == null)
                return;

            PanelToScreenOffset = GUIUtility.GUIToScreenPoint(Vector2.zero) - gestureListener.LocalToWorld(Vector2.zero);

            if ((int)gesture.type != MagnifyGestureEventType)
                return;

            ZoomAround(gestureListener.ChangeCoordinatesTo(target, gesture.mousePosition), 1f + gesture.delta.x);
            gesture.Use();
        }


        // ── Клавиши ──────────────────────────────────────────────────────

        private void HandleKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode == KeyCode.Escape && keyEvent.target != target)
            {
                // Своё Esc поле успеет сделать (заметка отменит правку), фокус уходит на холст следом.
                target.schedule.Execute(() => target.Focus());
                return;
            }

            if (IsTypingText(keyEvent.target as Focusable))
                return;

            switch (keyEvent.keyCode)
            {
                case KeyCode.Space:
                    if (!isSpaceHeld)
                        hasPannedWhileSpaceHeld = false;

                    isSpaceHeld = true;
                    break;

                case KeyCode.F when !HasModifiers(keyEvent):
                    FrameSelectedOrAll();
                    break;

                case KeyCode.A when !HasModifiers(keyEvent):
                    FrameWholeGraph();
                    break;

                default:
                    return;
            }

            keyEvent.StopImmediatePropagation();
        }

        // Пробел нажали и отпустили, не двигая холст, — поиск нод у курсора.
        private void HandleKeyUp(KeyUpEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.Space || !isSpaceHeld)
                return;

            isSpaceHeld = false;

            if (!hasPannedWhileSpaceHeld)
                graphView.NodeCreation.OpenSearch(graphView.NodeCreation.CanvasToScreen(LastMousePosition), null);

            keyEvent.StopImmediatePropagation();
        }

        /// <summary>Клавиша без Cmd, Ctrl, Alt и Shift: на Mac Ctrl+C и Cmd+C — не наши клавиши.</summary>
        public static bool HasModifiers(KeyboardEventBase<KeyDownEvent> keyEvent)
        {
            return keyEvent.actionKey || keyEvent.ctrlKey || keyEvent.commandKey || keyEvent.altKey || keyEvent.shiftKey;
        }


        // ── Перетаскивание холста ────────────────────────────────────────

        private void HandleMouseDown(MouseDownEvent mouseEvent)
        {
            var isPanButton = mouseEvent.button == (int)MouseButton.MiddleMouse
                              || (mouseEvent.button == (int)MouseButton.LeftMouse && (isSpaceHeld || mouseEvent.altKey));

            if (!isPanButton)
                return;

            isPanning = true;
            hasPannedWhileSpaceHeld |= isSpaceHeld;
            lastPanMousePosition = mouseEvent.mousePosition;
            target.CaptureMouse();

            mouseEvent.StopImmediatePropagation();
        }

        private void HandleMouseMove(MouseMoveEvent mouseEvent)
        {
            LastMousePosition = target.WorldToLocal(mouseEvent.mousePosition);

            if (!isPanning)
                return;

            PanBy(mouseEvent.mousePosition - lastPanMousePosition);
            lastPanMousePosition = mouseEvent.mousePosition;

            mouseEvent.StopImmediatePropagation();
        }

        private void HandleMouseUp(MouseUpEvent mouseEvent)
        {
            if (!isPanning)
                return;

            isPanning = false;
            target.ReleaseMouse();

            mouseEvent.StopImmediatePropagation();
        }


        // ── Вид ──────────────────────────────────────────────────────────

        /// <summary>Показать весь граф. Свой расчёт: стандартный FrameAll не отдаляет дальше 0.25 — большой граф не влезал.</summary>
        public void FrameWholeGraph()
        {
            Frame(graphView.graphElements.Where(element => element is Node || element is ScenarioNoteView));
        }

        public void FrameSelectedOrAll()
        {
            var selected = graphView.selection.OfType<GraphElement>().Where(element => element is not Edge).ToList();

            if (selected.Count > 0)
                Frame(selected);
            else
                FrameWholeGraph();
        }

        // Ближе 100 % не приближаем: маленький граф на весь экран читается хуже.
        private void Frame(IEnumerable<GraphElement> elements)
        {
            var view = graphView.layout;
            Rect? bounds = null;

            foreach (var element in elements)
            {
                var area = element.ChangeCoordinatesTo(graphView.contentViewContainer, new Rect(Vector2.zero, element.layout.size));

                if (area.width > 0f && !float.IsNaN(area.x))
                    bounds = bounds.HasValue ? Union(bounds.Value, area) : area;
            }

            if (!bounds.HasValue || view.width <= 0f || view.height <= 0f)
                return;

            var fitScale = Mathf.Min((view.width - 2f * FramePadding) / bounds.Value.width, (view.height - 2f * FramePadding) / bounds.Value.height);
            var scale = Mathf.Clamp(fitScale, MinScale, 1f);
            var position = view.size * 0.5f - bounds.Value.center * scale;

            graphView.UpdateViewTransform(position, new Vector3(scale, scale, 1f));
        }

        private static Rect Union(Rect first, Rect second)
        {
            return Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
        }

        private void PanBy(Vector2 screenOffset)
        {
            var transform = graphView.viewTransform;
            graphView.UpdateViewTransform(transform.position + (Vector3)screenOffset, transform.scale);
        }

        /// <summary>Зум с точкой холста (в координатах холста), которая остаётся на месте.</summary>
        public void ZoomAround(Vector2 localPoint, float factor)
        {
            var transform = graphView.viewTransform;
            var oldScale = transform.scale.x;
            var newScale = Mathf.Clamp(oldScale * factor, MinScale, MaxScale);

            if (Mathf.Approximately(newScale, oldScale))
                return;

            var position = (Vector2)transform.position;
            var newPosition = localPoint - (localPoint - position) * (newScale / oldScale);

            graphView.UpdateViewTransform(newPosition, new Vector3(newScale, newScale, 1f));
        }
    }
}
