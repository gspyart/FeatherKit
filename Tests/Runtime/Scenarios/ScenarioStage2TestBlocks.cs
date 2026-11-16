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


    /// <summary>Тестовый шаг: пишет в журнал имя объекта из переменной-слота — для блоков, что кладут результат в переменную.</summary>
    [Serializable]
    public class ScenarioTestRecordSlotAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioVariableSlot<UnityEngine.Object> slot;


        public ScenarioTestRecordSlotAction(ScenarioVariableSlot<UnityEngine.Object> slot)
        {
            this.slot = slot;
        }


        protected override void Apply(ScenarioRun run)
        {
            var value = slot.Get(run);
            ScenarioTestRecordAction.RecordedMarks.Add(value != null ? value.name : "none");
        }
    }


    /// <summary>Тестовый шаг: пишет в журнал число из переменной Counter или Number.</summary>
    [Serializable]
    public class ScenarioTestRecordNumberAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioValue<float> number;


        public ScenarioTestRecordNumberAction(ScenarioValue<float> number)
        {
            this.number = number;
        }


        protected override void Apply(ScenarioRun run)
        {
            ScenarioTestRecordAction.RecordedMarks.Add(number.Get(run).ToString("0.##"));
        }
    }


    /// <summary>Тестовый шаг: пишет в журнал число из переменной Counter.</summary>
    [Serializable]
    public class ScenarioTestRecordCounterAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioValue<int> counter;


        public ScenarioTestRecordCounterAction(ScenarioValue<int> counter)
        {
            this.counter = counter;
        }


        protected override void Apply(ScenarioRun run)
        {
            ScenarioTestRecordAction.RecordedMarks.Add(counter.Get(run).ToString());
        }
    }


    /// <summary>Тестовый шаг: зовёт код теста с запуском — для частей, у которых нет своего блока.</summary>
    [Serializable]
    public class ScenarioTestCallbackAction : ScenarioInstantAction
    {
        [NonSerialized] private Action<ScenarioRun> callback;


        public ScenarioTestCallbackAction(Action<ScenarioRun> callback)
        {
            this.callback = callback;
        }


        protected override void Apply(ScenarioRun run)
        {
            callback(run);
        }
    }


    /// <summary>Тестовый шаг: читает список типа T из слота — проверка, что так читать нельзя.</summary>
    [Serializable]
    public class ScenarioTestReadTypedListAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioVariableSlot<System.Collections.Generic.List<Light>> slot;


        public ScenarioTestReadTypedListAction(ScenarioVariableSlot<System.Collections.Generic.List<Light>> slot)
        {
            this.slot = slot;
        }


        protected override void Apply(ScenarioRun run)
        {
            slot.Get(run);
        }
    }
}
