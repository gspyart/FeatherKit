using System.Collections.Generic;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Блоки списков: Add, Remove, Clear, Contains, Count, Random Item; For Each по списку, который меняют во время обхода.</summary>
    public class ScenarioListBlockTests : ScenarioPlayModeTestBase
    {
        [Test]
        public void AddRemoveClear_ChangeTheLiveList()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));
            var count = graph.AddVariable(new ScenarioVariableDefinition("Count", ScenarioVariableKind.Counter));
            var first = CreateGameObject("First");
            var second = CreateGameObject("Second");

            var countLamps = WithField(new ListCountAction(), "list", ScenarioObjectList<Object>.FromVariable(lamps.Id));
            SetField(countLamps, "count", ScenarioVariableSlot<int>.FromVariable(count.Id));
            var recordCount = new ScenarioTestRecordCounterAction(ScenarioValue<int>.FromVariable(count.Id));

            RaiseWhen(CreateStepsScenario(graph,
                Add(lamps, first), Add(lamps, first), Add(lamps, second), countLamps, recordCount,
                Remove(lamps, first), countLamps, recordCount,
                WithField(new ClearListAction(), "list", ScenarioVariableSlot<List<Object>>.FromVariable(lamps.Id)),
                countLamps, recordCount));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "2", "1", "0" }), "Without Allow Repeats an object is added once");
        }

        [Test]
        public void Contains_MatchesObjectAndItsComponents()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));
            var lampObject = CreateGameObject("Lamp");
            var light = lampObject.AddComponent<Light>();
            lamps.InitialValue.ObjectListValue.Add(lampObject);

            var contains = WithField(new ListContainsCondition(), "list", ScenarioObjectList<Object>.FromVariable(lamps.Id));
            contains.Item.Value = light;

            var when = AddWhen(graph);
            when.Conditions.Add(contains);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Found"));
            RaiseWhen(CreateScenario(graph));

            Assert.That(Marks, Is.EqualTo(new[] { "Found" }));
        }

        [Test]
        public void RandomItem_PicksFromTheList_EmptyGivesNothing()
        {
            var graph = new ScenarioGraph();
            var picked = graph.AddVariable(new ScenarioVariableDefinition("Picked", ScenarioVariableKind.Object));
            var only = CreateGameObject("Only");

            var pickFromOne = WithField(new RandomFromListAction(), "list", new ScenarioObjectList<Object>(new Object[] { only }));
            SetField(pickFromOne, "item", ScenarioVariableSlot<Object>.FromVariable(picked.Id));
            var pickFromNone = WithField(new RandomFromListAction(), "item", ScenarioVariableSlot<Object>.FromVariable(picked.Id));
            var record = new ScenarioTestRecordSlotAction(ScenarioVariableSlot<Object>.FromVariable(picked.Id));

            RaiseWhen(CreateStepsScenario(graph, pickFromOne, record, pickFromNone, record));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Only", "none" }));
        }

        [Test]
        public void ForEach_ListChangedDuringLoop_DoesNotChangeThisLoop()
        {
            var graph = ScenarioTestGraphs.ForEachOverList();
            var lamps = graph.FindVariableByName(ScenarioTestGraphs.LampsVariableName);
            lamps.IsExposed = false;
            lamps.InitialValue.ObjectListValue.Add(CreateGameObject("A").AddComponent<Light>());
            lamps.InitialValue.ObjectListValue.Add(CreateGameObject("B").AddComponent<Light>());

            var clear = WithField(new ClearListAction(), "list", ScenarioVariableSlot<List<Object>>.FromVariable(lamps.Id));
            ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.EachTitle).AddStep(clear);

            RaiseWhen(CreateScenario(graph));

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.EachTitle, ScenarioTestGraphs.EachTitle, ScenarioTestGraphs.AllDoneTitle }));
        }


        private static AddToListAction Add(ScenarioVariableDefinition list, Object item)
        {
            var add = new AddToListAction();
            add.List.VariableId = list.Id;
            add.Item.Value = item;

            return add;
        }

        private static RemoveFromListAction Remove(ScenarioVariableDefinition list, Object item)
        {
            var remove = new RemoveFromListAction();
            remove.List.VariableId = list.Id;
            remove.Item.Value = item;

            return remove;
        }
    }
}
