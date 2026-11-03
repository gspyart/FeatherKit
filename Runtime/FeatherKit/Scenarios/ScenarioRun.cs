using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Один запуск сценария — то, что получает каждый его блок.
    /// - чей это сценарий и на каком объекте он стоит
    /// - кто его вызвал: вошедший в триггер, другой сценарий; может не быть никого
    /// - его ветки: основные шаги, ветки «In Parallel», фоновые шаги; запуск идёт, пока идёт хоть одна
    /// - первая ошибка шага в любой ветке обрывает весь запуск
    /// - общее правило блоков: пустое поле компонента — компонент на объекте сценария
    /// </summary>
    public class ScenarioRun
    {
        private readonly List<ScenarioBranch> branches = new List<ScenarioBranch>();


        public ScenarioRun(Scenario scenario, GameObject instigator)
        {
            Scenario = scenario;
            Instigator = instigator;
        }


        public Scenario Scenario { get; }

        /// <summary>Кто вызвал сценарий. Null — никто: старт сцены, кнопка, Timeline.</summary>
        public GameObject Instigator { get; }

        public GameObject Owner => Scenario.gameObject;

        /// <summary>Запуск оборван — остановили или шаг упал: ветки заканчивают, не начиная новых шагов.</summary>
        public bool IsStopped { get; private set; }

        /// <summary>Первая ошибка шага в любой ветке; null — ошибок не было.</summary>
        public Exception Failure { get; private set; }

        /// <summary>Шаг, на котором случилась ошибка, — самый вложенный из идущих. Null — ошибка вне шага.</summary>
        public ScenarioAction FailedAction { get; private set; }

        /// <summary>Ошибка случилась в фоновом шаге или внутри него.</summary>
        public bool HasFailedInBackground { get; private set; }

        /// <summary>Идёт хоть одна ветка: основные шаги, «In Parallel» или фоновый шаг.</summary>
        public bool HasRunningBranches => branches.Exists(branch => !branch.IsFinished);

        /// <summary>
        /// Идёт хоть один фоновый шаг. Ветку, которая спрашивает, и тех, из кого она выросла, не считаем:
        /// иначе «дождаться фоновых» внутри фонового шага ждал бы сам себя.
        /// </summary>
        public bool HasBackgroundSteps => branches.Exists(branch => branch.IsBackground && !branch.IsFinished
                                                                    && (ActiveBranch == null || !ActiveBranch.IsInside(branch)));

        /// <summary>Ветка, чей шаг выполняется прямо сейчас. Ставит сама ветка на время своего хода.</summary>
        internal ScenarioBranch ActiveBranch { get; set; }


        public T ComponentOrOwners<T>(T fieldValue) where T : Component
        {
            return fieldValue != null ? fieldValue : Owner.GetComponent<T>();
        }


        // ── Шаги ─────────────────────────────────────────────────────────

        /// <summary>
        /// Выполнить шаг: дождаться его или, если он не ждёт конца, пустить фоновой веткой.
        /// Так шаги зовут и сценарий, и вложенные столбики — блоку не нужно самому разбирать «Wait Until Done».
        /// </summary>
        public IEnumerator PerformStep(ScenarioAction action)
        {
            if (!action.WaitsForEnd)
            {
                StartBranch(PerformMarked(action), isBackground: true);
                yield break;
            }

            yield return PerformMarked(action);
        }

        // Пока шаг идёт, он отмечен в своей ветке: по отметкам инспектор подсвечивает блоки.
        private IEnumerator PerformMarked(ScenarioAction action)
        {
            var branch = ActiveBranch;

            branch?.MarkPerforming(action);
            yield return action.Perform(this);
            branch?.UnmarkPerforming(action);
        }


        /// <summary>Шаг сейчас идёт в какой-нибудь ветке запуска — для подсветки в инспекторе.</summary>
        public bool IsPerforming(ScenarioAction action)
        {
            return branches.Exists(branch => branch.IsPerforming(action));
        }


        // ── Ветки ────────────────────────────────────────────────────────

        /// <summary>Пустить шаги отдельной веткой — как «In Parallel». Ветку обрывает её Stop, обрыв запуска или ошибка.</summary>
        public ScenarioBranch StartBranch(IEnumerator steps)
        {
            return StartBranch(steps, isBackground: false);
        }

        // Дошедшие ветки выкидываем: повтор без конца иначе копил бы их весь запуск.
        private ScenarioBranch StartBranch(IEnumerator steps, bool isBackground)
        {
            branches.RemoveAll(branch => branch.IsFinished);

            var branch = new ScenarioBranch(this, ActiveBranch, isBackground);
            branches.Add(branch);
            branch.Begin(steps);

            return branch;
        }


        // ── Обрыв ────────────────────────────────────────────────────────

        /// <summary>Оборвать все ветки. Звук, Timeline и другой сценарий, начатые шагами, доигрывают сами.</summary>
        internal void Stop()
        {
            IsStopped = true;

            foreach (var branch in branches.ToArray())
                branch.Stop();
        }

        /// <summary>Шаг упал: запомнить первую ошибку. Ветки заканчивают сами, сценарий оборвёт остальное.</summary>
        internal void Fail(Exception exception, ScenarioAction failedAction, ScenarioBranch failedBranch)
        {
            if (Failure != null)
                return;

            Failure = exception;
            FailedAction = failedAction;
            HasFailedInBackground = failedBranch.IsInBackground;
            IsStopped = true;
        }
    }
}
