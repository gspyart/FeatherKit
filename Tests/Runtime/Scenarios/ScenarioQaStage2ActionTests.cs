using System.Collections;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Проверки тестировщика, этап 2, блоки: копии материалов у Fade Emission, Destroy своего объекта посреди ветки,
    /// выход из столкновения уничтоженного гостя, Set Position у Rigidbody, перенос старых полей.
    /// </summary>
    public class ScenarioQaStage2ActionTests : ScenarioPlayModeTestBase
    {
        private readonly List<GameObject> plainObjects = new List<GameObject>();


        // ── Fade Emission: копии материалов ──────────────────────────────

        // Шейдер без _EMISSION (Unlit, свой): копия материала всё равно одна, а не на каждый вызов.
        [Test]
        public void FadeEmission_ShaderWithoutEmissionKeyword_DoesNotCopyMaterialEachTime()
        {
            AssertOneMaterialCopy("Unlit/Color");
        }

        [Test]
        public void FadeEmission_StandardShader_CopiesMaterialOnce_AssetUntouched()
        {
            AssertOneMaterialCopy("Standard");
        }

        private void AssertOneMaterialCopy(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assume.That(shader, Is.Not.Null, shaderName + " is not in the build");

            var asset = new Material(shader) { name = "Lamp Glass" };
            var lamp = CreateGameObject("Lamp");
            var renderer = lamp.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = asset;
            var fade = new FadeEmissionAction();
            fade.Renderers.Items.Add(renderer);
            SetField(fade, "duration", 0f);
            var scenario = CreateStepsScenario(new ScenarioGraph(), fade);
            var materials = new List<Material>();

            try
            {
                for (var index = 0; index < 3; index++)
                {
                    RaiseWhen(scenario);
                    materials.Add(renderer.sharedMaterial);
                }

                Assert.That(asset.IsKeywordEnabled("_EMISSION"), Is.False, "The material asset itself must not change");
                var copies = new HashSet<Material>(materials).Count;
                Assert.That(copies, Is.EqualTo(1), $"Fade Emission ran 3 times and left {copies} different material copies");
            }
            finally
            {
                foreach (var copy in materials)
                {
                    if (copy != null && copy != asset)
                        Object.DestroyImmediate(copy);
                }

                Object.DestroyImmediate(asset);
            }
        }


        // ── Destroy своего объекта посреди ветки ─────────────────────────

        [UnityTest]
        public IEnumerator DestroyOwnObject_MidBranch_StopsScenario_WithoutErrors()
        {
            var effect = new ScenarioTestHeldEffect();
            var scenario = CreateStepsScenario(new ScenarioGraph(),
                new ScenarioTestHoldEffectAction(effect),
                new DestroyObjectAction(),
                new ScenarioTestWaitFramesAction(2),
                new ScenarioTestRecordAction("Later"));

            RaiseWhen(scenario);
            yield return WaitFrames(4);

            Assert.That(Marks, Does.Not.Contain("Later"));
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
        }


        // ── Collision Exit: гость уничтожен ──────────────────────────────

        [UnityTest]
        public IEnumerator CollisionExit_VisitorDestroyed_DoesNotThrow()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Left When" });
            when.AddEvent(new CollisionEvent { Moment = ScenarioCollisionMoment.Exit, VisitorTag = "Player" });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Left"));
            var floor = CreateScenario(graph, "Floor");
            floor.gameObject.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);

            var box = CreateGameObject("Player Box");
            box.tag = "Player";
            box.transform.position = new Vector3(0f, 1.5f, 0f);
            box.AddComponent<BoxCollider>();
            box.AddComponent<Rigidbody>();

            yield return new WaitForSeconds(1f);
            Object.Destroy(box);
            yield return new WaitForSeconds(0.3f);

            Assert.Pass("Exit fired: " + Marks.Contains("Left"));
        }


        // ── Set Position у Rigidbody ─────────────────────────────────────

        [UnityTest]
        public IEnumerator SetPosition_InterpolatedRigidbody_StaysAtNewPlace()
        {
            var crate = CreateGameObject("Crate");
            crate.AddComponent<BoxCollider>();
            var body = crate.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var setPosition = new SetPositionAction();
            SetField(setPosition.Target, "kind", ScenarioTargetKind.Specific);
            SetField(setPosition.Target, "specificObject", crate);
            setPosition.Point.Kind = ScenarioPointKind.WorldPoint;
            setPosition.Point.WorldPosition = new Vector3(5f, 0f, 0f);
            var scenario = CreateStepsScenario(new ScenarioGraph(), setPosition);
            yield return new WaitForFixedUpdate();

            RaiseWhen(scenario);

            for (var step = 0; step < 4; step++)
                yield return new WaitForFixedUpdate();

            yield return null;

            Assert.That(Vector3.Distance(crate.transform.position, new Vector3(5f, 0f, 0f)), Is.LessThan(0.01f));
        }


#if UNITY_EDITOR
        // ── Перенос старых полей ─────────────────────────────────────────

        // Старый Play Sound играл «из объекта» (Source = конкретный объект). Перенос зовёт specificObject.transform
        // прямо в OnAfterDeserialize — Unity такие вызовы при чтении данных запрещает.
        [Test, Category("QA_Found")]
        public void OldPlaySound_SourceIsSpecificObject_MovesIntoPoint()
        {
            var speaker = CreatePlainObject("Speaker");
            var scenario = CreateOldStepsScenario(new PlaySoundAction());

            LogAssert.ignoreFailingMessages = true;
            ApplyOldFields(scenario, action =>
            {
                action.FindPropertyRelative("source.kind").enumValueIndex = (int)ScenarioTargetKind.Specific;
                action.FindPropertyRelative("source.specificObject").objectReferenceValue = speaker;
            });
            LogAssert.ignoreFailingMessages = false;

            var playSound = (PlaySoundAction)LoadedSteps(scenario)[0];
            Assert.That(playSound.SoundPoint.Kind, Is.EqualTo(ScenarioPointKind.Object));
            Assert.That(playSound.SoundPoint.TargetTransform, Is.SameAs(speaker.transform));
        }

        // Старое и новое поле заданы оба (неперенесённый префаб и правка нового поля на экземпляре) — новое главнее.
        [Test, Category("QA_Found")]
        public void OldMoveToPoint_OldAndNewBothSet_NewWins()
        {
            var oldPoint = CreatePlainObject("Old Point");
            var scenario = CreateOldStepsScenario(new MoveToPointAction());

            ApplyOldFields(scenario, action =>
            {
                action.FindPropertyRelative("point").objectReferenceValue = oldPoint.transform;
                action.FindPropertyRelative("destination.kind").enumValueIndex = (int)ScenarioPointKind.WorldPoint;
                action.FindPropertyRelative("destination.worldPosition").vector3Value = new Vector3(1f, 2f, 3f);
            });

            var destination = ((MoveToPointAction)LoadedSteps(scenario)[0]).Destination;
            Assert.That(destination.Kind, Is.EqualTo(ScenarioPointKind.WorldPoint), "The old field overwrote the new one");
        }

        [Test, Category("QA_Found")]
        public void OldPlaySound_OldSourceAndNewPointBothSet_NewWins()
        {
            var scenario = CreateOldStepsScenario(new PlaySoundAction());

            ApplyOldFields(scenario, action =>
            {
                action.FindPropertyRelative("source.kind").enumValueIndex = (int)ScenarioTargetKind.Player;
                action.FindPropertyRelative("soundPoint.kind").enumValueIndex = (int)ScenarioPointKind.WorldPoint;
            });

            var soundPoint = ((PlaySoundAction)LoadedSteps(scenario)[0]).SoundPoint;
            Assert.That(soundPoint.Kind, Is.EqualTo(ScenarioPointKind.WorldPoint), "The old field overwrote the new one");
        }

        // Данные читаются дважды (открыли сцену, потом префаб пересохранили) — перенесённое не теряется.
        [Test]
        public void OldFlicker_ReadTwice_KeepsMovedLights()
        {
            var lamp = CreatePlainObject("Lamp");
            var light = lamp.AddComponent<Light>();
            var scenario = CreateOldStepsScenario(new FlickerAction());

            ApplyOldFields(scenario, action =>
            {
                var components = action.FindPropertyRelative("components");
                components.arraySize = 1;
                components.GetArrayElementAtIndex(0).objectReferenceValue = light;
            });
            ApplyOldFields(scenario, action => action.FindPropertyRelative("duration").floatValue = 2f);

            Assert.That(((FlickerAction)LoadedSteps(scenario)[0]).FlickeringComponents.Items, Is.EqualTo(new Component[] { light }));
        }

        // Как в тесте программиста: объект без HideAndDontSave — ссылки на такие сериализуются как в сцене.
        private GameObject CreatePlainObject(string objectName)
        {
            var plainObject = new GameObject(objectName);
            plainObjects.Add(plainObject);

            return plainObject;
        }

        [TearDown]
        public void DestroyPlainObjects()
        {
            foreach (var plainObject in plainObjects)
            {
                if (plainObject != null)
                    Object.DestroyImmediate(plainObject);
            }

            plainObjects.Clear();
        }

        private Scenario CreateOldStepsScenario(ScenarioAction block)
        {
            var stepsNode = new ScenarioStepsNode { CustomTitle = "Old Steps" };
            stepsNode.AddStep(block);
            var graph = new ScenarioGraph();
            graph.AddNode(stepsNode);

            return CreateScenario(graph);
        }

        private static void ApplyOldFields(Scenario scenario, System.Action<UnityEditor.SerializedProperty> fill)
        {
            var serializedScenario = new UnityEditor.SerializedObject(scenario);
            var actions = serializedScenario.FindProperty("graph.nodes.Array.data[0].steps.actions");
            fill(actions.GetArrayElementAtIndex(0));
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<ScenarioAction> LoadedSteps(Scenario scenario)
        {
            var steps = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(scenario.Graph, "Old Steps");

            return (List<ScenarioAction>)typeof(ScenarioActionList)
                .GetField("actions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(steps.Steps);
        }
#endif
    }
}
