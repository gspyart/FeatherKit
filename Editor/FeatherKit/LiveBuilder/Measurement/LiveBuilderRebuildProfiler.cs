using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Замер пересборки Live Builder по стадиям (<see cref="LiveBuilderRebuildStage"/>): у каждой стадии маркер профайлера Unity — виден всегда.
    /// Пока идёт запись — копит по стадиям число вызовов и время: своё (без вложенных стадий) и полное;
    /// и счётчики: проходы очереди, пересобранные постройки и части зданий, записанные на диск файлы мешей.
    /// </summary>
    public static class LiveBuilderRebuildProfiler
    {
        private static readonly int StageCount = Enum.GetValues(typeof(LiveBuilderRebuildStage)).Length;
        private static readonly ProfilerMarker[] Markers = CreateMarkers();

        private static readonly int[] CallsByStage = new int[StageCount];
        private static readonly long[] SelfTicksByStage = new long[StageCount];
        private static readonly long[] TotalTicksByStage = new long[StageCount];

        // Сколько раз стадия открыта сейчас: вложенный вызов той же стадии к полному времени не прибавляется второй раз.
        private static readonly int[] OpenScopesByStage = new int[StageCount];

        // Время вложенных стадий у каждой открытой — по порядку открытия; вычитается из её своего времени.
        private static readonly List<long> ChildTicksOfOpenScopes = new();

        private static bool isRecording;
        private static int recordingNumber;
        private static long recordingStartTicks;


        public static bool IsRecording => isRecording;

        // Счётчики с начала записи.
        public static int PassCount { get; private set; }
        public static int RebuiltConstructionCount { get; private set; }
        public static int RebuiltBuildingPartCount { get; private set; }
        public static int SavedFileCount { get; private set; }


        // ── Запись ───────────────────────────────────────────────────────

        public static void BeginRecording()
        {
            recordingNumber++;
            Array.Clear(CallsByStage, 0, StageCount);
            Array.Clear(SelfTicksByStage, 0, StageCount);
            Array.Clear(TotalTicksByStage, 0, StageCount);
            Array.Clear(OpenScopesByStage, 0, StageCount);
            ChildTicksOfOpenScopes.Clear();

            PassCount = 0;
            RebuiltConstructionCount = 0;
            RebuiltBuildingPartCount = 0;
            SavedFileCount = 0;

            recordingStartTicks = Stopwatch.GetTimestamp();
            isRecording = true;
        }


        public static LiveBuilderRebuildRecording EndRecording()
        {
            isRecording = false;
            var elapsed = ToMilliseconds(Stopwatch.GetTimestamp() - recordingStartTicks);
            var selfMilliseconds = new double[StageCount];
            var totalMilliseconds = new double[StageCount];

            for (var index = 0; index < StageCount; index++)
            {
                selfMilliseconds[index] = ToMilliseconds(SelfTicksByStage[index]);
                totalMilliseconds[index] = ToMilliseconds(TotalTicksByStage[index]);
            }

            return new LiveBuilderRebuildRecording(elapsed, (int[])CallsByStage.Clone(), selfMilliseconds, totalMilliseconds,
                PassCount, RebuiltConstructionCount, RebuiltBuildingPartCount, SavedFileCount);
        }


        // ── Стадии ───────────────────────────────────────────────────────

        // using (LiveBuilderRebuildProfiler.Measure(stage)) { … } — маркер профайлера всегда, время — только пока идёт запись.
        public static LiveBuilderStageScope Measure(LiveBuilderRebuildStage stage)
        {
            Markers[(int)stage].Begin();

            if (!isRecording)
                return new LiveBuilderStageScope(stage, 0, 0);

            OpenScopesByStage[(int)stage]++;
            ChildTicksOfOpenScopes.Add(0);
            return new LiveBuilderStageScope(stage, recordingNumber, Stopwatch.GetTimestamp());
        }


        // Стадия, открытая до начала записи или в прошлой записи, время не пишет.
        internal static void End(LiveBuilderRebuildStage stage, int scopeRecordingNumber, long startTicks)
        {
            Markers[(int)stage].End();

            if (!isRecording || scopeRecordingNumber != recordingNumber || ChildTicksOfOpenScopes.Count == 0)
                return;

            var index = (int)stage;
            var elapsed = Stopwatch.GetTimestamp() - startTicks;
            var last = ChildTicksOfOpenScopes.Count - 1;

            SelfTicksByStage[index] += elapsed - ChildTicksOfOpenScopes[last];
            ChildTicksOfOpenScopes.RemoveAt(last);

            if (last > 0)
                ChildTicksOfOpenScopes[last - 1] += elapsed;

            CallsByStage[index]++;

            if (--OpenScopesByStage[index] == 0)
                TotalTicksByStage[index] += elapsed;
        }


        // ── Счётчики ─────────────────────────────────────────────────────

        public static void CountPass()
        {
            if (isRecording)
                PassCount++;
        }


        public static void CountRebuiltConstruction()
        {
            if (isRecording)
                RebuiltConstructionCount++;
        }


        public static void CountRebuiltBuildingPart()
        {
            if (isRecording)
                RebuiltBuildingPartCount++;
        }


        public static void CountSavedFile()
        {
            if (isRecording)
                SavedFileCount++;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static ProfilerMarker[] CreateMarkers()
        {
            var markers = new ProfilerMarker[StageCount];

            for (var index = 0; index < StageCount; index++)
                markers[index] = new ProfilerMarker("LiveBuilder." + (LiveBuilderRebuildStage)index);

            return markers;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }


    /// <summary>Открытая стадия замера: закрывается в конце using. Создаёт только <see cref="LiveBuilderRebuildProfiler.Measure"/>.</summary>
    public readonly struct LiveBuilderStageScope : IDisposable
    {
        private readonly LiveBuilderRebuildStage stage;
        private readonly int recordingNumber;
        private readonly long startTicks;


        internal LiveBuilderStageScope(LiveBuilderRebuildStage stage, int recordingNumber, long startTicks)
        {
            this.stage = stage;
            this.recordingNumber = recordingNumber;
            this.startTicks = startTicks;
        }


        // IDisposable
        public void Dispose()
        {
            LiveBuilderRebuildProfiler.End(stage, recordingNumber, startTicks);
        }
    }
}
