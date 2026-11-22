using System.Linq;
using System.Reflection;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Секция Drawing панели и полосы: отчёт жеста (<see cref="LiveBuilderDrawingReport"/>) — шаг, живые размеры, набранное с замком,
    /// что сделают Enter и Esc — у коробки, лестницы и линии стены; полоса — восемь инструментов, этаж и сетка.
    /// Жесты — драйвером, без окна сцены; IMGUI не рисуется, проверяются данные, которые панель показывает.
    /// </summary>
    public class LiveBuilderDrawingPanelTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;
        private LiveBuilderTool toolBefore;
        private LiveBuilderBoxMode boxModeBefore;
        private int storeyBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            toolBefore = LiveBuilderToolPalette.Current;
            boxModeBefore = LiveBuilderBoxModeSettings.Mode;
            storeyBefore = LiveBuilderStoreys.CurrentStorey;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;
            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            LiveBuilderToolPalette.Current = toolBefore;
            LiveBuilderBoxModeSettings.Mode = boxModeBefore;
            LiveBuilderStoreys.CurrentStorey = storeyBefore;
            Selection.objects = selectionBefore;
        }


        // ── Коробка ──────────────────────────────────────────────────────

        // Шаг глубины, набрано «4X3X1,5»: все три числа с замком, Enter — ровно такая коробка; после Enter — отчёта нет.
        [Test]
        public void DrawingReport_BoxDepthStep_ShowsTypedAndEnterResult()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            var driver = new LiveBuilderToolDriver(boxTool);
            Assert.IsNull(boxTool.DrawingReport, "ничего не рисуешь — секции Drawing нет");

            driver.Drag(FloorRay(0f, 0f), FloorRay(2f, 2f));
            driver.Type("4X3X1,5");
            var report = boxTool.DrawingReport;

            Assert.AreEqual("Box", report.Title);
            Assert.AreEqual((2, 2, "Height"), (report.StepNumber, report.StepCount, report.StepName));
            Assert.AreEqual("4×3×1,5", report.TypedText);
            AssertSizes(report, ("Width", 4f, true), ("Length", 3f, true), ("Height", 1.5f, true));
            Assert.AreEqual("box " + LiveBuilderDrawingReport.FormatSizes(4f, 3f, 1.5f) + " — adds", report.EnterResult);
            Assert.AreEqual("clear typing", report.EscResult);

            driver.Press(KeyCode.Return);

            Assert.IsNull(boxTool.DrawingReport, "Enter — коробка встала, рисование кончилось");
            var size = InTestScene<BoxModelPart>().Single().Size;
            Assert.AreEqual(new Vector3(4f, 1.5f, 3f), size, "стороны по X и Z, высота — третье число");
        }


        // Пока тянешь, «4X3» — стороны с замком, Enter ещё ничего не делает; отпустил — шаг глубины, стороны так и с замком, мышью — высота.
        [Test]
        public void DrawingReport_BoxRectangleStep_TypedSidesStayLocked()
        {
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            var driver = new LiveBuilderToolDriver(boxTool);

            driver.Hold(FloorRay(0f, 0f), FloorRay(2f, 2f));
            driver.Type("4X3");
            var report = boxTool.DrawingReport;

            Assert.AreEqual((1, "Rectangle"), (report.StepNumber, report.StepName));
            AssertSizes(report, ("Width", 4f, true), ("Length", 3f, true));
            Assert.IsNull(report.EnterResult, "пока тянешь, Enter не наш");

            driver.Release();
            report = boxTool.DrawingReport;

            Assert.AreEqual((2, "Height"), (report.StepNumber, report.StepName));
            AssertSizes(report, ("Width", 4f, true), ("Length", 3f, true), ("Height", 0f, false));
            Assert.AreEqual("", report.TypedText, "стороны уже в прямоугольнике — набор пуст");
            Assert.AreEqual("drop the box", report.EscResult);
        }


        // ── Лестница ─────────────────────────────────────────────────────

        // Шаг высоты: сначала высота по настройкам, мышиная; набрал «2,4» — с замком, Enter — лестница этой высоты.
        [Test]
        public void DrawingReport_StairsHeightStep_ShowsTypedHeight()
        {
            var stairsTool = LiveBuilderToolPalette.Get<LiveBuilderStairsTool>();
            var driver = new LiveBuilderToolDriver(stairsTool);

            driver.Drag(FloorRay(0f, 0f), FloorRay(2f, 4f));
            var report = stairsTool.DrawingReport;

            Assert.AreEqual((2, 2, "Height"), (report.StepNumber, report.StepCount, report.StepName));
            AssertSizes(report, ("Width", 2f, false), ("Length", 4f, false), ("Height", LiveBuilderStairsSettings.Height, false));

            driver.Type("2,4");
            report = stairsTool.DrawingReport;

            Assert.AreEqual("2,4", report.TypedText);
            Assert.AreEqual(("Height", 2.4f, true), SizeOf(report, 2));
            StringAssert.StartsWith("stairs up " + LiveBuilderSceneText.FormatMeters(2.4f) + " m", report.EnterResult);

            driver.Press(KeyCode.Return);

            Assert.IsNull(stairsTool.DrawingReport);
            Assert.AreEqual(2.4f, InTestScene<BoxModelStairs>().Single().Height, 0.001f);
        }


        // ── Линия стены ──────────────────────────────────────────────────

        // Первая точка стоит: следующая — вторая; набрал «5» — отрезок 5 с замком, Enter — точка на 5 м; дальше Enter — готово.
        [Test]
        public void DrawingReport_WallLine_ShowsTypedSegmentLength()
        {
            var wallTool = LiveBuilderToolPalette.Get<LiveBuilderWallTool>();
            var driver = new LiveBuilderToolDriver(wallTool);

            driver.Click(FloorRay(0f, 0f));
            driver.Move(FloorRay(2f, 0f));
            var report = wallTool.DrawingReport;

            Assert.AreEqual(("Wall", 0, "Point 2"), (report.Title, report.StepCount, report.StepName));
            Assert.AreEqual(("Segment", 2f, false), SizeOf(report, 0));
            Assert.AreEqual("drop — needs 2 points", report.EnterResult);

            driver.Type("5");
            report = wallTool.DrawingReport;

            Assert.AreEqual(("Segment", 5f, true), SizeOf(report, 0));
            Assert.AreEqual("point at " + LiveBuilderSceneText.FormatMeters(5f) + " m", report.EnterResult);
            Assert.AreEqual("clear the length", report.EscResult);

            driver.Press(KeyCode.Return);
            report = wallTool.DrawingReport;

            Assert.AreEqual("Point 3", report.StepName);
            Assert.AreEqual(("Total", 5f, false), SizeOf(report, 1));
            Assert.AreEqual("finish the wall", report.EnterResult);

            driver.Press(KeyCode.Return);
            Assert.IsNull(wallTool.DrawingReport, "стена готова");
        }


        // ── Полосы ───────────────────────────────────────────────────────

        // Горизонтальная — восемь инструментов, этаж и сетка; вертикальная — без сетки. Этаж сменился — полоса показывает новый.
        [Test]
        public void ToolbarElements_HaveEightTools_StoreyAndGrid()
        {
            var horizontal = new LiveBuilderPanelToolbarElements(false);
            var vertical = new LiveBuilderPanelToolbarElements(true);

            Assert.AreEqual(8, horizontal.ToolToggleCount);
            Assert.AreEqual(8, vertical.ToolToggleCount);
            Assert.IsNotNull(horizontal.Root.Q<EditorToolbarDropdown>(), "в горизонтальной — шаг сетки");
            Assert.IsNull(vertical.Root.Q<EditorToolbarDropdown>(), "в вертикальной сетки нет");

            LiveBuilderStoreys.CurrentStorey = 2;
            horizontal.Refresh();

            Assert.AreEqual(LiveBuilderStoreys.CurrentStoreyNumber.ToString(), horizontal.Root.Q<Label>("LiveBuilderStorey").text);
        }


        // Unity сама предлагает раскладки, которые оверлей умеет: Panel и обе полосы.
        [Test]
        public void PanelOverlay_SupportsBothToolbarLayouts()
        {
            var supportedLayouts = typeof(Overlay).GetProperty("supportedLayouts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(supportedLayouts, "у Overlay нет supportedLayouts — проверить на новой версии Unity");

            var layouts = (Layout)supportedLayouts.GetValue(new LiveBuilderPanelOverlay());

            Assert.AreEqual(Layout.Panel | Layout.HorizontalToolbar | Layout.VerticalToolbar, layouts & (Layout.Panel | Layout.HorizontalToolbar | Layout.VerticalToolbar));
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void AssertSizes(LiveBuilderDrawingReport report, params (string Label, float Meters, bool IsTyped)[] expected)
        {
            Assert.AreEqual(expected.Length, report.Sizes.Count, "сколько размеров");

            for (var index = 0; index < expected.Length; index++)
                Assert.AreEqual(expected[index], SizeOf(report, index), "размер " + index);
        }

        // Метры — с округлением до миллиметра: сравниваются кортежем.
        private static (string, float, bool) SizeOf(LiveBuilderDrawingReport report, int index)
        {
            var size = report.Sizes[index];
            return (size.Label, Mathf.Round(size.Meters * 1000f) / 1000f, size.IsTyped);
        }


        // Луч сверху на пол текущего этажа, в метрах от начала тестовой сцены.
        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }

        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
