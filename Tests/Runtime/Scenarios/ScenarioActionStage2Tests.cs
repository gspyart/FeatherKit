using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Шаги этапа 2 — перенос, поворот, создание и удаление, материалы, физика, аниматор — и столкновения.
    /// Плюс перенос старых полей готовых блоков в новые при загрузке.
    /// </summary>
    public class ScenarioActionStage2Tests : ScenarioPlayModeTestBase
    {
        // ── Перенос и поворот ────────────────────────────────────────────

        [Test]
        public void SetPosition_MovesCharacterController_WithoutSnapBack()
        {
            var player = CreateGameObject("Player");
            player.AddComponent<CharacterController>();
            var marker = CreateGameObject("Marker");
            marker.transform.position = new Vector3(4f, 0f, 4f);

            var setPosition = WithField(new SetPositionAction(), "target", TargetObject(player));
            setPosition.Point.TargetTransform = marker.transform;

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), setPosition));

            Assert.That(player.transform.position, Is.EqualTo(new Vector3(4f, 0f, 4f)));
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SetPosition_Rigidbody_ResetsVelocity()
        {
            var crate = CreateGameObject("Crate");
            var body = crate.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(0f, 0f, 10f);
            var setPosition = WithField(new SetPositionAction(), "target", TargetObject(crate));
            setPosition.Point.Kind = ScenarioPointKind.WorldPoint;
            setPosition.Point.WorldPosition = new Vector3(3f, 0f, 0f);

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), setPosition));
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(crate.transform.position, new Vector3(3f, 0f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void RotateTo_FacesPoint_HorizontalOnly()
        {
            var turret = CreateGameObject("Turret");
            var aim = CreateGameObject("Aim");
            aim.transform.position = new Vector3(5f, 3f, 0f);

            var rotate = WithField(new RotateToAction(), "target", TargetObject(turret));
            rotate.FacePoint.Kind = ScenarioPointKind.Object;
            rotate.FacePoint.TargetTransform = aim.transform;

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), rotate));

            Assert.That(Vector3.Angle(turret.transform.forward, Vector3.right), Is.LessThan(0.1f));
        }


        // ── Создание и удаление ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator SpawnPrefab_PutsCopyIntoVariable_DestroyRemovesIt()
        {
            var graph = new ScenarioGraph();
            var spawnedVariable = graph.AddVariable(new ScenarioVariableDefinition("Spawned", ScenarioVariableKind.Object) { ObjectType = typeof(GameObject) });
            var prefab = CreateGameObject("Ghost");

            var spawn = new SpawnPrefabAction();
            spawn.Prefab.Value = prefab;
            spawn.Point.Kind = ScenarioPointKind.WorldPoint;
            spawn.Point.WorldPosition = new Vector3(0f, 0f, 7f);
            spawn.Spawned.VariableId = spawnedVariable.Id;
            GameObject copy = null;
            var grab = new ScenarioTestCallbackAction(run => copy = ScenarioVariableSlot<GameObject>.FromVariable(spawnedVariable.Id).Get(run));

            RaiseWhen(CreateStepsScenario(graph, spawn, grab));

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.transform.position, Is.EqualTo(new Vector3(0f, 0f, 7f)));
            Assert.That(copy, Is.Not.SameAs(prefab));

            var destroyGraph = new ScenarioGraph();
            var copyVariable = destroyGraph.AddVariable(new ScenarioVariableDefinition("Copy", ScenarioVariableKind.Object) { ObjectType = typeof(GameObject) });
            copyVariable.InitialValue.ObjectValue = copy;
            RaiseWhen(CreateStepsScenario(destroyGraph, WithField(new DestroyObjectAction(), "target", ScenarioTarget.FromVariable(copyVariable.Id))));
            yield return null;

            Assert.That(copy == null, Is.True);
        }


        // ── Материалы ────────────────────────────────────────────────────

        [Test]
        public void SwapMaterial_ReplacesSlot()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;
            var renderer = cube.GetComponent<MeshRenderer>();
            var newMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "Graffiti" };

            try
            {
                var swap = WithField(new SwapMaterialAction(), "renderers", new ScenarioObjectList<Renderer>(new Renderer[] { renderer }));
                swap.NewMaterial.Value = newMaterial;

                RaiseWhen(CreateStepsScenario(new ScenarioGraph(), swap));

                Assert.That(renderer.sharedMaterial, Is.SameAs(newMaterial));
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(newMaterial);
            }
        }

        [UnityTest]
        public IEnumerator FadeEmission_SetsColorThroughPropertyBlock()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;
            var renderer = cube.GetComponent<MeshRenderer>();

            try
            {
                var fade = WithField(new FadeEmissionAction(), "renderers", new ScenarioObjectList<Renderer>(new Renderer[] { renderer }));
                fade.GlowColor.Value = Color.red;
                SetField(fade, "duration", 0.05f);
                SetField(fade, "intensity", 2f);

                RaiseWhen(CreateStepsScenario(new ScenarioGraph(), fade));
                yield return new WaitForSeconds(0.15f);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                Assert.That(block.GetColor("_EmissionColor"), Is.EqualTo(Color.red * 2f));
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }


        // ── Физика и столкновения ────────────────────────────────────────

        [UnityTest]
        public IEnumerator AddImpulse_AndSetKinematic()
        {
            var box = CreateGameObject("Box");
            var body = box.AddComponent<Rigidbody>();
            body.useGravity = false;

            var push = WithField(new AddImpulseAction(), "target", TargetObject(box));
            SetField(push, "worldDirection", Vector3.right);
            push.Strength.Value = 2f;
            var hold = WithField(new SetKinematicAction(), "target", TargetObject(box));
            SetField(hold, "change", ScenarioSwitch.TurnOn);

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), push));
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity.x, Is.EqualTo(2f).Within(0.01f), "Impulse 2 on a body of mass 1");

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), hold));

            Assert.That(body.isKinematic, Is.True);
        }

        [UnityTest]
        public IEnumerator Collision_FallingBox_FiresEnterWithBoxAsCaller()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Hit When" });
            when.AddEvent(new CollisionEvent { Moment = ScenarioCollisionMoment.Enter });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Hit"));
            var floor = CreateScenario(graph, "Floor");
            floor.gameObject.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);

            var box = CreateGameObject("Falling Box");
            box.transform.position = new Vector3(0f, 2f, 0f);
            box.AddComponent<BoxCollider>();
            box.AddComponent<Rigidbody>();

            yield return new WaitForSeconds(1.5f);

            Assert.That(Marks, Is.EqualTo(new[] { "Hit" }));
            Assert.That(ReportedRuns[0].InstigatorName, Is.EqualTo("Falling Box"));
        }


        // ── Аниматор ─────────────────────────────────────────────────────

        [Test]
        public void AnimatorSetBool_WithoutAnimator_FailsRun()
        {
            LogAssert.Expect(LogType.Error, new Regex("Animator not found"));
            LogAssert.Expect(LogType.Exception, new Regex("Animator not found"));

            RaiseWhen(CreateStepsScenario(new ScenarioGraph(), new AnimatorSetBoolAction()));

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }


#if UNITY_EDITOR
        // ── Перенос старых полей ─────────────────────────────────────────

        // Старые данные пишутся в скрытые поля прежних имён через SerializedObject, как их хранит сцена;
        // при обратной записи Unity зовёт OnAfterDeserialize, и блок переносит их в новые поля.
        [Test]
        public void OldFields_MoveIntoNewOnLoad()
        {
            var lampObject = new GameObject("Lamp");
            var light = lampObject.AddComponent<Light>();
            var director = lampObject.AddComponent<PlayableDirector>();
            var animator = lampObject.AddComponent<Animator>();
            var clip = AudioClip.Create("Scream", 100, 1, 44100, false);
            var stepsNode = new ScenarioStepsNode { CustomTitle = "Old Steps" };
            var graph = new ScenarioGraph();
            graph.AddNode(stepsNode);

            foreach (var block in new ScenarioAction[]
                     {
                         new FlickerAction(), new FadeLightAction(), new MoveToPointAction(), new PlayTimelineAction(),
                         new AnimatorTriggerAction(), new PlaySoundAction()
                     })
                stepsNode.AddStep(block);

            var scenario = CreateScenario(graph);

            try
            {
                var serializedScenario = new UnityEditor.SerializedObject(scenario);
                var actions = serializedScenario.FindProperty("graph.nodes.Array.data[0].steps.actions");

                SetOldList(actions.GetArrayElementAtIndex(0).FindPropertyRelative("components"), light);
                SetOldList(actions.GetArrayElementAtIndex(1).FindPropertyRelative("lights"), light);
                actions.GetArrayElementAtIndex(2).FindPropertyRelative("point").objectReferenceValue = lampObject.transform;
                actions.GetArrayElementAtIndex(3).FindPropertyRelative("director").objectReferenceValue = director;
                actions.GetArrayElementAtIndex(4).FindPropertyRelative("animator").objectReferenceValue = animator;
                actions.GetArrayElementAtIndex(5).FindPropertyRelative("clip").objectReferenceValue = clip;
                actions.GetArrayElementAtIndex(5).FindPropertyRelative("source.kind").enumValueIndex = (int)ScenarioTargetKind.Player;
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();

                var loadedSteps = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(scenario.Graph, "Old Steps");
                var loaded = (List<ScenarioAction>)typeof(ScenarioActionList)
                    .GetField("actions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(loadedSteps.Steps);

                Assert.That(((FlickerAction)loaded[0]).FlickeringComponents.Items, Is.EqualTo(new Component[] { light }));
                Assert.That(((FadeLightAction)loaded[1]).LightList.Items, Is.EqualTo(new[] { light }));
                Assert.That(((MoveToPointAction)loaded[2]).Destination.Kind, Is.EqualTo(ScenarioPointKind.Object));
                Assert.That(((MoveToPointAction)loaded[2]).Destination.TargetTransform, Is.SameAs(lampObject.transform));
                Assert.That(((PlayTimelineAction)loaded[3]).Timeline.Value, Is.SameAs(director));
                Assert.That(((AnimatorTriggerAction)loaded[4]).TargetAnimator.Value, Is.SameAs(animator));
                Assert.That(((PlaySoundAction)loaded[5]).Sound.Value, Is.SameAs(clip));
                Assert.That(((PlaySoundAction)loaded[5]).SoundPoint.Kind, Is.EqualTo(ScenarioPointKind.Player));

                serializedScenario.Update();
                Assert.That(actions.GetArrayElementAtIndex(2).FindPropertyRelative("point").objectReferenceValue, Is.Null,
                    "The old field is emptied and does not overwrite the new one again");
            }
            finally
            {
                Object.DestroyImmediate(scenario.gameObject);
                Object.DestroyImmediate(lampObject);
                Object.DestroyImmediate(clip);
            }
        }

        private static void SetOldList(UnityEditor.SerializedProperty list, Object item)
        {
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = item;
        }
#endif


        private static ScenarioTarget TargetObject(GameObject gameObject)
        {
            var scenarioTarget = new ScenarioTarget();
            SetField(scenarioTarget, "kind", ScenarioTargetKind.Specific);
            SetField(scenarioTarget, "specificObject", gameObject);

            return scenarioTarget;
        }
    }
}
