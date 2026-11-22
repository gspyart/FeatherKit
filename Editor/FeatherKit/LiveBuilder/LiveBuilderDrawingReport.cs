using System.Collections.Generic;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Один живой размер того, что рисуешь: подпись, метры и набран ли он с клавиатуры (тогда мышь его не меняет).</summary>
    public readonly struct LiveBuilderDrawnSize
    {
        public LiveBuilderDrawnSize(string label, float meters, bool isTyped)
        {
            Label = label;
            Meters = meters;
            IsTyped = isTyped;
        }


        public string Label { get; }

        public float Meters { get; }

        public bool IsTyped { get; }
    }


    /// <summary>
    /// Что рисуется сейчас — для секции Drawing панели (<see cref="LiveBuilderDrawingSection"/>), как поле размеров в SketchUp.
    /// - что рисуешь и шаг жеста: «Box, step 2 / 2 · Height»
    /// - живые размеры с подписями, набранное с клавиатуры, что сделают Enter и Esc
    /// Заполняет жест, который знает свои шаги и числа (<see cref="LiveBuilderTool.DrawingReport"/>); панель только рисует.
    /// </summary>
    public sealed class LiveBuilderDrawingReport
    {
        private readonly List<LiveBuilderDrawnSize> sizes = new();


        // stepCount 0 — шагов сколько угодно (точки линии): показывается только stepName.
        public LiveBuilderDrawingReport(string title, int stepNumber, int stepCount, string stepName)
        {
            Title = title;
            StepNumber = stepNumber;
            StepCount = stepCount;
            StepName = stepName;
        }


        public string Title { get; }

        public int StepNumber { get; }

        public int StepCount { get; }

        public string StepName { get; }

        public IReadOnlyList<LiveBuilderDrawnSize> Sizes => sizes;

        // Набранное как есть: «4×3×1,5»; ничего не набрано — пусто.
        public string TypedText { get; private set; } = "";

        // Что сделает Enter и Esc; клавиша ничего не делает — null.
        public string EnterResult { get; private set; }

        public string EscResult { get; private set; }


        // ── Заполнение ───────────────────────────────────────────────────

        public LiveBuilderDrawingReport WithSize(string label, float meters, bool isTyped = false)
        {
            sizes.Add(new LiveBuilderDrawnSize(label, meters, isTyped));
            return this;
        }


        public LiveBuilderDrawingReport WithTyped(string typedText)
        {
            TypedText = typedText ?? "";
            return this;
        }


        public LiveBuilderDrawingReport WithKeys(string enterResult, string escResult)
        {
            EnterResult = enterResult;
            EscResult = escResult;
            return this;
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // Отпечаток показанного: поменялся — панель перерисовывается (числа меняются на каждое движение мыши).
        public int Stamp()
        {
            var stamp = System.HashCode.Combine(Title, StepNumber, StepName, TypedText, EnterResult, EscResult);

            foreach (var size in sizes)
                stamp = System.HashCode.Combine(stamp, size.Label, size.Meters, size.IsTyped);

            return stamp;
        }


        // «4 × 3 × 1,5» — числа через знак умножения с пробелами, метры как в надписях сцены.
        public static string FormatSizes(params float[] meters)
        {
            var parts = new string[meters.Length];

            for (var index = 0; index < meters.Length; index++)
                parts[index] = LiveBuilderSceneText.FormatMeters(meters[index]);

            return string.Join(" × ", parts);
        }
    }
}
