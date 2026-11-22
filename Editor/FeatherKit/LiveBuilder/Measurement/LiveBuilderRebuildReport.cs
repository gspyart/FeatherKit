using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Отчёт замера пересборки (<see cref="LiveBuilderRebuildMeasurement"/>) текстом, по-английски — для консоли и буфера обмена.
    /// - первая сборка уровня по стадиям, второй проход сразу за ней, запись сцены
    /// - бюджетные действия: медиана, максимум, кадр жеста (черновой проход) в кадрах в секунду, доводка после отпускания
    /// - куда уходит время каждого действия: стадии в среднем за прогон и счётчики
    /// </summary>
    public sealed class LiveBuilderRebuildReport
    {
        private const double FramesPerSecondBudget = 30.0;
        private const int NameWidth = 36;
        private const int NumberWidth = 11;

        private static readonly LiveBuilderRebuildStage[] Stages = (LiveBuilderRebuildStage[])Enum.GetValues(typeof(LiveBuilderRebuildStage));

        private readonly List<LiveBuilderMeasuredAction> actions = new();
        private readonly int requestedRunCount;
        private readonly StringBuilder text = new();


        public LiveBuilderRebuildReport(int requestedRunCount)
        {
            this.requestedRunCount = requestedRunCount;
        }


        public LiveBuilderRebuildRecording FirstBuild { get; set; }
        public LiveBuilderRebuildRecording SecondPass { get; set; }
        public LiveBuilderRebuildRecording SceneSave { get; set; }


        public void Add(LiveBuilderMeasuredAction action)
        {
            actions.Add(action);
        }


        public string ToText()
        {
            text.Clear();
            text.AppendLine("Live Builder rebuild measurement: reference level (9-storey section, 300-box yard)");
            text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ", Unity " + Application.unityVersion + ", "
                            + SystemInfo.processorType + ", " + SystemInfo.graphicsDeviceName);

            if (FirstBuild != null)
                AppendFirstBuild();

            if (actions.Count > 0)
            {
                AppendBudgets();
                AppendStagesOfActions();
            }

            return text.ToString();
        }


        // ── Первая сборка ────────────────────────────────────────────────

        private void AppendFirstBuild()
        {
            text.AppendLine();
            text.AppendLine("First build: one queue pass over the whole level");
            AppendRow("Stage", "Calls", "Self ms", "Total ms");

            foreach (var stage in Stages)
                if (FirstBuild.CallsOf(stage) > 0)
                    AppendRow(stage.ToString(), FirstBuild.CallsOf(stage).ToString(CultureInfo.InvariantCulture), Number(FirstBuild.SelfMillisecondsOf(stage)),
                        Number(FirstBuild.TotalMillisecondsOf(stage)));

            AppendRow("Outside stages", "", Number(FirstBuild.ElapsedMilliseconds - FirstBuild.StagesMilliseconds), "");
            AppendRow("Whole build", "", Number(FirstBuild.ElapsedMilliseconds), "");
            text.AppendLine("Rebuilt: constructions " + FirstBuild.RebuiltConstructionCount + ", building parts " + FirstBuild.RebuiltBuildingPartCount
                            + ", passes " + FirstBuild.PassCount);

            if (SecondPass != null)
                text.AppendLine("Second pass right after: " + Number(SecondPass.ElapsedMilliseconds) + " ms, rebuilt constructions " + SecondPass.RebuiltConstructionCount
                                + ", building parts " + SecondPass.RebuiltBuildingPartCount);

            if (SceneSave != null)
                text.AppendLine("Scene save (Ctrl+S): " + Number(SceneSave.ElapsedMilliseconds) + " ms, LightmapUV " + Number(SceneSave.TotalMillisecondsOf(LiveBuilderRebuildStage.LightmapUV))
                                + " ms, DiskSave " + Number(SceneSave.SelfMillisecondsOf(LiveBuilderRebuildStage.DiskSave)) + " ms, files written " + SceneSave.SavedFileCount);
        }


        // ── Бюджеты ──────────────────────────────────────────────────────

        private void AppendBudgets()
        {
            text.AppendLine();
            text.AppendLine("Budget actions, ms: edit + queue pass, a gesture step is a draft pass (median of " + requestedRunCount + " runs unless marked *)");
            AppendRow("Action", "Median", "Max", "Budget", "Runs");

            foreach (var action in actions)
                AppendRow(action.Name, Number(action.MedianOf(run => run.ActionMilliseconds)), Number(action.MaxOf(run => run.ActionMilliseconds)),
                    Number(action.BudgetMilliseconds), action.Runs.Count + (action.IsShortened ? "*" : ""));

            text.AppendLine();
            text.AppendLine("Scene view during a gesture: frame = gesture step + scene render, budget >= " + Number(FramesPerSecondBudget) + " fps;");
            text.AppendLine("the first step (handle grabbed) is measured apart: a moving opening has no base mesh yet");
            AppendRow("Gesture", "Frame ms", "Max ms", "Fps", "First step");

            foreach (var action in actions)
            {
                if (!action.IsGesture)
                    continue;

                var frame = action.MedianOf(run => run.FrameMilliseconds);
                AppendRow(action.Name, Number(frame), Number(action.MaxOf(run => run.FrameMilliseconds)), Number(frame > 0.0 ? 1000.0 / frame : 0.0),
                    Number(action.MedianOf(run => run.FirstGestureStep.ElapsedMilliseconds)));
                AppendRow("  of it scene render (1920 x 1080, GPU)", Number(action.MedianOf(run => run.RenderMilliseconds)), "", "");
            }

            if (actions.Exists(action => action.IsShortened))
                text.AppendLine("* a run takes over a second: fewer runs, the first one counts");

            AppendFinalPasses();
        }

        // Чистовой проход очередь делает через 0,3 с после отпускания: фаска, коллайдер, подпись.
        private void AppendFinalPasses()
        {
            text.AppendLine();
            text.AppendLine("After release: final pass (bevel, collider, signature), the queue runs it 0.3 s after the gesture");
            AppendRow("Gesture", "Median", "Max", "Bevel", "Collider");

            foreach (var action in actions)
                if (action.IsGesture)
                    AppendRow(action.Name, Number(action.MedianOf(run => run.FinalPass.ElapsedMilliseconds)), Number(action.MaxOf(run => run.FinalPass.ElapsedMilliseconds)),
                        Number(action.MedianOf(run => run.FinalPass.TotalMillisecondsOf(LiveBuilderRebuildStage.Bevel))),
                        Number(action.MedianOf(run => run.FinalPass.TotalMillisecondsOf(LiveBuilderRebuildStage.Collider))));
        }


        // ── Куда уходит время ────────────────────────────────────────────

        private void AppendStagesOfActions()
        {
            text.AppendLine();
            text.AppendLine("Where the time goes: mean per run, self ms (gestures: the draft pass)");

            var header = new List<string> { "Stage" };
            header.AddRange(actions.ConvertAll(action => action.ShortName));
            AppendRow(header.ToArray());

            foreach (var stage in Stages)
                if (actions.Exists(action => action.MeanOf(run => run.Pass.CallsOf(stage)) > 0.0))
                    AppendActionsRow(stage.ToString(), run => run.Pass.SelfMillisecondsOf(stage));

            AppendActionsRow("Outside stages", run => run.Pass.ElapsedMilliseconds - run.Pass.StagesMilliseconds);
            AppendActionsRow("Whole action", run => run.ActionMilliseconds);
            AppendActionsRow("Rebuilt constructions", run => run.Pass.RebuiltConstructionCount);
            AppendActionsRow("Rebuilt building parts", run => run.Pass.RebuiltBuildingPartCount);
            AppendActionsRow("Queue passes", run => run.Pass.PassCount);
            AppendActionsRow("Final pass after release, ms", run => run.FinalPass?.ElapsedMilliseconds ?? 0.0);
            AppendActionsRow("Deferred disk save, ms", run => run.DiskSave.ElapsedMilliseconds);
            AppendActionsRow("Deferred disk save, files", run => run.DiskSave.SavedFileCount);
        }

        private void AppendActionsRow(string name, Func<LiveBuilderMeasuredRun, double> valueOf)
        {
            var cells = new List<string> { name };
            cells.AddRange(actions.ConvertAll(action => Number(action.MeanOf(valueOf))));
            AppendRow(cells.ToArray());
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Первый столбец — слева, числа — справа.
        private void AppendRow(params string[] cells)
        {
            text.Append(cells[0].PadRight(NameWidth));

            for (var index = 1; index < cells.Length; index++)
                text.Append(cells[index].PadLeft(NumberWidth));

            text.AppendLine();
        }

        private static string Number(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
