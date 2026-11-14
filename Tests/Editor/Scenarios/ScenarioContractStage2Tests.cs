using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Заморозка контракта этапа 2: новые виды переменных, списки, слоты записи, точки, цель «Variable».
    /// - числа в enum не сдвигаются; поля переживают сохранение в граф-файл; пути SerializedProperty на месте
    /// </summary>
    public class ScenarioContractStage2Tests
    {
        private const string TempFolder = "Assets/ScenarioContractStage2Temp";
        private const string TempAssetPath = TempFolder + "/Graph.asset";


        [TearDown]
        public void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Числа enum ───────────────────────────────────────────────────

        [Test]
        public void EnumValues_DoNotShift()
        {
            Assert.That(EnumNumbers<ScenarioVariableKind>(), Is.EqualTo(new Dictionary<string, int>
            {
                ["Flag"] = 0, ["Counter"] = 1, ["Number"] = 2, ["Object"] = 3,
                ["Vector3"] = 4, ["Text"] = 5, ["Color"] = 6, ["ObjectList"] = 7
            }));

            Assert.That(EnumNumbers<ScenarioTargetKind>(), Is.EqualTo(new Dictionary<string, int>
            {
                ["Self"] = 0, ["Instigator"] = 1, ["Player"] = 2, ["Specific"] = 3, ["Variable"] = 4
            }));

            Assert.That(EnumNumbers<ScenarioPointKind>(), Is.EqualTo(new Dictionary<string, int>
            {
                ["ThisObject"] = 0, ["Caller"] = 1, ["Player"] = 2, ["PlayerEyes"] = 3,
                ["Object"] = 4, ["Variable"] = 5, ["WorldPoint"] = 6
            }));
        }


        // ── Виды переменных по типу поля ─────────────────────────────────

        [TestCase(typeof(Vector3), ScenarioVariableKind.Vector3)]
        [TestCase(typeof(string), ScenarioVariableKind.Text)]
        [TestCase(typeof(Color), ScenarioVariableKind.Color)]
        [TestCase(typeof(List<Light>), ScenarioVariableKind.ObjectList)]
        [TestCase(typeof(IReadOnlyList<GameObject>), ScenarioVariableKind.ObjectList)]
        [TestCase(typeof(List<Object>), ScenarioVariableKind.ObjectList)]
        [TestCase(typeof(Light), ScenarioVariableKind.Object)]
        public void TryGetKind_NewTypes(Type valueType, ScenarioVariableKind expectedKind)
        {
            Assert.That(ScenarioVariableKinds.TryGetKind(valueType, out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(expectedKind));
        }

        [TestCase(typeof(List<int>))]
        [TestCase(typeof(Vector2))]
        [TestCase(typeof(Quaternion))]
        public void TryGetKind_OtherTypes_HaveNoVariable(Type valueType)
        {
            Assert.That(ScenarioVariableKinds.TryGetKind(valueType, out _), Is.False);
        }

        [Test]
        public void Fits_ObjectList_ChecksItemType()
        {
            var lamps = new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList) { ObjectType = typeof(Light) };

            Assert.That(ScenarioVariableKinds.Fits(lamps, typeof(List<Light>)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(lamps, typeof(IReadOnlyList<GameObject>)), Is.True, "GameObject ↔ component converts");
            Assert.That(ScenarioVariableKinds.Fits(lamps, typeof(List<Object>)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(lamps, typeof(List<AudioClip>)), Is.False);
            Assert.That(ScenarioVariableKinds.Fits(lamps, typeof(Light)), Is.False, "A list is not a single object");
        }

        [Test]
        public void Fits_NewValueKinds_MatchOnlyTheirType()
        {
            var spot = new ScenarioVariableDefinition("Spot", ScenarioVariableKind.Vector3);
            var caption = new ScenarioVariableDefinition("Caption", ScenarioVariableKind.Text);
            var tint = new ScenarioVariableDefinition("Tint", ScenarioVariableKind.Color);

            Assert.That(ScenarioVariableKinds.Fits(spot, typeof(Vector3)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(spot, typeof(float)), Is.False);
            Assert.That(ScenarioVariableKinds.Fits(caption, typeof(string)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(tint, typeof(Color)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(tint, typeof(Vector3)), Is.False);
        }


        // ── Сохранение в граф-файл ───────────────────────────────────────

        [Test]
        public void GraphAsset_WithNewFields_SurvivesSaveAndLoad()
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = BuildGraphWithNewFields();

            AssetDatabase.CreateFolder("Assets", "ScenarioContractStage2Temp");
            AssetDatabase.CreateAsset(asset, TempAssetPath);
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(asset);

            var loaded = AssetDatabase.LoadAssetAtPath<ScenarioGraphAsset>(TempAssetPath);
            var graph = loaded.Graph;

            var spot = graph.Variables.Single(variable => variable.Name == "Spot");
            Assert.That(spot.Kind, Is.EqualTo(ScenarioVariableKind.Vector3));
            Assert.That(spot.InitialValue.VectorValue, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(graph.Variables.Single(variable => variable.Name == "Caption").InitialValue.TextValue, Is.EqualTo("Boo"));
            Assert.That(graph.Variables.Single(variable => variable.Name == "Tint").InitialValue.ColorValue, Is.EqualTo(Color.red));

            var lamps = graph.Variables.Single(variable => variable.Name == ScenarioTestGraphs.LampsVariableName);
            Assert.That(lamps.Kind, Is.EqualTo(ScenarioVariableKind.ObjectList));
            Assert.That(lamps.ObjectType, Is.EqualTo(typeof(Light)));
            Assert.That(lamps.InitialValue.ObjectListValue, Is.Not.Null);

            var forEach = ScenarioTestGraphs.FindNode<ScenarioForEachNode>(graph, ScenarioTestGraphs.ForEachTitle);
            Assert.That(forEach.List.VariableId, Is.EqualTo(lamps.Id));
            Assert.That(forEach.Item.IsChosen, Is.True);

            Assert.That(ScenarioTestGraphs.FindNode<ScenarioGateNode>(graph, ScenarioTestGraphs.GateTitle).StartsOpen, Is.False);

            var looksAt = (LooksAtCondition)FirstCondition(ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, "Gaze When"));
            Assert.That(looksAt.Gaze.Looker.Kind, Is.EqualTo(ScenarioPointKind.Variable));
            Assert.That(looksAt.Gaze.Target.UsesRenderersCenter, Is.True);
            Assert.That(looksAt.Gaze.RequiresClearView, Is.True);

            var distance = (DistanceCondition)FirstCondition(ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, "Distance When"));
            Assert.That(distance.Comparison, Is.EqualTo(ScenarioDistanceComparison.CloserThan));
            Assert.That(distance.IsHorizontalOnly, Is.True);
            Assert.That(distance.To.Offset, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(distance.To.WorldPosition, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(distance.To.Kind, Is.EqualTo(ScenarioPointKind.WorldPoint));
        }


        // ── Пути SerializedProperty ──────────────────────────────────────

        [Test]
        public void NewFields_HaveSerializedPaths()
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = BuildGraphWithNewFields();

            try
            {
                var serializedAsset = new SerializedObject(asset);
                var graph = serializedAsset.FindProperty("graph");

                var variables = graph.FindPropertyRelative("variables");
                RequireRelative(variables.GetArrayElementAtIndex(0), "initialValue.vectorValue", "initialValue.textValue",
                    "initialValue.colorValue", "initialValue.objectListValue");

                RequireRelative(NodeProperty(asset, graph, ScenarioTestGraphs.ForEachTitle),
                    "list", "list.items", "list.variableId", "item", "item.variableId");

                RequireRelative(NodeProperty(asset, graph, ScenarioTestGraphs.GateTitle), "startsOpen");

                RequireRelative(FirstConditionProperty(asset, graph, "Gaze When"),
                    "gaze", "gaze.looker", "gaze.looker.kind", "gaze.looker.targetTransform", "gaze.looker.variableId",
                    "gaze.looker.worldPosition", "gaze.looker.offset", "gaze.looker.usesRenderersCenter",
                    "gaze.targetMode", "gaze.target", "gaze.worldDirection", "gaze.objectAxis", "gaze.angleMode",
                    "gaze.maxAngle", "gaze.maxYaw", "gaze.maxPitch", "gaze.frameMarginPercent", "gaze.maxDistance",
                    "gaze.requiresClearView", "gaze.clearViewLayers");

                RequireRelative(FirstConditionProperty(asset, graph, "Distance When"),
                    "from", "to", "comparison", "distance", "maxDistance", "isHorizontalOnly");

                RequireRelative(NodeProperty(asset, graph, "Target Steps").FindPropertyRelative("steps.actions").GetArrayElementAtIndex(0),
                    "target.kind", "target.specificObject", "target.variableId");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }


        // ── Сборка графа ─────────────────────────────────────────────────

        // For Each и Gate из общих тестовых графов, Looks At и Distance — на своих When, переменные всех новых видов.
        private static ScenarioGraph BuildGraphWithNewFields()
        {
            var graph = ScenarioTestGraphs.ForEachOverList();

            var spot = graph.AddVariable(new ScenarioVariableDefinition("Spot", ScenarioVariableKind.Vector3));
            spot.InitialValue.VectorValue = new Vector3(1f, 2f, 3f);
            graph.AddVariable(new ScenarioVariableDefinition("Caption", ScenarioVariableKind.Text)).InitialValue.TextValue = "Boo";
            graph.AddVariable(new ScenarioVariableDefinition("Tint", ScenarioVariableKind.Color)).InitialValue.ColorValue = Color.red;

            graph.AddNode(new ScenarioGateNode { CustomTitle = ScenarioTestGraphs.GateTitle, StartsOpen = false });

            var gazeWhen = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Gaze When" });
            var looksAt = new LooksAtCondition();
            looksAt.Gaze.Looker.Kind = ScenarioPointKind.Variable;
            looksAt.Gaze.Looker.VariableId = spot.Id;
            looksAt.Gaze.Target.UsesRenderersCenter = true;
            looksAt.Gaze.RequiresClearView = true;
            gazeWhen.Conditions.Add(looksAt);

            var distanceWhen = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Distance When" });
            var distance = new DistanceCondition { Comparison = ScenarioDistanceComparison.CloserThan, IsHorizontalOnly = true };
            distance.To.Kind = ScenarioPointKind.WorldPoint;
            distance.To.WorldPosition = new Vector3(5f, 0f, 5f);
            distance.To.Offset = new Vector3(0f, 1f, 0f);
            distanceWhen.Conditions.Add(distance);

            var targetSteps = graph.AddNode(new ScenarioStepsNode { CustomTitle = "Target Steps" });
            targetSteps.AddStep(new SetObjectActiveAction());

            return graph;
        }

        private static ScenarioCondition FirstCondition(ScenarioWhenNode whenNode)
        {
            var conditions = (List<ScenarioCondition>)typeof(ScenarioConditionList)
                .GetField("conditions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(whenNode.Conditions);

            return conditions[0];
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static Dictionary<string, int> EnumNumbers<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(value => value.ToString(), value => Convert.ToInt32(value));
        }

        private static SerializedProperty NodeProperty(ScenarioGraphAsset asset, SerializedProperty graph, string title)
        {
            var index = asset.Graph.Nodes.ToList().FindIndex(node => node.Title == title);

            return graph.FindPropertyRelative("nodes").GetArrayElementAtIndex(index);
        }

        private static SerializedProperty FirstConditionProperty(ScenarioGraphAsset asset, SerializedProperty graph, string whenTitle)
        {
            return NodeProperty(asset, graph, whenTitle).FindPropertyRelative("conditions.conditions").GetArrayElementAtIndex(0);
        }

        private static void RequireRelative(SerializedProperty parent, params string[] relativePaths)
        {
            foreach (var relativePath in relativePaths)
                Assert.That(parent.FindPropertyRelative(relativePath), Is.Not.Null, $"{parent.propertyPath} → {relativePath}");
        }
    }
}
