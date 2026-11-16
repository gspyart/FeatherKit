using System.Collections;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Пространство: Looks At (конус, по осям, в кадре, Clear View, направления), Looked At, Distance Crossed, Inside Zone, Remember Position.</summary>
    public class ScenarioSpaceTests : ScenarioPlayModeTestBase
    {
        private GameObject looker;
        private GameObject target;


        [SetUp]
        public void PlaceLookerAndTarget()
        {
            looker = CreateGameObject("Looker");
            target = CreateGameObject("Target");
            target.transform.position = new Vector3(0f, 0f, 5f);
            target.AddComponent<BoxCollider>();
        }


        // ── Looks At ─────────────────────────────────────────────────────

        [Test]
        public void LooksAt_Cone_SeesAhead_NotAside()
        {
            var gaze = NewGaze();

            Assert.That(IsSeen(gaze), Is.True);

            looker.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Assert.That(IsSeen(gaze), Is.False);
        }

        [Test]
        public void LooksAt_HorizontalAndVertical_LimitsEachAxis()
        {
            var gaze = NewGaze();
            gaze.AngleMode = ScenarioLookAngleMode.HorizontalAndVertical;
            gaze.MaxYaw = 40f;
            gaze.MaxPitch = 10f;

            target.transform.position = new Vector3(3f, 0f, 5f);
            Assert.That(IsSeen(gaze), Is.True, "31° to the side fits 40° of yaw");

            target.transform.position = new Vector3(0f, 2f, 5f);
            Assert.That(IsSeen(gaze), Is.False, "22° up does not fit 10° of pitch");
        }

        [Test]
        public void LooksAt_InCameraFrame_UsesLookerCamera()
        {
            var camera = looker.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            var gaze = NewGaze();
            gaze.AngleMode = ScenarioLookAngleMode.InCameraFrame;

            Assert.That(IsSeen(gaze), Is.True);

            target.transform.position = new Vector3(0f, 0f, -5f);
            Assert.That(IsSeen(gaze), Is.False, "Behind the camera");
        }

        [Test]
        public void LooksAt_ClearView_WallBlocks_TargetItselfDoesNot()
        {
            var gaze = NewGaze();
            gaze.RequiresClearView = true;
            Physics.SyncTransforms();

            Assert.That(IsSeen(gaze), Is.True, "Hitting the target's own collider is not a block");

            var wall = CreateGameObject("Wall");
            wall.transform.position = new Vector3(0f, 0f, 2.5f);
            wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            Assert.That(IsSeen(gaze), Is.False);
        }

        [Test]
        public void LooksAt_WorldDirectionAndObjectAxis()
        {
            var gaze = NewGaze();
            gaze.TargetMode = ScenarioLookTargetMode.WorldDirection;
            gaze.WorldDirection = Vector3.forward;

            Assert.That(IsSeen(gaze), Is.True);

            gaze.TargetMode = ScenarioLookTargetMode.ObjectAxis;
            gaze.ObjectAxis = ScenarioObjectAxis.Right;

            Assert.That(IsSeen(gaze), Is.False, "Looking forward, not along the target's right axis");
        }

        private ScenarioGazeCheck NewGaze()
        {
            var gaze = new ScenarioGazeCheck();
            gaze.Looker.Kind = ScenarioPointKind.Object;
            gaze.Looker.TargetTransform = looker.transform;
            gaze.Target.Kind = ScenarioPointKind.Object;
            gaze.Target.TargetTransform = target.transform;

            return gaze;
        }

        private bool IsSeen(ScenarioGazeCheck gaze)
        {
            var looksAt = WithField(new LooksAtCondition(), "gaze", gaze);
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.Conditions.Add(looksAt);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Seen"));
            Marks.Clear();
            RaiseWhen(CreateScenario(graph));

            return Marks.Contains("Seen");
        }


        // ── Looked At ────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LookedAt_StartsAndStops_FireOnTransitions_CallerIsLooker()
        {
            looker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var graph = new ScenarioGraph();
            AddLookWhen(graph, ScenarioLookMoment.StartsLooking, "Started");
            AddLookWhen(graph, ScenarioLookMoment.StopsLooking, "Stopped");
            var scenario = CreateScenario(graph);

            yield return WaitFrames(3);
            Assert.That(Marks, Is.Empty, "Not looking at the start is not a «stop»");

            looker.transform.rotation = Quaternion.identity;
            yield return WaitFrames(3);
            looker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return WaitFrames(3);

            Assert.That(Marks, Is.EqualTo(new[] { "Started", "Stopped" }));
            Assert.That(ReportedRuns.TrueForAll(record => record.InstigatorName == "Looker"), Is.True);
            Assert.That(scenario.IsRunning, Is.False);
        }

        private void AddLookWhen(ScenarioGraph graph, ScenarioLookMoment moment, string title)
        {
            var lookedAt = WithField(new LookedAtEvent { Moment = moment, CheckInterval = 0f }, "gaze", NewGaze());
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = title + " When" });
            when.AddEvent(lookedAt);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, title));
        }


        // ── Distance Crossed ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DistanceCrossed_FiresOnCrossing_WithHysteresis()
        {
            var mover = CreateGameObject("Mover");
            mover.transform.position = new Vector3(10f, 0f, 0f);
            var crossed = new DistanceCrossedEvent { Crossing = ScenarioDistanceCrossing.ComesCloserThan, Distance = 3f, CheckInterval = 0f };
            crossed.From.Kind = ScenarioPointKind.Object;
            crossed.From.TargetTransform = mover.transform;
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Crossed" });
            when.AddEvent(crossed);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Close"));
            CreateScenario(graph);

            foreach (var x in new[] { 10f, 2f, 3.1f, 2f, 4f, 2f })
            {
                mover.transform.position = new Vector3(x, 0f, 0f);
                yield return WaitFrames(2);
            }

            Assert.That(Marks, Is.EqualTo(new[] { "Close", "Close" }), "3.1 m is inside the 0.25 m margin and does not re-arm");
        }


        // ── Inside Zone ──────────────────────────────────────────────────

        [Test]
        public void InsideZone_BoxCollider()
        {
            var zone = CreateGameObject("Zone");
            zone.AddComponent<BoxCollider>().size = new Vector3(4f, 4f, 4f);
            Physics.SyncTransforms();
            var inside = new InsideZoneCondition();
            inside.Who.Kind = ScenarioPointKind.Object;
            inside.Who.TargetTransform = looker.transform;
            inside.Zone.Value = zone.GetComponent<BoxCollider>();

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.RunAlongside;
            when.Conditions.Add(inside);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Inside"));
            ConnectToIn(graph, when, ScenarioWhenNode.NotMet, AddSteps(graph, "Outside"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            looker.transform.position = new Vector3(0f, 0f, 3f);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Inside", "Outside" }));
        }


        // ── Remember Position ────────────────────────────────────────────

        [Test]
        public void RememberPosition_ThenSetPosition_ReturnsObject()
        {
            var graph = new ScenarioGraph();
            var spot = graph.AddVariable(new ScenarioVariableDefinition("Spot", ScenarioVariableKind.Vector3));
            target.transform.position = new Vector3(1f, 2f, 3f);

            var remember = new RememberPositionAction();
            remember.Point.Kind = ScenarioPointKind.Object;
            remember.Point.TargetTransform = target.transform;
            remember.Position.VariableId = spot.Id;
            var move = new ScenarioTestCallbackAction(_ => target.transform.position = Vector3.zero);
            var setPosition = WithField(new SetPositionAction(), "target", TargetObject(target));
            setPosition.Point.Kind = ScenarioPointKind.Variable;
            setPosition.Point.VariableId = spot.Id;

            RaiseWhen(CreateStepsScenario(graph, remember, move, setPosition));

            Assert.That(target.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        private static ScenarioTarget TargetObject(GameObject gameObject)
        {
            var scenarioTarget = new ScenarioTarget();
            SetField(scenarioTarget, "kind", ScenarioTargetKind.Specific);
            SetField(scenarioTarget, "specificObject", gameObject);

            return scenarioTarget;
        }
    }
}
