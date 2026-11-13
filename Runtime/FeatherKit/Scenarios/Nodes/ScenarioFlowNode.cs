using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// База ноды потока — своей ноды на C#: Branch, Wait For, Repeat, Coin Flip.
    /// - вход In; выходы — статические поля ScenarioOutput или DescribePorts
    /// - Perform решает, куда пойти: Fire — пустить и не ждать, FireAndWait — дождаться всего выросшего
    /// </summary>
    [Serializable]
    public abstract class ScenarioFlowNode : ScenarioNode
    {
        public static readonly ScenarioInput In = new ScenarioInput("in", "In");


        // ── Ход ноды ─────────────────────────────────────────────────────

        /// <summary>Рантайм входит в ноду только так: Perform защищён, чтобы ноду не дёргали в обход запуска.</summary>
        internal IEnumerator Enter(ScenarioRun run, ScenarioInput input)
        {
            return Perform(run, input);
        }


        /// <summary>Работа ноды. Может длиться кадрами; нода кончилась — её ветка кончилась.</summary>
        protected abstract IEnumerator Perform(ScenarioRun run, ScenarioInput input);


        // ── Выходы ───────────────────────────────────────────────────────

        /// <summary>Пустить провода выхода и идти дальше. Вернёт кусок запуска — его можно дождаться или оборвать.</summary>
        protected ScenarioFiring Fire(ScenarioRun run, ScenarioOutput output)
        {
            return run.Fire(this, output);
        }

        /// <summary>Пустить провода выхода и дождаться всего, что из них выросло: на этом Repeat, Sequence, Parallel.</summary>
        protected IEnumerator FireAndWait(ScenarioRun run, ScenarioOutput output)
        {
            return Fire(run, output).WaitUntilDone();
        }
    }


    /// <summary>Расписание проверки «раз в N секунд»: 0 — каждый кадр. Первая проверка — сразу.</summary>
    public class ScenarioCheckSchedule
    {
        private readonly float interval;
        private float nextCheckTime = float.NegativeInfinity;


        public ScenarioCheckSchedule(float interval)
        {
            this.interval = Mathf.Max(0f, interval);
        }


        /// <summary>Пора проверять? Ответ «да» сразу отсчитывает следующий интервал.</summary>
        public bool IsDue()
        {
            if (Time.time < nextCheckTime)
                return false;

            nextCheckTime = Time.time + interval;

            return true;
        }
    }
}
