using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Повторный клик в то же место: под лучом все грани объектов из коробок, и закрытые тоже; каждый повтор выделяет следующую коробку вглубь,
    /// после дальней — снова ближнюю; сдвинулась камера — уже не повтор.
    /// </summary>
    public class LiveBuilderDepthClickTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private GameObject cameraObject;
        private BoxModelSelectionMode previousSelectionMode;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            cameraObject = new GameObject("Depth Click Camera");
            cameraObject.transform.position = Origin + new Vector3(1f, 10f, 1f);
            previousSelectionMode = BoxModelSelectionModeSetting.Current;
            BoxModelSelectionModeSetting.Current = BoxModelSelectionMode.Parts;
            LiveBuilderDepthClick.Forget();
        }


        [TearDown]
        public void CloseScene()
        {
            BoxModelSelectionModeSetting.Current = previousSelectionMode;
            LiveBuilderDepthClick.Forget();
            Selection.activeObject = null;
            testScene.Dispose();
        }


        // ── Все грани под лучом ──────────────────────────────────────────

        [Test]
        public void PickAllSurfaces_FindsHiddenOnesInDepthOrder()
        {
            var (upper, lower) = CreateStackedBoxes();
            var hits = BoxModelPicker.PickAllSurfaces(DownRay());

            Assert.GreaterOrEqual(hits.Count, 2);
            Assert.AreSame(upper.GetComponentInParent<BoxModel>(), hits[0].Model, "ближняя — верхняя коробка");
            Assert.AreSame(lower.GetComponentInParent<BoxModel>(), hits[1].Model, "за ней — нижняя");
        }


        // ── Перебор ──────────────────────────────────────────────────────

        [Test]
        public void RepeatedClick_GoesDeeper_ThenWrapsAround()
        {
            var (upper, lower) = CreateStackedBoxes();
            var mouse = new Vector2(100f, 100f);
            Selection.activeGameObject = upper.gameObject;
            LiveBuilderDepthClick.RememberClick(mouse, DownRay(), cameraObject.transform, LiveBuilderTool.Select);

            Assert.IsTrue(LiveBuilderDepthClick.TrySelectDeeper(mouse, DownRay(), cameraObject.transform, LiveBuilderTool.Select));
            Assert.AreSame(lower.gameObject, Selection.activeGameObject, "второй клик — коробка глубже");

            Assert.IsTrue(LiveBuilderDepthClick.TrySelectDeeper(mouse, DownRay(), cameraObject.transform, LiveBuilderTool.Select));
            Assert.AreSame(upper.gameObject, Selection.activeGameObject, "после дальней — снова ближняя");
        }


        [Test]
        public void CameraMoved_IsNotRepeat()
        {
            var (upper, _) = CreateStackedBoxes();
            var mouse = new Vector2(100f, 100f);
            Selection.activeGameObject = upper.gameObject;
            LiveBuilderDepthClick.RememberClick(mouse, DownRay(), cameraObject.transform, LiveBuilderTool.Select);

            cameraObject.transform.position += Vector3.right;

            Assert.IsFalse(LiveBuilderDepthClick.TrySelectDeeper(mouse, DownRay(), cameraObject.transform, LiveBuilderTool.Select));
            Assert.AreSame(upper.gameObject, Selection.activeGameObject);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Две коробки в разных объектах одна над другой: верхняя закрывает нижнюю сверху.
        private static (BoxModelPart Upper, BoxModelPart Lower) CreateStackedBoxes()
        {
            var lowerModel = BoxModelFactory.CreateModel(Origin);
            var lower = BoxModelFactory.CreatePart(lowerModel, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(lowerModel);

            var upperModel = BoxModelFactory.CreateModel(Origin + new Vector3(0f, 3f, 0f));
            var upper = BoxModelFactory.CreatePart(upperModel, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(upperModel);
            Physics.SyncTransforms();
            return (upper, lower);
        }

        private static Ray DownRay()
        {
            return new Ray(Origin + new Vector3(1f, 10f, 1f), Vector3.down);
        }
    }
}
