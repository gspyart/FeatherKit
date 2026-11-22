using FeatherKit.EditorTools.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Память Live Builder об удалённых постройках (<see cref="LiveBuilderDestroyedObjects"/>): расчёты объектов, нерезаные части зданий,
    /// кого резали вырезы — забываются, когда постройку удалили; чистовой проход по всей сцене чистит их сам.
    /// </summary>
    public class LiveBuilderDestroyedObjectsTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();

            // Удалённое прежними проверками — забыть до начала.
            LiveBuilderRebuildQueue.RunFinalPassNow();
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        [Test]
        public void ForgetDestroyed_DropsDestroyedConstructions()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            Object.DestroyImmediate(targets.Root.gameObject);

            Assert.Greater(BoxModelBuildResults.ForgetDestroyed(), 0, "расчёты объектов из коробок");
            Assert.Greater(SplineBuildingUncutParts.ForgetDestroyed(), 0, "нерезаные части здания");
            Assert.Greater(GeometryCutterLastTargets.ForgetDestroyed(), 0, "кого резали вырезы");

            Assert.AreEqual(0, BoxModelBuildResults.ForgetDestroyed(), "второй раз забывать нечего");
        }


        [Test]
        public void FinalPass_ForgetsDestroyedConstructions()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            Object.DestroyImmediate(targets.Root.gameObject);

            LiveBuilderRebuildQueue.RunFinalPassNow();

            Assert.AreEqual(0, BoxModelBuildResults.ForgetDestroyed(), "расчёты объектов забыты проходом");
            Assert.AreEqual(0, SplineBuildingUncutParts.ForgetDestroyed(), "нерезаные части забыты проходом");
            Assert.AreEqual(0, GeometryCutterLastTargets.ForgetDestroyed(), "вырезы забыты проходом");
        }


        [Test]
        public void ForgetDestroyed_KeepsLiveConstructions()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            BoxModelBuildResults.ForgetDestroyed();
            SplineBuildingUncutParts.ForgetDestroyed();
            var surfaces = BoxModelRebuilder.GetSurfaces(targets.Yard);

            Assert.AreEqual(0, SplineBuildingUncutParts.ForgetDestroyed(), "живое здание не забыто");
            Assert.AreSame(surfaces, BoxModelRebuilder.GetSurfaces(targets.Yard), "расчёт живого объекта остался");
        }
    }
}
