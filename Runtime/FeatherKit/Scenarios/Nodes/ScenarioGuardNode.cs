using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Guard While»: пускает Body и держит его, пока верно условие.
    /// - на входе условие ложно — сразу Broken
    /// - нарушилось — всё выросшее из Body обрывается, долгие эффекты Body плавно уходят, выход Broken
    /// - всё из Body кончилось само — выход Done
    /// - провод из Body назад в эту же ноду не заводит второго сторожа: Body просто идёт заново
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Guard While")]
    public class ScenarioGuardNode : ScenarioFlowNode, IScenarioLiveNode
    {
        public static readonly ScenarioOutput Body = new ScenarioOutput("body", "Body");
        public static readonly ScenarioOutput Broken = new ScenarioOutput("broken", "Broken");
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("How often to check the conditions while Body runs, in seconds. 0 — every frame")]
        [Min(0f)]
        [ScenarioLabel("Check Interval, s")]
        [ScenarioInspectorOnly]
        [SerializeField] private float checkInterval;

        // Идущие сторожа этой ноды: при Run Alongside их несколько, у каждого свой запуск.
        [NonSerialized] private List<GuardedBody> guardedBodies;


        public ScenarioConditionList Conditions => conditions;

        public float CheckInterval
        {
            get => checkInterval;
            set => checkInterval = Mathf.Max(0f, value);
        }


        // ── Сторож ───────────────────────────────────────────────────────

        // Body кончился в том же кадре, что нарушилось условие, — он успел: выход Done.
        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (IsEnteredFromOwnBody(run))
            {
                Fire(run, Body);
                yield break;
            }

            if (!conditions.IsMet(run))
            {
                Fire(run, Broken);
                yield break;
            }

            var guardedBody = BeginGuarding(run, Fire(run, Body));
            var schedule = new ScenarioCheckSchedule(checkInterval);
            schedule.IsDue(); // условие только что проверено на входе — интервал считаем отсюда

            while (guardedBody.Body.IsRunning)
            {
                yield return null;

                if (guardedBody.Body.IsRunning && schedule.IsDue() && !conditions.IsMet(run))
                {
                    guardedBodies.Remove(guardedBody);
                    guardedBody.Body.Cancel();
                    Fire(run, Broken);
                    yield break;
                }
            }

            guardedBodies.Remove(guardedBody);
            Fire(run, Done);
        }

        // Вошли изнутри своего же Body: этот Body уже сторожат — новый кусок вырастет внутри него.
        private bool IsEnteredFromOwnBody(ScenarioRun run)
        {
            if (guardedBodies == null || run.ActiveBranch == null)
                return false;

            foreach (var guardedBody in guardedBodies)
            {
                if (guardedBody.Run == run && run.ActiveBranch.IsInside(guardedBody.Body))
                    return true;
            }

            return false;
        }

        // Оборванный сторож до конца Perform не доходит — убираем его по концу ветки.
        private GuardedBody BeginGuarding(ScenarioRun run, ScenarioFiring body)
        {
            guardedBodies ??= new List<GuardedBody>();

            var guardedBody = new GuardedBody(run, body);
            guardedBodies.Add(guardedBody);
            run.ActiveBranch?.CallWhenFinished(() => guardedBodies.Remove(guardedBody));

            return guardedBody;
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            guardedBodies?.Clear();
        }


        /// <summary>Один идущий сторож: чей запуск и какой кусок Body он держит.</summary>
        private class GuardedBody
        {
            public GuardedBody(ScenarioRun run, ScenarioFiring body)
            {
                Run = run;
                Body = body;
            }


            public ScenarioRun Run { get; }

            public ScenarioFiring Body { get; }
        }
    }
}
