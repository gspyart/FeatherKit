using System;
using FeatherKit.Scenarios;
using UnityEngine;

// Конструкторы только с параметрами: без пустого конструктора меню «+» и поиск нод тестовые блоки не покажут.
namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Тестовое событие «стук»: слушает общую рассылку теста и срабатывает своим «случаем».</summary>
    [Serializable]
    public class ScenarioTestKnockEvent : ScenarioEvent
    {
        [SerializeField] private string knockName;


        public ScenarioTestKnockEvent(string knockName)
        {
            this.knockName = knockName;
        }


        /// <summary>Стук для всех подписанных — рассылка не FeatherKit, без своего «случая».</summary>
        public static event Action Knocked;


        public static void Knock()
        {
            Knocked?.Invoke();
        }


        protected override void OnAttached()
        {
            Knocked += HandleKnocked;
        }

        protected override void OnReleased()
        {
            Knocked -= HandleKnocked;
        }

        private void HandleKnocked()
        {
            FireAsOwnOccurrence(null);
        }
    }


    /// <summary>Тестовый шаг: кладёт позицию точки в переменную Vector3.</summary>
    [Serializable]
    public class ScenarioTestStorePointAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioPoint point;
        [SerializeField] private ScenarioVariableSlot<Vector3> slot;


        public ScenarioTestStorePointAction(ScenarioPoint point, ScenarioVariableSlot<Vector3> slot)
        {
            this.point = point;
            this.slot = slot;
        }


        protected override void Apply(ScenarioRun run)
        {
            if (!point.TryGetPosition(run, out var position))
                throw new InvalidOperationException("No point");

            slot.Set(run, position);
        }
    }


    /// <summary>Тестовый шаг: пишет в журнал вектор из поля «значение или переменная».</summary>
    [Serializable]
    public class ScenarioTestRecordVectorAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioValue<Vector3> vector;


        public ScenarioTestRecordVectorAction(ScenarioValue<Vector3> vector)
        {
            this.vector = vector;
        }


        protected override void Apply(ScenarioRun run)
        {
            ScenarioTestRecordAction.RecordedMarks.Add(vector.Get(run).ToString("0.0"));
        }
    }
}
