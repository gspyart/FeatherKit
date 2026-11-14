using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Gate»: пропускает вход Enter в Out, пока открыта; Open, Close и Toggle её открывают и закрывают.
    /// Открыта ли — общее для ноды, а не для запуска: все запуски видят одни ворота. Stop возвращает начальное.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Gate", Description = "Let the flow through only while the gate is open")]
    public class ScenarioGateNode : ScenarioFlowNode, IScenarioLiveNode
    {
        public static readonly ScenarioInput Enter = new ScenarioInput("in", "Enter");
        public static readonly ScenarioInput Open = new ScenarioInput("open", "Open");
        public static readonly ScenarioInput Close = new ScenarioInput("close", "Close");
        public static readonly ScenarioInput Toggle = new ScenarioInput("toggle", "Toggle");
        public static readonly ScenarioOutput Out = new ScenarioOutput("out", "Out");

        [ScenarioLabel("Starts Open")]
        [SerializeField] private bool startsOpen = true;

        [NonSerialized] private bool hasLiveState;
        [NonSerialized] private bool isOpenNow;


        public bool StartsOpen
        {
            get => startsOpen;
            set => startsOpen = value;
        }

        public bool IsOpen => hasLiveState ? isOpenNow : startsOpen;


        // Вход Enter — тот же id «in», что у любой ноды потока: провода к In не рвутся.
        public override void DescribePorts(ScenarioPortList ports)
        {
            ports.AddInput(Enter);
            ports.AddInput(Open);
            ports.AddInput(Close);
            ports.AddInput(Toggle);
            ports.AddOutput(Out);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (input == Open)
                SetOpen(true);
            else if (input == Close)
                SetOpen(false);
            else if (input == Toggle)
                SetOpen(!IsOpen);
            else if (IsOpen)
                Fire(run, Out);

            yield break;
        }

        private void SetOpen(bool isOpen)
        {
            hasLiveState = true;
            isOpenNow = isOpen;
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            hasLiveState = false;
        }
    }
}
