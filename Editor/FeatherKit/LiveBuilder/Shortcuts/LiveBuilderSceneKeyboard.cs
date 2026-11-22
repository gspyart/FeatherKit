using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что делают клавиши Live Builder в окне сцены — часть <see cref="LiveBuilderSceneTool"/>; ярлыки Shortcut Manager зовут сюда.
    /// - сетка, этаж, прилипание к точкам, шпаргалка, поворот лестницы
    /// - Enter, цепочка Esc и клавиши набора — выбранному инструменту
    /// - что сейчас в сцене — для контекстов ярлыков (<see cref="LiveBuilderShortcutContexts"/>)
    /// </summary>
    public sealed class LiveBuilderSceneKeyboard
    {
        private readonly LiveBuilderSceneTool owner;


        public LiveBuilderSceneKeyboard(LiveBuilderSceneTool owner)
        {
            this.owner = owner;
        }


        public LiveBuilderSceneKeyState KeyState => new()
        {
            IsToolActive = LiveBuilderActivation.IsToolActive,
            IsSceneViewFocused = IsSceneViewFocused,
            IsEditingTextField = EditorGUIUtility.editingTextField,
            IsOtherHandleDragged = owner.IsOtherHandleDragged,
            IsCameraFlying = Tools.viewTool == ViewTool.FPS,
            IsAnythingStarted = owner.IsAnythingStarted,
            IsToolBusy = CurrentTool.IsBusy,
            IsContourShown = IsContourShown,
            IsContourPointSelected = LiveBuilderContourSelection.Knots.Count > 0
        };

        private static LiveBuilderTool CurrentTool => LiveBuilderToolPalette.Current;

        private static bool IsContourShown => LiveBuilderSceneTool.CanShowContour && LiveBuilderContourHandles.IsShown;

        // hasFocus, а не EditorWindow.focusedWindow: в редакторе это одно и то же, а в фоновом режиме (проверки клавиш) focusedWindow пуст.
        private static bool IsSceneViewFocused
        {
            get
            {
                foreach (SceneView sceneView in SceneView.sceneViews)
                    if (sceneView != null && sceneView.hasFocus)
                        return true;

                return false;
            }
        }


        // ── Общие клавиши ────────────────────────────────────────────────

        public void ChangeGridStep(bool isLarger)
        {
            LiveBuilderGrid.ChangeStep(isLarger);
            SceneView.RepaintAll();
        }


        public void ChangeStorey(int delta)
        {
            LiveBuilderStoreys.CurrentStorey += delta;
            SceneView.RepaintAll();
        }


        // Прилипание к углам, серединам и рёбрам построек и направляющие.
        public void TogglePointSnap()
        {
            LiveBuilderPointSnap.IsEnabled = !LiveBuilderPointSnap.IsEnabled;
            SceneView.RepaintAll();
        }


        public void ToggleKeyHints()
        {
            LiveBuilderSceneKeys.IsShown = !LiveBuilderSceneKeys.IsShown;
            SceneView.RepaintAll();
        }


        // Рисуешь лестницу или пандус — подъём в другую сторону; иначе выделенная лестница или пандус поворачивается на месте, если его правит инструмент.
        public void Rotate()
        {
            var selected = Selection.activeGameObject;

            if (!SendKeyToTool(KeyCode.Tab) && selected != null)
            {
                if (selected.TryGetComponent<BoxModelStairs>(out var selectedStairs) && CurrentTool.CanEdit(selectedStairs))
                    BoxModelStairsTurns.Turn(selectedStairs, BoxModelStairsTurns.Clockwise(selectedStairs.Direction));
                else if (selected.TryGetComponent<BoxModelPart>(out var selectedPart) && CurrentTool.CanEdit(selectedPart))
                    BoxModelRamps.TurnRamp(selectedPart);
            }

            SceneView.RepaintAll();
        }


        // ── Enter и Esc ──────────────────────────────────────────────────

        // Рисуешь — «готово» или набранное число; иначе клавиша не наша.
        public bool Confirm()
        {
            return SendKeyToTool(KeyCode.Return);
        }


        // Отменяет одно, по порядку: набранное число или начатое → выбранные точки контура → «Новый» у коробки → на «Выделение» → снять выделение.
        // Возвращает, было ли что отменять.
        public bool Cancel()
        {
            var isCancelled = CancelOneStep();

            if (isCancelled)
                SceneView.RepaintAll();

            return isCancelled;
        }

        // Начатое бросает инструмент сам: набранное число он стирает первым, а «Новый» у коробки снимает, когда ничего не начато.
        private bool CancelOneStep()
        {
            var tool = CurrentTool;

            if (tool.IsBusy)
            {
                if (!SendKeyToTool(KeyCode.Escape))
                    owner.CancelAll();

                return true;
            }

            if (owner.IsAnythingStarted)
            {
                owner.CancelAll();
                return true;
            }

            if (IsContourShown && LiveBuilderContourSelection.Knots.Count > 0)
            {
                LiveBuilderContourSelection.Clear();
                return true;
            }

            if (SendKeyToTool(KeyCode.Escape))
                return true;

            if (tool is not LiveBuilderSelectTool)
            {
                LiveBuilderToolPalette.Select<LiveBuilderSelectTool>();
                return true;
            }

            if (Selection.activeGameObject == null)
                return false;

            BoxModelFaceSelection.Clear();
            Selection.activeGameObject = null;
            return true;
        }


        // ── Клавиши инструменту ──────────────────────────────────────────

        // Цифры, X, запятая, Enter, Esc, Tab — инструмент сам решает, его ли это клавиша. true — забрал.
        public bool SendKeyToTool(KeyCode key)
        {
            var isTaken = CurrentTool.OnKeyDown(new Event { type = EventType.KeyDown, keyCode = key });

            if (isTaken)
                SceneView.RepaintAll();

            return isTaken;
        }


        // Клавиши без ярлыков — их окно сцены отдаёт сюда само: Enter и Esc ярлыком Unity сделать не даёт, остальное — хвост набора числа.
        // true — клавиша наша.
        public bool HandleUnboundKey(KeyCode key)
        {
            return key switch
            {
                KeyCode.Return or KeyCode.KeypadEnter => Confirm(),
                KeyCode.Escape => Cancel(),
                _ => IsTypingKey(key) && SendKeyToTool(key)
            };
        }

        // Цифры над буквами и X — ярлыки «рисую»; здесь то, что в набор попадает без них.
        private static bool IsTypingKey(KeyCode key)
        {
            return key is >= KeyCode.Keypad0 and <= KeyCode.Keypad9
                or KeyCode.KeypadPeriod or KeyCode.KeypadMultiply or KeyCode.Comma or KeyCode.Period or KeyCode.Semicolon or KeyCode.Backspace;
        }
    }
}
