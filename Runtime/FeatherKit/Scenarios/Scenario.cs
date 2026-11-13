using System;
using System.Collections.Generic;
using FeatherKit.Attributes;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Сценарий без кода: граф, где ноды When начинают запуски, а провода ведут по нодам Do и нодам потока.
    /// - граф встроенный или из файла; файл копируется на объект — у каждого свои переменные и Only Once
    /// - слушает события нод When, пока включён; Stop обрывает запуски и плавно отпускает долгие эффекты
    /// - пустой граф — старый режим: события, условия и шаги списками
    /// - пересылает блокам Start и триггеры; сообщает журналу и подсветке, что идёт и чем кончилось
    /// </summary>
    [FDescription("A scenario without code: “When” nodes start runs, wires lead them through “Do” steps and flow nodes. With an empty graph the old lists work: When → If → Do.")]
    public class Scenario : MonoBehaviour, IScenarioEventListener
    {
        private static readonly List<Scenario> activeScenarios = new List<Scenario>();

        [SerializeReference] private List<ScenarioEvent> events = new List<ScenarioEvent>();

        [SerializeField] private ScenarioConditionsMode conditionsMode;
        [SerializeReference] private List<ScenarioCondition> conditions = new List<ScenarioCondition>();

        [SerializeReference] private List<ScenarioAction> actions = new List<ScenarioAction>();

        [Tooltip("Run only once; after that, events are ignored")]
        [SerializeField] private bool runsOnce;

        // Граф сценария: новые поля рядом со старыми списками, пока старый рантайм жив.
        [SerializeField] private ScenarioGraphSource graphSource;
        [SerializeField] private ScenarioGraph graph = new ScenarioGraph();
        [SerializeField] private ScenarioGraphAsset graphAsset;
        [SerializeField] private List<ScenarioVariableBinding> variableBindings = new List<ScenarioVariableBinding>();

        [Tooltip("Permanent key of this scenario for checkpoints and saves")]
        [SerializeField] private string persistentId;

        // Своя копия граф-файла: состояние нод и переменные не должны делиться между объектами.
        [NonSerialized] private ScenarioGraphAsset graphAssetCopy;
        [NonSerialized] private ScenarioGraph runningGraph;

        private ScenarioWiring wiring;
        private ScenarioRuns runs;
        private ScenarioGraphVariables graphVariables;
        private ScenarioHeldEffects heldEffects;
        private ScenarioTriggerOccupants triggerOccupants;
        private ScenarioListRunner listRunner;
        private bool hasWarnedDisabledCall;


        /// <summary>Чем кончилась попытка запуска любого сценария — для журнала в редакторе.</summary>
        public static event Action<ScenarioRunRecord> RunReported;


        /// <summary>Объект проснулся впервые — Unity-событие Start.</summary>
        public event Action Started;

        public event Action<Collider> TriggerEntered;

        public event Action<Collider> TriggerExited;

        /// <summary>Сработал выход ноды графа: id ноды и id выхода. Для подсветки проводов в игре.</summary>
        public event Action<int, string> WireFired;

        /// <summary>Сценарий позвали — TryRun, Run или нода Run Scenario; слушает событие Called.</summary>
        internal event Action<GameObject> Called;


        public bool IsRunning => (listRunner != null && listRunner.IsRunning) || (runs != null && runs.HasRunningRuns);

        public bool HasRun => (listRunner != null && listRunner.HasRun) || (runs != null && runs.HasStarted);

        /// <summary>Номер идущего основного шага старых списков; -1 — сценарий стоит или доигрывают только фоновые шаги.</summary>
        public int CurrentActionIndex => listRunner != null ? listRunner.CurrentActionIndex : -1;

        public IReadOnlyList<ScenarioEvent> Events => events;


        // ── Граф ─────────────────────────────────────────────────────────

        /// <summary>Включённые сейчас сценарии — для окна сценариев и подсветки.</summary>
        public static IReadOnlyList<Scenario> ActiveScenarios => activeScenarios;

        public ScenarioGraphSource GraphSource => graphSource;

        /// <summary>Граф, который исполняет сценарий: встроенный или из файла, в игре — своя копия файла. Null — файл не выбран.</summary>
        public ScenarioGraph Graph
        {
            get
            {
                return runningGraph ?? SourceGraph;
            }
        }

        public ScenarioGraphAsset GraphAsset => graphAsset;

        /// <summary>Значения открытых переменных графа на этом объекте.</summary>
        public IReadOnlyList<ScenarioVariableBinding> VariableBindings => variableBindings;

        public string PersistentId => persistentId;

        // IScenarioEventListener
        Scenario IScenarioEventListener.Scenario => this;


        // ── Части ────────────────────────────────────────────────────────

        internal ScenarioWiring Wiring => wiring;

        internal ScenarioRuns Runs => runs;

        internal ScenarioGraphVariables GraphVariables => graphVariables;

        internal ScenarioHeldEffects HeldEffects => heldEffects;

        // Старые списки — для части ScenarioListRunner: поля остаются здесь, чтобы не сбросить данные в сценах.
        internal List<ScenarioAction> ListActions => actions;

        internal List<ScenarioCondition> ListConditions => conditions;

        internal ScenarioConditionsMode ListConditionsMode => conditionsMode;

        internal bool ListRunsOnce => runsOnce;

        /// <summary>Коллайдеры, которые сейчас внутри триггеров объекта, — для блоков, подписавшихся позже входа.</summary>
        internal IReadOnlyCollection<Collider> CollidersInside => triggerOccupants != null
            ? triggerOccupants.CollidersInside
            : Array.Empty<Collider>();

        /// <summary>Сценарий исполняет граф; пустой граф — живёт старыми списками. По данным: верно и до Awake.</summary>
        internal bool UsesGraph
        {
            get
            {
                var usedGraph = runningGraph ?? SourceGraph;

                return usedGraph != null && usedGraph.Nodes.Count > 0;
            }
        }

        private ScenarioGraph SourceGraph => graphSource == ScenarioGraphSource.Asset
            ? graphAsset != null ? graphAsset.Graph : null
            : graph;


        // ── Запуск ───────────────────────────────────────────────────────

        /// <summary>
        /// Запустить. Граф: сработают ноды When с событием Called; true — хоть одна пошла в Then.
        /// Старые списки: false — уже идёт, уже отработал разовый или условия не сошлись.
        /// </summary>
        public bool TryRun(GameObject instigator = null)
        {
            if (!isActiveAndEnabled)
            {
                WarnDisabledCallOnce();
                return false;
            }

            if (UsesGraph)
                return CallGraph(instigator, null);

            return listRunner != null && listRunner.TryRun(instigator);
        }

        // Выключенный сценарий зовут часто — каждый кадр из Update или Timeline; одной строки в консоли хватит.
        private void WarnDisabledCallOnce()
        {
            if (hasWarnedDisabledCall)
                return;

            hasWarnedDisabledCall = true;
            FLog.Warning(this, ("Scenario ", FLogColor.Bad), (name, FLogColor.Value), (" is disabled — not called", FLogColor.Bad));
        }

        /// <summary>Позвать граф: сработают ноды When с событием Called. Начатые запуски — в startedRuns, если он дан.</summary>
        internal bool CallGraph(GameObject instigator, List<ScenarioRun> startedRuns)
        {
            if (!isActiveAndEnabled || runs == null)
                return false;

            startedRuns ??= new List<ScenarioRun>();
            var countBefore = startedRuns.Count;

            runs.CollectStartedRuns(startedRuns, () => RaiseInOwnOccurrence(Called, instigator));

            return startedRuns.Count > countBefore;
        }

        /// <summary>Запуск для кнопок UnityEvent и сигналов Timeline: им нужен метод без результата.</summary>
        public void Run()
        {
            TryRun();
        }


        /// <summary>
        /// Оборвать все запуски, фоновые шаги тоже, и плавно отпустить долгие эффекты. Начатое шагами
        /// на других объектах — звук, Timeline, другой сценарий — доигрывает само.
        /// </summary>
        public void Stop()
        {
            try
            {
                listRunner?.Stop();
                runs?.StopAll();
            }
            finally
            {
                ResetLiveNodes();
                heldEffects?.ReleaseAll();
            }
        }

        // Запуски уже оборваны; то, что ноды держат сверх них, — подписки и списки ожиданий — сбрасываем тоже.
        private void ResetLiveNodes()
        {
            if (runningGraph == null)
                return;

            foreach (var node in runningGraph.Nodes)
                (node as IScenarioLiveNode)?.ResetLiveState();
        }

        // ── Подсветка и журнал ───────────────────────────────────────────

        /// <summary>Шаг сейчас идёт — основной, вложенный или фоновый. Для подсветки в инспекторе.</summary>
        public bool IsPerforming(ScenarioAction action)
        {
            return listRunner != null && listRunner.IsPerforming(action);
        }

        /// <summary>Нода графа сейчас идёт хоть в одном запуске. Для подсветки на холсте.</summary>
        public bool IsPerforming(int nodeId)
        {
            return runs != null && runs.IsPerforming(nodeId);
        }


        internal void ReportWireFired(int nodeId, string portId)
        {
            WireFired?.Invoke(nodeId, portId);
        }

        internal void ReportRun(ScenarioRunRecord record)
        {
            RunReported?.Invoke(record);
        }


        // Рассылка — свой «случай»: ожидание, начатое из-за неё, её же не засчитывает.
        private static void RaiseInOwnOccurrence<T>(Action<T> handlers, T argument)
        {
            if (handlers == null)
                return;

            ScenarioEventOccurrences.Begin();

            try
            {
                handlers.Invoke(argument);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }


        // IScenarioEventListener
        void IScenarioEventListener.HandleEventFired(ScenarioEvent firedEvent, GameObject instigator)
        {
            TryRun(instigator);
        }


        // Список статический: без перезагрузки домена выключенные сценарии прошлого запуска дожили бы до следующего.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            activeScenarios.Clear();
        }


        // ── Подготовка графа ─────────────────────────────────────────────

        private void PrepareGraph()
        {
            runningGraph = graphSource == ScenarioGraphSource.Asset ? CopyGraphAsset() : graph;

            // Привязки — только у граф-файла: во встроенном графе старые привязки не перекрывают его значения.
            var bindings = graphSource == ScenarioGraphSource.Asset ? variableBindings : null;

            wiring = new ScenarioWiring(runningGraph, this);
            runs = new ScenarioRuns(this);
            graphVariables = new ScenarioGraphVariables(runningGraph, bindings);
            heldEffects = new ScenarioHeldEffects(this);
            triggerOccupants = new ScenarioTriggerOccupants();
            listRunner = new ScenarioListRunner(this);
        }

        private ScenarioGraph CopyGraphAsset()
        {
            if (graphAsset == null)
                return null;

            graphAssetCopy = Instantiate(graphAsset);
            graphAssetCopy.name = graphAsset.name;

            return graphAssetCopy.Graph;
        }


        private void AttachEvents()
        {
            if (UsesGraph)
            {
                foreach (var node in runningGraph.Nodes)
                    (node as ScenarioWhenNode)?.Attach(this);

                return;
            }

            foreach (var scenarioEvent in events)
                scenarioEvent?.Attach(this);
        }

        private void ReleaseEvents()
        {
            if (UsesGraph)
            {
                foreach (var node in runningGraph.Nodes)
                    (node as ScenarioWhenNode)?.Release();

                return;
            }

            foreach (var scenarioEvent in events)
                scenarioEvent?.Release();
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            PrepareGraph();
        }

        private void OnEnable()
        {
            activeScenarios.Add(this);
            AttachEvents();
        }

        private void Start()
        {
            Started?.Invoke();
        }

        private void OnDisable()
        {
            activeScenarios.Remove(this);
            ReleaseEvents();
        }

        private void OnDestroy()
        {
            Stop();

            if (graphAssetCopy != null)
                Destroy(graphAssetCopy);
        }

        private void OnTriggerEnter(Collider other)
        {
            triggerOccupants?.HandleEntered(other);
            RaiseInOwnOccurrence(TriggerEntered, other);
        }

        private void OnTriggerExit(Collider other)
        {
            triggerOccupants?.HandleExited(other);
            RaiseInOwnOccurrence(TriggerExited, other);
        }
    }
}
