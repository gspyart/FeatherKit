using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Этажи уровня — в самой сцене (<see cref="LiveBuilderLevelStoreys"/>): первая правка кладёт их туда со всеми числами, в сборку они не попадают,
    /// сменилась активная сцена — читаются этажи уже её.
    /// </summary>
    public class LiveBuilderLevelStoreysTests
    {
        private LiveBuilderTestScene testScene;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        [Test]
        public void FirstEdit_PutsStoreysIntoScene_KeepingOtherNumber()
        {
            Assert.IsFalse(LiveBuilderStoreys.IsStoredInScene, "в новой сцене этажей уровня нет");
            var groundBefore = LiveBuilderStoreys.GroundLevel;

            LiveBuilderStoreys.StoreyHeight = 3.5f;

            var storeys = StoreysInActiveScene();
            Assert.AreEqual(1, storeys.Length, "легли в сцену одним объектом");
            Assert.AreEqual(3.5f, storeys[0].StoreyHeight, 0.001f, "с новой высотой этажа");
            Assert.AreEqual(groundBefore, storeys[0].GroundLevel, 0.001f, "пол первого этажа — прежний");
            Assert.AreEqual("EditorOnly", storeys[0].tag, "в сборку игры не попадают");
            Assert.AreEqual(3.5f, LiveBuilderStoreys.StoreyHeight, 0.001f, "окно читает из сцены");
        }


        // Активной стала другая открытая сцена — этажи уже её, а не тестовой. Её саму не правим: это открытый уровень.
        [Test]
        public void ActiveSceneSwitch_ReadsThatScenesStoreys()
        {
            LiveBuilderStoreys.StoreyHeight = 3.5f;
            var testStoreys = LiveBuilderLevelStoreysInScene.Find();
            var testSceneHandle = SceneManager.GetActiveScene();
            var otherScene = OtherLoadedScene(testSceneHandle);
            Assume.That(otherScene.IsValid(), "нужна ещё одна открытая сцена");

            SceneManager.SetActiveScene(otherScene);

            try
            {
                Assert.AreNotSame(testStoreys, LiveBuilderLevelStoreysInScene.Find(), "в другой сцене — не этажи тестовой");
            }
            finally
            {
                SceneManager.SetActiveScene(testSceneHandle);
            }

            Assert.AreEqual(3.5f, LiveBuilderStoreys.StoreyHeight, 0.001f, "вернулись — снова её этажи");
        }

        private static Scene OtherLoadedScene(Scene except)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene != except && scene.isLoaded)
                    return scene;
            }

            return default;
        }


        private static LiveBuilderLevelStoreys[] StoreysInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<LiveBuilderLevelStoreys>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(storeys => storeys.gameObject.scene == scene).ToArray();
        }
    }
}
