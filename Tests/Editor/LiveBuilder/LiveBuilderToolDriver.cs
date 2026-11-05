using FeatherKit.EditorTools.LiveBuilder;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Нажимает инструмент Live Builder из проверок так же, как окно сцены (<see cref="LiveBuilderSceneTool"/>): нажатие, протягивание, отпускание,
    /// клавиши и Esc — лучами в мире, без окна сцены и камеры. Протягивание начинается, как в окне, только после нажатия; Esc, который инструмент
    /// не забрал, бросает начатое (<see cref="LiveBuilderTool.Cancel"/>). Мышью задавать глубину и высоту нельзя — у них нет камеры; их набирают цифрами.
    /// </summary>
    public sealed class LiveBuilderToolDriver
    {
        private readonly LiveBuilderTool tool;


        public LiveBuilderToolDriver(LiveBuilderTool tool)
        {
            this.tool = tool;
        }


        public bool IsBusy => tool.IsBusy;


        // ── Мышь ─────────────────────────────────────────────────────────

        // Клик: нажал и отпустил на месте. Возвращает, забрал ли инструмент нажатие.
        public bool Click(Ray worldRay, bool isShiftHeld = false)
        {
            var isTaken = tool.OnMouseDown(worldRay, MouseEvent(EventType.MouseDown, isShiftHeld));

            if (!isTaken)
                tool.OnMouseUp(MouseEvent(EventType.MouseUp, isShiftHeld));

            return isTaken;
        }


        // Протянул с зажатой кнопкой от одного луча до другого и отпустил.
        public void Drag(Ray fromRay, Ray toRay)
        {
            if (tool.OnMouseDown(fromRay, MouseEvent(EventType.MouseDown, false)))
                return;

            tool.OnMouseDrag(fromRay, toRay, true);
            tool.OnMouseUp(MouseEvent(EventType.MouseUp, false));
        }


        // Нажал и тянешь, кнопку не отпустил: пока держишь, можно набирать цифры (<see cref="Type"/>). Отпустить — <see cref="Release"/>.
        public void Hold(Ray fromRay, Ray toRay)
        {
            if (tool.OnMouseDown(fromRay, MouseEvent(EventType.MouseDown, false)))
                return;

            tool.OnMouseDrag(fromRay, toRay, true);
        }


        public void Release()
        {
            tool.OnMouseUp(MouseEvent(EventType.MouseUp, false));
        }


        public void Move(Ray worldRay)
        {
            tool.OnMouseMove(worldRay, MouseEvent(EventType.MouseMove, false));
        }


        // ── Клавиши ──────────────────────────────────────────────────────

        // Клавиша, как в окне сцены: сначала инструменту; Esc, который он не забрал, бросает начатое. Возвращает, забрал ли инструмент клавишу.
        public bool Press(KeyCode key)
        {
            if (tool.OnKeyDown(new Event { type = EventType.KeyDown, keyCode = key }))
                return true;

            if (key == KeyCode.Escape && tool.IsBusy)
                tool.Cancel();

            return false;
        }


        // Набрать число с клавиатуры: цифры, запятая, X между числами.
        public void Type(string text)
        {
            foreach (var character in text)
                Press(KeyOf(character));
        }

        private static KeyCode KeyOf(char character)
        {
            return character switch
            {
                >= '0' and <= '9' => KeyCode.Alpha0 + (character - '0'),
                ',' or '.' => KeyCode.Comma,
                'x' or 'X' or '×' => KeyCode.X,
                _ => KeyCode.None
            };
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static Event MouseEvent(EventType type, bool isShiftHeld)
        {
            return new Event { type = type, button = 0, shift = isShiftHeld, mousePosition = Vector2.zero };
        }
    }
}
