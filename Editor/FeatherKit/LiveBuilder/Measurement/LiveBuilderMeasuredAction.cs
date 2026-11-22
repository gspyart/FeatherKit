using System;
using System.Collections.Generic;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Действие замера с бюджетом из ТЗ — сдвиг коробки, сдвиг окна, число этажей — и его прогоны.
    /// Считает медиану, максимум и среднее по любому числу прогона: времени действия, кадра жеста, стадии.
    /// </summary>
    public sealed class LiveBuilderMeasuredAction
    {
        private readonly List<LiveBuilderMeasuredRun> runs = new();


        public LiveBuilderMeasuredAction(string name, string shortName, double budgetMilliseconds, bool isGesture)
        {
            Name = name;
            ShortName = shortName;
            BudgetMilliseconds = budgetMilliseconds;
            IsGesture = isGesture;
        }


        public string Name { get; }

        // Заголовок столбца в таблице стадий.
        public string ShortName { get; }

        public double BudgetMilliseconds { get; }

        // Тянут мышью — у прогона есть кадр жеста: шаг и отрисовка окна сцены.
        public bool IsGesture { get; }

        // Действие идёт секундами — прогонов меньше, чем просили.
        public bool IsShortened { get; set; }

        public IReadOnlyList<LiveBuilderMeasuredRun> Runs => runs;


        public void Add(LiveBuilderMeasuredRun run)
        {
            runs.Add(run);
        }


        // ── Статистика ───────────────────────────────────────────────────

        public double MedianOf(Func<LiveBuilderMeasuredRun, double> valueOf)
        {
            var values = runs.ConvertAll(run => valueOf(run));
            values.Sort();

            if (values.Count == 0)
                return 0.0;

            var middle = values.Count / 2;
            return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2.0;
        }


        public double MaxOf(Func<LiveBuilderMeasuredRun, double> valueOf)
        {
            var max = 0.0;

            foreach (var run in runs)
                max = Math.Max(max, valueOf(run));

            return max;
        }


        public double MeanOf(Func<LiveBuilderMeasuredRun, double> valueOf)
        {
            if (runs.Count == 0)
                return 0.0;

            var sum = 0.0;

            foreach (var run in runs)
                sum += valueOf(run);

            return sum / runs.Count;
        }
    }


    /// <summary>
    /// Один прогон действия: правка и проход очереди (по стадиям), отрисовка кадра после него, у жеста — его первый шаг и чистовой проход после отпускания,
    /// отложенная запись файлов на диск. Кадр жеста — правка с черновым проходом плюс отрисовка.
    /// </summary>
    public sealed class LiveBuilderMeasuredRun
    {
        public LiveBuilderMeasuredRun(LiveBuilderRebuildRecording pass, double renderMilliseconds, LiveBuilderRebuildRecording finalPass,
            LiveBuilderRebuildRecording diskSave, LiveBuilderRebuildRecording firstGestureStep)
        {
            Pass = pass;
            RenderMilliseconds = renderMilliseconds;
            FinalPass = finalPass;
            DiskSave = diskSave;
            FirstGestureStep = firstGestureStep;
        }


        public LiveBuilderRebuildRecording Pass { get; }

        // Первый шаг жеста — взяли ручку: заготовки ещё нет. Не жест — null.
        public LiveBuilderRebuildRecording FirstGestureStep { get; }
        public double RenderMilliseconds { get; }

        // Доводка после отпускания; не жест — null.
        public LiveBuilderRebuildRecording FinalPass { get; }

        public LiveBuilderRebuildRecording DiskSave { get; }

        public double ActionMilliseconds => Pass.ElapsedMilliseconds;
        public double FrameMilliseconds => Pass.ElapsedMilliseconds + RenderMilliseconds;
    }
}
