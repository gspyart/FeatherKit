using System.Collections;
using System.Reflection;
using FeatherKit.Contexts;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Проверки тестировщика, этап 2, пространство: Clear View у составной цели, триггеры и слои, кадр без камеры,
    /// опрос Looked At — остановка на Destroy/Stop/Restart, один опрос на ноду, Hold Time, Distance Crossed «дальше».
    /// </summary>
    public class ScenarioQaStage2SpaceTests : ScenarioPlayModeTestBase
    {
        private const BindingFlags PrivateField = BindingFlags.Instance | BindingFlags.NonPublic;

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


        // ── Clear View ───────────────────────────────────────────────────

        // Силуэт-монстр: Rigidbody на корне, коллайдер на «голове», цель — голова. Луч упирается в саму голову,
        // но RaycastHit.transform отдаёт корень с Rigidbody, а не голову — и голова «закрыта» сама собой.
        [Test, Category("QA_Found")]
        public void ClearView_TargetIsChildColliderOfRigidbody_DoesNotBlockItself()
        {
            var monster = CreateGameObject("Monster");
            monster.transform.position = new Vector3(0f, 0f, 5f);
            monster.AddComponent<Rigidbody>().isKinematic = true;
            var head = CreateGameObject("Head");
            head.transform.SetParent(monster.transform, false);
            head.AddComponent<BoxCollider>();
            Object.DestroyImmediate(target);
            Physics.SyncTransforms();

            var gaze = NewGaze(head.transform);
            gaze.RequiresClearView = true;

            Assert.That(IsSeen(gaze), Is.True, "The target's own collider counts as a wall when its Rigidbody is on a parent");
        }

        [Test]
        public void ClearView_TriggerInTheWay_DoesNotBlock()
        {
            var fog = CreateGameObject("Fog Trigger");
            fog.transform.position = new Vector3(0f, 0f, 2.5f);
            fog.AddComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();

            var gaze = NewGaze(target.transform);
            gaze.RequiresClearView = true;

            Assert.That(IsSeen(gaze), Is.True);
        }

        [Test]
        public void ClearView_WallOnExcludedLayer_DoesNotBlock()
        {
            const int IgnoreRaycastLayer = 2;

            var glass = CreateGameObject("Glass");
            glass.layer = IgnoreRaycastLayer;
            glass.transform.position = new Vector3(0f, 0f, 2.5f);
            glass.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            var gaze = NewGaze(target.transform);
            gaze.RequiresClearView = true;
            gaze.ClearViewLayers = ~(1 << IgnoreRaycastLayer);

            Assert.That(IsSeen(gaze), Is.True);
        }


        // ── In Camera Frame без камеры ───────────────────────────────────

        [Test]
        public void InCameraFrame_NoCameraAnywhere_IsNotSeen_WithoutError()
        {
            Assume.That(Camera.main, Is.Null, "The test scene has a main camera");

            var gaze = NewGaze(target.transform);
            gaze.AngleMode = ScenarioLookAngleMode.InCameraFrame;

            Assert.That(IsSeen(gaze), Is.False);
            Assert.That(ReportedRuns.TrueForAll(record => record.Outcome != ScenarioRunOutcome.Failed), Is.True);
        }


        // ── Опрос Looked At: подписка и отписка ──────────────────────────

        [UnityTest]
        public IEnumerator LookedAt_PollingStops_WhenScenarioDestroyed()
        {
            yield return null;
            var before = RunningCoroutineCount();

            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Looked When" });
            when.AddEvent(LookedAt(ScenarioLookMoment.StartsLooking, checkInterval: 0.1f));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Seen"));
            var scenario = CreateScenario(graph);
            yield return null;

            Assert.That(RunningCoroutineCount() - before, Is.EqualTo(1), "One poll for one event block");

            Object.Destroy(scenario.gameObject);
            yield return WaitFrames(2);

            Assert.That(RunningCoroutineCount(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator LookedAtInWaitFor_TwoWaits_PollStopsOnStop()
        {
            yield return null;
            var before = RunningCoroutineCount();

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.RunAlongside;
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var seen = waitFor.AddCase(new ScenarioWaitCase(LookedAt(ScenarioLookMoment.StartsLooking, checkInterval: 0.1f)));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, waitFor);
            ConnectToIn(graph, waitFor, seen.Output, AddSteps(graph, "Seen"));
            looker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return null;
            RaiseWhen(scenario);
            yield return null;

            Assert.That(seen.AwaitedEvent.IsAttached, Is.True);

            scenario.Stop();
            yield return null;

            Assert.That(seen.AwaitedEvent.IsAttached, Is.False);
            Assert.That(RunningCoroutineCount(), Is.EqualTo(before));
        }


        // ── Hold Time ────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LookedAt_HoldTime_GlanceAwayMidHold_StartsOver()
        {
            looker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var lookedAt = LookedAt(ScenarioLookMoment.StartsLooking, checkInterval: 0f);
            lookedAt.HoldTime = 0.4f;
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Looked When" });
            when.AddEvent(lookedAt);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Seen"));
            CreateScenario(graph);
            yield return WaitFrames(2);

            looker.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(0.25f);
            looker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return WaitFrames(3);
            looker.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(0.25f);

            Assert.That(Marks, Is.Empty, "Two glances of 0.25 s are not one look of 0.4 s");

            yield return new WaitForSeconds(0.3f);

            Assert.That(Marks, Is.EqualTo(new[] { "Seen" }));
        }


        // ── Distance Crossed: «отошёл дальше» ────────────────────────────

        [UnityTest]
        public IEnumerator DistanceCrossed_MovesFarther_FiresOnCrossing_WithHysteresis()
        {
            var mover = CreateGameObject("Mover");
            mover.transform.position = new Vector3(1f, 0f, 0f);
            var crossed = new DistanceCrossedEvent { Crossing = ScenarioDistanceCrossing.MovesFartherThan, Distance = 3f, CheckInterval = 0f };
            crossed.From.Kind = ScenarioPointKind.Object;
            crossed.From.TargetTransform = mover.transform;
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Crossed" });
            when.AddEvent(crossed);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Far"));
            CreateScenario(graph);

            foreach (var x in new[] { 1f, 4f, 2.9f, 4f, 2f, 4f })
            {
                mover.transform.position = new Vector3(x, 0f, 0f);
                yield return WaitFrames(2);
            }

            Assert.That(Marks, Is.EqualTo(new[] { "Far", "Far" }), "2.9 m is inside the margin and does not re-arm");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private LookedAtEvent LookedAt(ScenarioLookMoment moment, float checkInterval)
        {
            return WithField(new LookedAtEvent { Moment = moment, CheckInterval = checkInterval }, "gaze", NewGaze(target.transform));
        }

        private ScenarioGazeCheck NewGaze(Transform targetTransform)
        {
            var gaze = new ScenarioGazeCheck();
            gaze.Looker.Kind = ScenarioPointKind.Object;
            gaze.Looker.TargetTransform = looker.transform;
            gaze.Target.Kind = ScenarioPointKind.Object;
            gaze.Target.TargetTransform = targetTransform;

            return gaze;
        }

        private bool IsSeen(ScenarioGazeCheck gaze)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.Conditions.Add(WithField(new LooksAtCondition(), "gaze", gaze));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Seen"));
            Marks.Clear();
            RaiseWhen(CreateScenario(graph));

            return Marks.Contains("Seen");
        }

        private static int RunningCoroutineCount()
        {
            var runner = AppContext.Current.Coroutines;
            var running = (IDictionary)runner.GetType().GetField("running", PrivateField).GetValue(runner);

            return running.Count;
        }
    }
}
