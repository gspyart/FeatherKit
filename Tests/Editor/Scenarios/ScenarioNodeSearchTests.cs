using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Поиск нод: что в нём есть, где лежит и что из выбранного получается.</summary>
    public class ScenarioNodeSearchTests
    {
        [Test]
        public void Options_StartWithCommentWhenAndDo_AndHaveFlowNodes()
        {
            var options = ScenarioNodeSearch.CollectOptions();
            var createdTypes = options.Select(option => option.CreatedType).ToList();

            Assert.That(options[0].CreatedType, Is.EqualTo(typeof(ScenarioNote)));
            Assert.That(options[1].CreatedType, Is.EqualTo(typeof(ScenarioWhenNode)));
            Assert.That(options[2].CreatedType, Is.EqualTo(typeof(ScenarioStepsNode)));
            Assert.That(createdTypes, Has.Member(typeof(ScenarioWaitForNode)).And.Member(typeof(ScenarioGuardNode)));
            Assert.That(createdTypes.Count(type => type == typeof(ScenarioWhenNode)), Is.EqualTo(1));
        }

        [Test]
        public void Options_PutBlocksIntoTheirFolders()
        {
            var options = ScenarioNodeSearch.CollectOptions();

            var flicker = options.Single(option => option.CreatedType == typeof(FlickerAction));
            var started = options.Single(option => option.CreatedType == typeof(ObjectStartedEvent));
            var waitFor = options.Single(option => option.CreatedType == typeof(ScenarioWaitForNode));

            Assert.That(flicker.Path, Is.EqualTo(new[] { "Steps", "Light", "Flicker" }));
            Assert.That(started.Path, Is.EqualTo(new[] { "Events", "Object", "Start" }));
            Assert.That(waitFor.Path, Is.EqualTo(new[] { "Flow", "Wait For" }));
        }

        [Test]
        public void Options_SkipConditionsAndTestBlocksWithoutEmptyConstructor()
        {
            var createdTypes = ScenarioNodeSearch.CollectOptions().Select(option => option.CreatedType).ToList();

            Assert.That(createdTypes, Has.No.Member(typeof(ScenarioTestRecordAction)));
            Assert.That(createdTypes, Has.No.Member(typeof(ScenarioTestManualEvent)));
            Assert.That(createdTypes, Has.No.Member(typeof(ScenarioTestCoinFlipNode)));
            Assert.That(createdTypes.Any(type => typeof(ScenarioCondition).IsAssignableFrom(type)), Is.False);
        }

        [Test]
        public void EveryOptionButComment_MakesANode()
        {
            foreach (var option in ScenarioNodeSearch.CollectOptions().Where(option => option.CreatedType != typeof(ScenarioNote)))
            {
                var node = ScenarioGraphEditing.CreateNodeFor(option.CreatedType);

                Assert.That(node, Is.Not.Null, string.Join("/", option.Path));

                if (typeof(ScenarioAction).IsAssignableFrom(option.CreatedType))
                    Assert.That(((ScenarioStepsNode)node).Steps.Count, Is.EqualTo(1));
            }
        }
    }
}
