using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Клавиши Live Builder против ярлыков окна сцены Unity — через обработчик ярлыков самой Unity (<see cref="UnityShortcutKeyboard"/>).
    /// Live Builder включён — «2», S, X при рисовании и стрелки у выбранных точек выполняют его ярлык, без окна спора; выключен — ярлыки Unity как были.
    /// Держится на внутреннем API Unity: после обновления Unity первым смотреть PriorityContextMethod_IsFound.
    /// </summary>
    public class LiveBuilderShortcutPriorityTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;
        private LiveBuilderTool toolBefore;
        private PivotRotation pivotRotationBefore;
        private ViewTool viewToolBefore;
        private bool isPointSnapEnabledBefore;
        private SceneView sceneView;
        private bool isSceneViewCreated;
        private UnityShortcutKeyboard unityKeyboard;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            toolBefore = LiveBuilderToolPalette.Current;
            pivotRotationBefore = Tools.pivotRotation;
            viewToolBefore = Tools.viewTool;
            isPointSnapEnabledBefore = LiveBuilderPointSnap.IsEnabled;
            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();

            isSceneViewCreated = SceneView.sceneViews.Count == 0;
            sceneView = EditorWindow.GetWindow<SceneView>();
            sceneView.Focus();
            unityKeyboard = new UnityShortcutKeyboard(sceneView);
        }


        [TearDown]
        public void CloseScene()
        {
            unityKeyboard.Dispose();
            LiveBuilderActivation.SetToolActive(false);

            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            LiveBuilderContourSelection.SetOwner(null);
            Tools.viewTool = viewToolBefore;

            if (isSceneViewCreated)
                sceneView.Close();

            testScene.Dispose();
            Tools.pivotRotation = pivotRotationBefore;
            LiveBuilderPointSnap.IsEnabled = isPointSnapEnabledBefore;
            LiveBuilderToolPalette.Current = toolBefore;
            Selection.objects = selectionBefore;
        }


        // ── Внутреннее API ───────────────────────────────────────────────

        [Test]
        public void PriorityContextMethod_IsFound()
        {
            Assert.IsTrue(LiveBuilderPriorityShortcutContexts.IsAvailable,
                "ContextManager.RegisterPriorityContext не найден — после обновления Unity клавиши Live Builder спорят с окном сцены");
        }


        // ── Live Builder включён ─────────────────────────────────────────

        [Test]
        public void LiveBuilderOn_TwoAndS_RunLiveBuilderShortcuts()
        {
            TurnOnLiveBuilder();
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();

            CollectionAssert.AreEqual(new[] { LiveBuilderShortcutIds.ToolOf(boxTool) }, unityKeyboard.Press(KeyCode.Alpha2), "«2» — «Коробка», не 2D");
            Assert.AreSame(boxTool, LiveBuilderToolPalette.Current);
            Assert.IsFalse(sceneView.in2DMode, "2D не включился");

            var isPointSnapEnabled = LiveBuilderPointSnap.IsEnabled;
            CollectionAssert.AreEqual(new[] { LiveBuilderShortcutIds.TogglePointSnap }, unityKeyboard.Press(KeyCode.S), "S — прилипание, не Tool Settings");
            Assert.AreNotEqual(isPointSnapEnabled, LiveBuilderPointSnap.IsEnabled);
        }


        [Test]
        public void LiveBuilderOn_DigitsAndXWhileDrawing_TypeNumber()
        {
            TurnOnLiveBuilder();
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderToolPalette.Current = boxTool;
            var pivotRotation = Tools.pivotRotation;

            new LiveBuilderToolDriver(boxTool).Hold(FloorRay(0f, 0f), FloorRay(1f, 1f));

            CollectionAssert.AreEqual(new[] { LiveBuilderShortcutIds.DigitOf(2) }, unityKeyboard.Press(KeyCode.Alpha2), "«2» при рисовании — цифра");
            CollectionAssert.AreEqual(new[] { LiveBuilderShortcutIds.Times }, unityKeyboard.Press(KeyCode.X), "X при рисовании — «×»");
            Assert.AreEqual(pivotRotation, Tools.pivotRotation, "ось ручек Unity не переключилась");
            Assert.IsFalse(sceneView.in2DMode, "2D не включился");
        }


        [Test]
        public void LiveBuilderOn_ArrowWithSelectedPoints_NudgesPoints()
        {
            TurnOnLiveBuilder();
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f));
            Selection.activeGameObject = wall.gameObject;
            LiveBuilderContourSelection.SetOwner(wall);

            CollectionAssert.DoesNotContain(unityKeyboard.Press(KeyCode.LeftArrow), LiveBuilderShortcutIds.ContourNudgeLeft, "точки не выбраны — стрелка не наша");

            LiveBuilderContourSelection.Add(0, 1);
            CollectionAssert.AreEqual(new[] { LiveBuilderShortcutIds.ContourNudgeLeft }, unityKeyboard.Press(KeyCode.LeftArrow), "выбраны точки — стрелка двигает их");
        }


        [Test]
        public void LiveBuilderOn_CameraFlying_KeysStayUnitys()
        {
            TurnOnLiveBuilder();
            Tools.viewTool = ViewTool.FPS;

            Assert.IsFalse(unityKeyboard.Press(KeyCode.S).Any(IsLiveBuilderShortcut), "камера летит — S не наша");
            Assert.IsFalse(unityKeyboard.Press(KeyCode.Alpha2).Any(IsLiveBuilderShortcut), "и цифры не наши");
        }


        // ── Live Builder выключен ────────────────────────────────────────

        [Test]
        public void LiveBuilderOff_KeysRunUnityShortcuts()
        {
            LiveBuilderActivation.SetToolActive(false);
            Assert.IsFalse(LiveBuilderActivation.IsToolActive);

            CollectionAssert.AreEqual(new[] { "Scene View/Toggle 2D Mode" }, unityKeyboard.Press(KeyCode.Alpha2));
            CollectionAssert.AreEqual(new[] { "Overlays/Show Tool Settings" }, unityKeyboard.Press(KeyCode.S));
            CollectionAssert.AreEqual(new[] { "Tools/Toggle Pivot Orientation" }, unityKeyboard.Press(KeyCode.X));
            Assert.IsFalse(unityKeyboard.Press(KeyCode.LeftArrow).Any(IsLiveBuilderShortcut), "стрелка — не Live Builder");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private void TurnOnLiveBuilder()
        {
            LiveBuilderActivation.SetToolActive(true);
            Assert.IsTrue(LiveBuilderActivation.IsToolActive, "Live Builder включился");
            Assert.IsTrue(sceneView.hasFocus, "окно сцены в фокусе — без него контексты Live Builder молчат");
        }

        private static bool IsLiveBuilderShortcut(string id)
        {
            return id.StartsWith(LiveBuilderShortcutIds.Category);
        }

        // Луч сверху на пол текущего этажа, в метрах от начала тестовой сцены.
        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }
    }
}
