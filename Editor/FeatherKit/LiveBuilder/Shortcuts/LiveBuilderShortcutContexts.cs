using UnityEditor.ShortcutManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Когда работают ярлыки Live Builder: пять контекстов Shortcut Manager и одно правило на всех (<see cref="IsActive"/>).
    /// - часть <see cref="LiveBuilderSceneTool"/>: ставит контексты на время инструмента и снимает их — выключенный Live Builder клавиши Unity не трогает
    /// - ставит приоритетными (<see cref="LiveBuilderPriorityShortcutContexts"/>): иначе «2», S и стрелки спорят с ярлыками окна сцены
    /// - контекст — «где сейчас клавиша»: в сцене, ничего не начато, рисую, видны точки контура, выбраны точки
    /// </summary>
    public sealed class LiveBuilderShortcutContexts
    {
        private readonly LiveBuilderShortcutContext[] contexts;
        private bool isRegistered;


        public LiveBuilderShortcutContexts(LiveBuilderSceneKeyboard keyboard)
        {
            contexts = new LiveBuilderShortcutContext[]
            {
                new LiveBuilderSceneShortcutContext(keyboard),
                new LiveBuilderIdleShortcutContext(keyboard),
                new LiveBuilderDrawingShortcutContext(keyboard),
                new LiveBuilderContourShortcutContext(keyboard),
                new LiveBuilderContourPointsShortcutContext(keyboard)
            };
        }


        // ── Правило ──────────────────────────────────────────────────────

        // Мышь держит чужая ручка — клавиши её: смена инструмента посреди тяги ломает её отмену. Камера летит — WASD и S её.
        public static bool IsActive(LiveBuilderShortcutContextKind kind, LiveBuilderSceneKeyState state)
        {
            var isScene = state.IsToolActive && state.IsSceneViewFocused && !state.IsEditingTextField && !state.IsOtherHandleDragged && !state.IsCameraFlying;
            var isContour = isScene && state.IsContourShown && !state.IsAnythingStarted;

            return kind switch
            {
                LiveBuilderShortcutContextKind.Scene => isScene,
                LiveBuilderShortcutContextKind.Idle => isScene && !state.IsAnythingStarted,
                LiveBuilderShortcutContextKind.Drawing => isScene && state.IsToolBusy,
                LiveBuilderShortcutContextKind.Contour => isContour,
                LiveBuilderShortcutContextKind.ContourPoints => isContour && state.IsContourPointSelected,
                _ => false
            };
        }


        // ── Регистрация ──────────────────────────────────────────────────

        // Повторный вызов ничего не делает: окно сцены зовёт его каждый кадр.
        public void Register()
        {
            if (isRegistered)
                return;

            foreach (var context in contexts)
                LiveBuilderPriorityShortcutContexts.Register(context);

            isRegistered = true;
        }


        public void Unregister()
        {
            if (!isRegistered)
                return;

            foreach (var context in contexts)
                LiveBuilderPriorityShortcutContexts.Unregister(context);

            isRegistered = false;
        }
    }


    public enum LiveBuilderShortcutContextKind
    {
        Scene,
        Idle,
        Drawing,
        Contour,
        ContourPoints
    }


    /// <summary>
    /// Что сейчас в окне сцены — для правила контекстов (<see cref="LiveBuilderShortcutContexts.IsActive"/>).
    /// </summary>
    public struct LiveBuilderSceneKeyState
    {
        public bool IsToolActive;
        public bool IsSceneViewFocused;
        public bool IsEditingTextField;
        public bool IsOtherHandleDragged;
        public bool IsCameraFlying;
        public bool IsAnythingStarted;
        public bool IsToolBusy;
        public bool IsContourShown;
        public bool IsContourPointSelected;
    }


    /// <summary>
    /// Контекст ярлыков Live Builder: активен по правилу <see cref="LiveBuilderShortcutContexts.IsActive"/>; через него ярлык находит клавиатуру сцены.
    /// </summary>
    public abstract class LiveBuilderShortcutContext : IShortcutContext
    {
        protected LiveBuilderShortcutContext(LiveBuilderSceneKeyboard keyboard, LiveBuilderShortcutContextKind kind)
        {
            Keyboard = keyboard;
            Kind = kind;
        }


        public LiveBuilderSceneKeyboard Keyboard { get; }

        public LiveBuilderShortcutContextKind Kind { get; }

        // IShortcutContext
        public bool active => LiveBuilderShortcutContexts.IsActive(Kind, Keyboard.KeyState);


        public static LiveBuilderSceneKeyboard KeyboardOf(ShortcutArguments arguments)
        {
            return ((LiveBuilderShortcutContext)arguments.context).Keyboard;
        }
    }


    // Окно сцены в фокусе, Live Builder включён, не печатают в поле.
    public sealed class LiveBuilderSceneShortcutContext : LiveBuilderShortcutContext
    {
        public LiveBuilderSceneShortcutContext(LiveBuilderSceneKeyboard keyboard) : base(keyboard, LiveBuilderShortcutContextKind.Scene) { }
    }


    // Ничего не начато — цифры выбирают инструмент.
    public sealed class LiveBuilderIdleShortcutContext : LiveBuilderShortcutContext
    {
        public LiveBuilderIdleShortcutContext(LiveBuilderSceneKeyboard keyboard) : base(keyboard, LiveBuilderShortcutContextKind.Idle) { }
    }


    // Инструмент рисует — цифры и X набирают число.
    public sealed class LiveBuilderDrawingShortcutContext : LiveBuilderShortcutContext
    {
        public LiveBuilderDrawingShortcutContext(LiveBuilderSceneKeyboard keyboard) : base(keyboard, LiveBuilderShortcutContextKind.Drawing) { }
    }


    // Видны точки контура выделенной стены или здания, ничего не начато.
    public sealed class LiveBuilderContourShortcutContext : LiveBuilderShortcutContext
    {
        public LiveBuilderContourShortcutContext(LiveBuilderSceneKeyboard keyboard) : base(keyboard, LiveBuilderShortcutContextKind.Contour) { }
    }


    // Точки контура выбраны — стрелки их двигают; не выбраны — стрелки остаются камере Unity.
    public sealed class LiveBuilderContourPointsShortcutContext : LiveBuilderShortcutContext
    {
        public LiveBuilderContourPointsShortcutContext(LiveBuilderSceneKeyboard keyboard) : base(keyboard, LiveBuilderShortcutContextKind.ContourPoints) { }
    }
}
