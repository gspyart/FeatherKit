using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Подсветка графа в игре — часть ScenarioGraphView: какие ноды сейчас идут и по какому проводу только что прошёл запуск.
    /// - чей граф смотрим: Scenario, через который он открыт; у граф-файла — выбранный в шапке «Debug: …»
    /// - раз в 100 мс спрашивает Scenario.IsPerforming(nodeId); провод вспыхивает на 0,3 с по WireFired
    /// </summary>
    internal class ScenarioRunHighlighter
    {
        public const string RunningNodeClass = "scenario-node--running";
        public const string FiredWireClass = "scenario-wire--fired";

        private const long PollMilliseconds = 100;
        private const long WireFlashMilliseconds = 300;

        private readonly ScenarioGraphView graphView;
        private Scenario target;
        private bool isChosenByHand;


        public ScenarioRunHighlighter(ScenarioGraphView graphView)
        {
            this.graphView = graphView;
            graphView.schedule.Execute(RefreshRunningNodes).Every(PollMilliseconds);
        }


        /// <summary>Чей идущий граф подсвечивать. Null — никого: вне игры или граф-файл без выбранного объекта.</summary>
        public Scenario Target
        {
            get => target;
            set
            {
                if (target == value)
                    return;

                if (target != null)
                    target.WireFired -= FlashWire;

                target = value;

                if (target != null)
                    target.WireFired += FlashWire;

                RefreshRunningNodes();
            }
        }


        // ── Кого подсвечивать ────────────────────────────────────────────

        /// <summary>Объекты в игре, исполняющие этот граф-файл, — для выбора в шапке.</summary>
        public static List<Scenario> ScenariosRunning(ScenarioGraphAsset graphFile)
        {
            return Scenario.ActiveScenarios
                .Where(scenario => scenario != null && scenario.GraphSource == ScenarioGraphSource.Asset && scenario.GraphAsset == graphFile)
                .ToList();
        }

        /// <summary>Кого смотреть по умолчанию: сценарий, через который открыт граф, — если граф открыт в игре.</summary>
        public void ChooseDefaultTarget()
        {
            var host = graphView.Host;
            isChosenByHand = false;
            Target = EditorApplication.isPlaying && host is { IsAlive: true } ? host.Scenario : null;
        }

        /// <summary>Выбор в шапке «Debug: …»: этот объект и держим, пока не выберут другой или не откроют другой граф.</summary>
        public void ChooseByHand(Scenario scenario)
        {
            isChosenByHand = true;
            Target = scenario;
        }


        // ── Подсветка ────────────────────────────────────────────────────

        // После пересборки холста классы теряются — опрос раз в 100 мс их и возвращает.
        private void RefreshRunningNodes()
        {
            if (EditorApplication.isPlaying && target == null && !isChosenByHand)
                ChooseDefaultTarget();

            var isWatching = EditorApplication.isPlaying && target != null;

            foreach (var nodeView in graphView.nodes.OfType<ScenarioNodeView>())
                nodeView.EnableInClassList(RunningNodeClass, isWatching && target.IsPerforming(nodeView.NodeId));
        }

        private void FlashWire(int nodeId, string outputId)
        {
            foreach (var edge in graphView.edges.Where(edge => edge.userData is ScenarioWire wire
                                                                && wire.FromNodeId == nodeId && wire.FromPortId == outputId))
            {
                edge.AddToClassList(FiredWireClass);
                edge.schedule.Execute(() => edge.RemoveFromClassList(FiredWireClass)).ExecuteLater(WireFlashMilliseconds);
            }
        }
    }
}
