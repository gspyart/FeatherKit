using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEngine;

// Конструкторы только с параметрами: без пустого конструктора меню «+» и поиск нод тестовые блоки не покажут.
namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Тестовый шаг: дописывает свою метку в общий журнал — по журналу тест видит, куда пошёл граф.</summary>
    [Serializable]
    public class ScenarioTestRecordAction : ScenarioInstantAction
    {
        public static readonly List<string> RecordedMarks = new List<string>();

        [SerializeField] private string mark;


        public ScenarioTestRecordAction(string mark)
        {
            this.mark = mark;
        }


        public string Mark => mark;

        public override string Summary => mark;


        protected override void Apply(ScenarioRun run)
        {
            RecordedMarks.Add(mark);
        }
    }


    /// <summary>Тестовое событие: срабатывает, когда тест зовёт Raise.</summary>
    [Serializable]
    public class ScenarioTestManualEvent : ScenarioEvent
    {
        [SerializeField] private string eventName;


        public ScenarioTestManualEvent(string eventName)
        {
            this.eventName = eventName;
        }


        public string EventName => eventName;

        public override string Summary => eventName;


        public void Raise(GameObject instigator = null)
        {
            Fire(instigator);
        }


        protected override void OnAttached()
        {
        }

        protected override void OnReleased()
        {
        }
    }


    /// <summary>Тестовое условие: верно, пока тест держит его включённым.</summary>
    [Serializable]
    public class ScenarioTestSwitchCondition : ScenarioCondition
    {
        [SerializeField] private bool isOn;


        public ScenarioTestSwitchCondition(bool isOn)
        {
            this.isOn = isOn;
        }


        public bool IsOn
        {
            get => isOn;
            set => isOn = value;
        }


        protected override bool Check(ScenarioRun run)
        {
            return isOn;
        }
    }


    /// <summary>Тестовая своя нода потока: выходы — приватные статические поля, как в примере из плана.</summary>
    [Serializable]
    public class ScenarioTestCoinFlipNode : ScenarioFlowNode
    {
        public const string HeadsId = "heads";
        public const string TailsId = "tails";

        private static readonly ScenarioOutput Heads = new ScenarioOutput(HeadsId, "Heads");
        private static readonly ScenarioOutput Tails = new ScenarioOutput(TailsId, "Tails");

        [ScenarioLabel("Heads Chance")]
        [Range(0f, 1f)]
        [SerializeField] private float headsChance;


        public ScenarioTestCoinFlipNode(float headsChance)
        {
            this.headsChance = headsChance;
        }


        public float HeadsChance => headsChance;


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var isHeads = headsChance >= 1f || UnityEngine.Random.value < headsChance;
            Fire(run, isHeads ? Heads : Tails);
            yield break;
        }
    }
}
