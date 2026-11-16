using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Repeat»: пускает Each Time и ждёт всё выросшее, снова и снова; затем Finished.
    /// - сколько раз: N раз, пока сошлись условия (проверка перед витком) или бесконечно
    /// - вход Stop обрывает идущие на ноде повторы: виток обрывается, повтор уходит в Finished
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Repeat", Description = "Run “Each Time” again and again, then go to Finished")]
    public class ScenarioRepeatNode : ScenarioFlowNode, IScenarioLiveNode
    {
        public static readonly ScenarioInput Stop = new ScenarioInput("stop", "Stop");
        public static readonly ScenarioOutput EachTime = new ScenarioOutput("eachTime", "Each Time");
        public static readonly ScenarioOutput Finished = new ScenarioOutput("finished", "Finished");

        [ScenarioLabel("Repeat")]
        [SerializeField] private ScenarioRepeatMode mode;

        [ScenarioLabel("Times")]
        [Min(1)]
        [SerializeField] private int repeatCount = 3;

        [Tooltip("For “While”: checked before each round")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        // Идущие повторы: при Run Alongside на одной ноде крутятся несколько запусков.
        [NonSerialized] private List<RepeatNodeLoop> loops;


        public ScenarioRepeatMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public int RepeatCount
        {
            get => repeatCount;
            set => repeatCount = Mathf.Max(1, value);
        }

        public ScenarioConditionList Conditions => conditions;

        public override string Summary => mode switch
        {
            ScenarioRepeatMode.Times => $"{repeatCount} times",
            ScenarioRepeatMode.While => "while conditions are met",
            _ => "forever"
        };


        // ── Повтор ───────────────────────────────────────────────────────

        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (input == Stop)
            {
                StopAllLoops();
                yield break;
            }

            var loop = BeginLoop(run);

            for (var round = 0; ShouldRepeat(run, loop, round); round++)
            {
                loop.CurrentRound = Fire(run, EachTime);

                // Виток без проводов мгновенный: бесконечный повтор без ожидания ловим тем же счётчиком, что провода.
                if (!loop.CurrentRound.HasBranches)
                    run.CountInstantTransition();

                yield return loop.CurrentRound.WaitUntilDone();
            }

            loops.Remove(loop);
            Fire(run, Finished);
        }

        private bool ShouldRepeat(ScenarioRun run, RepeatNodeLoop loop, int round)
        {
            if (loop.IsStopRequested || run.IsStopped)
                return false;

            return mode switch
            {
                ScenarioRepeatMode.Times => round < repeatCount,
                ScenarioRepeatMode.While => conditions.IsMet(run),
                _ => true
            };
        }


        // Обрыв ветки повтора не доходит до конца Perform — убираем повтор из списка по концу ветки.
        private RepeatNodeLoop BeginLoop(ScenarioRun run)
        {
            loops ??= new List<RepeatNodeLoop>();

            var loop = new RepeatNodeLoop();
            loops.Add(loop);
            run.ActiveBranch?.CallWhenFinished(() => loops.Remove(loop));

            return loop;
        }


        // Все повторы ноды, а не только своего запуска: «игрок ушёл — хватит мигать» приходит другим запуском.
        private void StopAllLoops()
        {
            if (loops == null)
                return;

            foreach (var loop in loops.ToArray())
            {
                loop.IsStopRequested = true;
                loop.CurrentRound?.Cancel();
            }
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            loops?.Clear();
        }


        /// <summary>Один идущий повтор на ноде: идущий виток и просили ли остановиться.</summary>
        private class RepeatNodeLoop
        {
            public ScenarioFiring CurrentRound { get; set; }

            public bool IsStopRequested { get; set; }
        }
    }


    /// <summary>Сколько раз повторять.</summary>
    public enum ScenarioRepeatMode
    {
        [InspectorName("Times")] Times,
        [InspectorName("While")] While,
        [InspectorName("Forever")] Forever
    }
}
