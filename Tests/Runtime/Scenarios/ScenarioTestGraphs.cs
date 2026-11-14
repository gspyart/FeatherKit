using System;
using FeatherKit.Scenarios;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Графы сценария, собранные кодом, — общие для тестов рантайма и редактора.
    /// - каждый граф проверяет свою часть: старт, разветвление, Wait For, Guard While, своя нода, переменные,
    ///   For Each по списку, Gate и Once, Looks At, Distance
    /// - ноды находятся по подписи через FindNode, куда пошёл граф — по меткам ScenarioTestRecordAction
    /// </summary>
    public static class ScenarioTestGraphs
    {
        // ── Подписи нод ──────────────────────────────────────────────────

        public const string WhenTitle = "When";
        public const string LampOnTitle = "Lamp On";
        public const string NotMetTitle = "Not Met";
        public const string LeftTitle = "Left";
        public const string RightTitle = "Right";
        public const string SilhouetteOnTitle = "Silhouette On";
        public const string WaitLookTitle = "Wait Look";
        public const string SilhouetteOffTitle = "Silhouette Off";
        public const string TimedOutTitle = "Timed Out";
        public const string GuardTitle = "Guard";
        public const string BodyTitle = "Body";
        public const string BrokenTitle = "Broken";
        public const string GuardDoneTitle = "Guard Done";
        public const string CoinTitle = "Coin";
        public const string HeadsTitle = "Heads";
        public const string TailsTitle = "Tails";

        public const string ForEachTitle = "For Each";
        public const string EachTitle = "Each";
        public const string AllDoneTitle = "All Done";
        public const string OpenWhenTitle = "Open When";
        public const string GateTitle = "Gate";
        public const string OnceTitle = "Once";
        public const string PassedTitle = "Passed";
        public const string SeenTitle = "Seen";
        public const string UnseenTitle = "Unseen";
        public const string NearTitle = "Near";
        public const string FarTitle = "Far";

        public const string LampsVariableName = "Lamps";
        public const string CurrentLampVariableName = "Current Lamp";
        public const string LookerVariableName = "Looker";

        public const string LookedAtCaseLabel = "Looked At";
        public const string LeftZoneCaseLabel = "Left Zone";


        // ── Графы ────────────────────────────────────────────────────────

        /// <summary>When (условие верно) → Then → «Lamp On»; Not Met → «Not Met».</summary>
        public static ScenarioGraph WhenThenSteps()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "start", new Vector2(0f, 0f));
            when.Conditions.Add(new ScenarioTestSwitchCondition(true));

            var lampOn = AddSteps(graph, LampOnTitle, new Vector2(300f, 0f));
            var notMet = AddSteps(graph, NotMetTitle, new Vector2(300f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, lampOn, ScenarioStepsNode.In);
            graph.Connect(when, ScenarioWhenNode.NotMet, notMet, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>С одного выхода Then два провода — «Left» и «Right» идут разом.</summary>
        public static ScenarioGraph FanOut()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "start", new Vector2(0f, 0f));
            var left = AddSteps(graph, LeftTitle, new Vector2(300f, 0f));
            var right = AddSteps(graph, RightTitle, new Vector2(300f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, left, ScenarioStepsNode.In);
            graph.Connect(when, ScenarioWhenNode.Then, right, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>
        /// «Силуэт»: When → «Silhouette On» → Wait For: «Looked At» (условие) → «Silhouette Off»,
        /// «Left Zone» (событие) → назад к «Silhouette On», Timed Out (1 с) → «Timed Out».
        /// </summary>
        public static ScenarioGraph SilhouetteWaitFor()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "turned away", new Vector2(0f, 0f));
            var silhouetteOn = AddSteps(graph, SilhouetteOnTitle, new Vector2(300f, 0f));

            var waitLook = graph.AddNode(new ScenarioWaitForNode { CustomTitle = WaitLookTitle, Timeout = 1f },
                new Vector2(600f, 0f));
            var lookedAt = waitLook.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(false), LookedAtCaseLabel));
            var leftZone = waitLook.AddCase(new ScenarioWaitCase(new ScenarioTestManualEvent("left zone"), LeftZoneCaseLabel));

            var silhouetteOff = AddSteps(graph, SilhouetteOffTitle, new Vector2(900f, 0f));
            var timedOut = AddSteps(graph, TimedOutTitle, new Vector2(900f, 300f));

            graph.Connect(when, ScenarioWhenNode.Then, silhouetteOn, ScenarioStepsNode.In);
            graph.Connect(silhouetteOn, ScenarioStepsNode.Done, waitLook, ScenarioFlowNode.In);
            graph.Connect(waitLook, lookedAt.Output, silhouetteOff, ScenarioStepsNode.In);
            graph.Connect(waitLook, leftZone.Output, silhouetteOn, ScenarioStepsNode.In);
            graph.Connect(waitLook, ScenarioWaitForNode.TimedOut, timedOut, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>When → Guard While (условие верно): Body → «Body» с ожиданием; Broken → «Broken»; Done → «Guard Done».</summary>
        public static ScenarioGraph GuardWhile()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "start", new Vector2(0f, 0f));

            var guard = graph.AddNode(new ScenarioGuardNode { CustomTitle = GuardTitle }, new Vector2(300f, 0f));
            guard.Conditions.Add(new ScenarioTestSwitchCondition(true));

            var body = AddSteps(graph, BodyTitle, new Vector2(600f, 0f));
            body.AddStep(new WaitAction());

            var broken = AddSteps(graph, BrokenTitle, new Vector2(600f, 150f));
            var done = AddSteps(graph, GuardDoneTitle, new Vector2(600f, 300f));

            graph.Connect(when, ScenarioWhenNode.Then, guard, ScenarioFlowNode.In);
            graph.Connect(guard, ScenarioGuardNode.Body, body, ScenarioStepsNode.In);
            graph.Connect(guard, ScenarioGuardNode.Broken, broken, ScenarioStepsNode.In);
            graph.Connect(guard, ScenarioGuardNode.Done, done, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>Своя нода с приватными статическими выходами: When → Coin → «Heads» / «Tails».</summary>
        public static ScenarioGraph CoinFlip(float headsChance = 1f)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "start", new Vector2(0f, 0f));
            var coin = graph.AddNode(new ScenarioTestCoinFlipNode(headsChance) { CustomTitle = CoinTitle }, new Vector2(300f, 0f));
            var heads = AddSteps(graph, HeadsTitle, new Vector2(600f, 0f));
            var tails = AddSteps(graph, TailsTitle, new Vector2(600f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, coin, ScenarioFlowNode.In);
            graph.Connect(coin, new ScenarioOutput(ScenarioTestCoinFlipNode.HeadsId, "Heads"), heads, ScenarioStepsNode.In);
            graph.Connect(coin, new ScenarioOutput(ScenarioTestCoinFlipNode.TailsId, "Tails"), tails, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>Граф с переменными всех видов, открытой переменной Object, рамкой и заметкой.</summary>
        public static ScenarioGraph WithVariables()
        {
            var graph = WhenThenSteps();

            var doorOpen = graph.AddVariable(new ScenarioVariableDefinition("Door Open", ScenarioVariableKind.Flag));
            doorOpen.InitialValue.FlagValue = true;

            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            levers.InitialValue.CounterValue = 2;
            levers.IsSaved = true;

            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            delay.InitialValue.NumberValue = 0.5f;

            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object));
            lamp.ObjectType = typeof(Light);
            lamp.IsExposed = true;

            var group = graph.AddGroup(new ScenarioGroup("Start"));
            group.AddNode(FindNode<ScenarioWhenNode>(graph, WhenTitle));
            group.AddNode(FindNode<ScenarioStepsNode>(graph, LampOnTitle));

            graph.AddNote(new ScenarioNote("Lamp comes from the object's bindings", new Rect(0f, 300f, 200f, 80f)));

            return graph;
        }


        /// <summary>
        /// For Each по открытой переменной Object List «Lamps» (лампы — Light), текущая лампа — в «Current Lamp»:
        /// When → For Each: Each → «Each», Done → «All Done».
        /// </summary>
        public static ScenarioGraph ForEachOverList()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition(LampsVariableName, ScenarioVariableKind.ObjectList));
            lamps.ObjectType = typeof(Light);
            lamps.IsExposed = true;
            var currentLamp = graph.AddVariable(new ScenarioVariableDefinition(CurrentLampVariableName, ScenarioVariableKind.Object));
            currentLamp.ObjectType = typeof(Light);

            var when = AddWhen(graph, "start", new Vector2(0f, 0f));
            var forEach = graph.AddNode(new ScenarioForEachNode { CustomTitle = ForEachTitle }, new Vector2(300f, 0f));
            forEach.List.VariableId = lamps.Id;
            forEach.Item.VariableId = currentLamp.Id;

            var each = AddSteps(graph, EachTitle, new Vector2(600f, 0f));
            var allDone = AddSteps(graph, AllDoneTitle, new Vector2(600f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, forEach, ScenarioFlowNode.In);
            graph.Connect(forEach, ScenarioForEachNode.Each, each, ScenarioStepsNode.In);
            graph.Connect(forEach, ScenarioForEachNode.Done, allDone, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>When → Gate (закрыта на старте) → Once → «Passed»; «Open When» → вход Open у Gate.</summary>
        public static ScenarioGraph GateAndOnce()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "pull lever", new Vector2(0f, 0f));
            var openWhen = graph.AddNode(new ScenarioWhenNode { CustomTitle = OpenWhenTitle }, new Vector2(0f, 200f));
            openWhen.AddEvent(new ScenarioTestManualEvent("power on"));

            var gate = graph.AddNode(new ScenarioGateNode { CustomTitle = GateTitle, StartsOpen = false }, new Vector2(300f, 0f));
            var once = graph.AddNode(new ScenarioOnceNode { CustomTitle = OnceTitle }, new Vector2(600f, 0f));
            var passed = AddSteps(graph, PassedTitle, new Vector2(900f, 0f));

            graph.Connect(when, ScenarioWhenNode.Then, gate, ScenarioGateNode.Enter);
            graph.Connect(openWhen, ScenarioWhenNode.Then, gate, ScenarioGateNode.Open);
            graph.Connect(gate, ScenarioGateNode.Out, once, ScenarioFlowNode.In);
            graph.Connect(once, ScenarioOnceNode.Out, passed, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>
        /// When с условием Looks At: смотрит объект из переменной «Looker» (открытая, Object) на этот объект,
        /// конус 30°, Clear View. Then → «Seen», Not Met → «Unseen».
        /// </summary>
        public static ScenarioGraph LooksAtWithLooker()
        {
            var graph = new ScenarioGraph();
            var looker = graph.AddVariable(new ScenarioVariableDefinition(LookerVariableName, ScenarioVariableKind.Object));
            looker.ObjectType = typeof(Transform);
            looker.IsExposed = true;

            var when = AddWhen(graph, "check gaze", new Vector2(0f, 0f));
            var looksAt = new LooksAtCondition();
            looksAt.Gaze.Looker.Kind = ScenarioPointKind.Variable;
            looksAt.Gaze.Looker.VariableId = looker.Id;
            looksAt.Gaze.Target.Kind = ScenarioPointKind.ThisObject;
            looksAt.Gaze.Target.UsesRenderersCenter = true;
            looksAt.Gaze.MaxAngle = 30f;
            looksAt.Gaze.RequiresClearView = true;
            when.Conditions.Add(looksAt);

            var seen = AddSteps(graph, SeenTitle, new Vector2(300f, 0f));
            var unseen = AddSteps(graph, UnseenTitle, new Vector2(300f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, seen, ScenarioStepsNode.In);
            graph.Connect(when, ScenarioWhenNode.NotMet, unseen, ScenarioStepsNode.In);

            return graph;
        }

        /// <summary>When с условием Distance: игрок ближе 3 м к этому объекту, без высоты. Then → «Near», Not Met → «Far».</summary>
        public static ScenarioGraph Distance()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph, "check distance", new Vector2(0f, 0f));
            var distance = new DistanceCondition { Comparison = ScenarioDistanceComparison.CloserThan, Distance = 3f, IsHorizontalOnly = true };
            when.Conditions.Add(distance);

            var near = AddSteps(graph, NearTitle, new Vector2(300f, 0f));
            var far = AddSteps(graph, FarTitle, new Vector2(300f, 150f));

            graph.Connect(when, ScenarioWhenNode.Then, near, ScenarioStepsNode.In);
            graph.Connect(when, ScenarioWhenNode.NotMet, far, ScenarioStepsNode.In);

            return graph;
        }


        // ── Поиск ────────────────────────────────────────────────────────

        public static T FindNode<T>(ScenarioGraph graph, string title) where T : ScenarioNode
        {
            foreach (var node in graph.Nodes)
            {
                if (node is T typedNode && node.Title == title)
                    return typedNode;
            }

            throw new ArgumentException($"No {typeof(T).Name} titled “{title}” in the graph");
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static ScenarioWhenNode AddWhen(ScenarioGraph graph, string eventName, Vector2 position)
        {
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = WhenTitle }, position);
            when.AddEvent(new ScenarioTestManualEvent(eventName));

            return when;
        }

        // Шаг стопки пишет в журнал подпись своей ноды — по журналу видно, куда пошёл граф.
        private static ScenarioStepsNode AddSteps(ScenarioGraph graph, string title, Vector2 position)
        {
            var steps = graph.AddNode(new ScenarioStepsNode { CustomTitle = title }, position);
            steps.AddStep(new ScenarioTestRecordAction(title));

            return steps;
        }
    }
}
