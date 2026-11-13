using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Долгие эффекты, которые держит сценарий: звук по кругу, затемнение, тряска. Часть Scenario.
    /// - помнит, какой запуск и какая ветка взяли эффект
    /// - отпускает эффекты оборванного куска, остановленного запуска или все разом — на Stop и уничтожении
    /// </summary>
    public class ScenarioHeldEffects
    {
        private readonly List<HeldEffectRecord> heldEffects = new List<HeldEffectRecord>();
        private readonly Scenario owner;


        public ScenarioHeldEffects(Scenario owner)
        {
            this.owner = owner;
        }


        public int Count => heldEffects.Count;


        /// <param name="branch">Ветка, в которой эффект взят; null — вне веток, его отпустит только остановка.</param>
        public void Hold(IScenarioHeldEffect effect, ScenarioRun run, ScenarioBranch branch)
        {
            heldEffects.Add(new HeldEffectRecord(effect, run, branch));
        }


        // ── Отпускание ───────────────────────────────────────────────────

        /// <summary>Отпустить эффекты, взятые в ветках этого куска запуска.</summary>
        public void ReleaseInside(ScenarioFiring firing)
        {
            ReleaseWhere(record => record.Branch != null && record.Branch.IsInside(firing));
        }

        public void ReleaseTakenBy(ScenarioRun run)
        {
            ReleaseWhere(record => record.Run == run);
        }

        public void ReleaseAll()
        {
            ReleaseWhere(_ => true);
        }


        // Один упавший эффект не должен оставить остальные висеть: ошибку в лог — и дальше.
        private void ReleaseWhere(Predicate<HeldEffectRecord> shouldRelease)
        {
            var released = heldEffects.FindAll(shouldRelease);

            if (released.Count == 0)
                return;

            heldEffects.RemoveAll(shouldRelease);

            foreach (var record in released)
            {
                try
                {
                    record.Effect.Release();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, owner);
                }
            }
        }


        /// <summary>Кто и где взял долгий эффект.</summary>
        private readonly struct HeldEffectRecord
        {
            public HeldEffectRecord(IScenarioHeldEffect effect, ScenarioRun run, ScenarioBranch branch)
            {
                Effect = effect;
                Run = run;
                Branch = branch;
            }


            public IScenarioHeldEffect Effect { get; }

            public ScenarioRun Run { get; }

            public ScenarioBranch Branch { get; }
        }
    }
}
