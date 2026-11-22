using System.Collections.Generic;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Формы этапа 6 для построек вне зданий: открытая лестница (Open), пандус — режим Ramp «Коробки», двускатная крыша кнопкой Gable Roof,
    /// колонна — отдельный объект. И «Готово, когда» этапа: отдельно стоящий объект с пандусом, колоннами, аркой-вырезом и навесом — без ProBuilder.
    /// </summary>
    public class LiveBuilderOutdoorShapesTests
    {
        private const string TempFolder = "Assets/LiveBuilderOutdoorShapesTemp";
        private const float Tolerance = 0.01f;

        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;
        private LiveBuilderBoxMode modeBefore;
        private int columnSidesBefore;
        private BoxModelDrawMode drawModeBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            modeBefore = LiveBuilderBoxModeSettings.Mode;
            columnSidesBefore = LiveBuilderBoxModeSettings.ColumnSides;
            drawModeBefore = BoxModelDrawModeSetting.Current;
            Selection.objects = new Object[0];
            BoxModelDrawModeSetting.Current = BoxModelDrawMode.Auto;
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            LiveBuilderBoxModeSettings.Mode = modeBefore;
            LiveBuilderBoxModeSettings.ColumnSides = columnSidesBefore;
            BoxModelDrawModeSetting.Current = drawModeBefore;
            Selection.objects = selectionBefore;

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Открытая лестница ────────────────────────────────────────────

        // Вид хранится номером: старое «Ступени» (0) открывается как Open, «Сплошная» (1) — как Solid. Меш тот же — Meshes_MatchBaseline.
        [Test]
        public void OpenStairs_OldStyleNumbers_ReadAsOpenAndSolid()
        {
            var stairs = new GameObject("Stairs").AddComponent<BoxModelStairs>();
            var serialized = new SerializedObject(stairs);

            serialized.FindProperty("style").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(BoxModelStairsStyle.Solid, stairs.Style, "1 — «Сплошная» — Solid");

            serialized.FindProperty("style").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(BoxModelStairsStyle.Open, stairs.Style, "0 — старые «Ступени» — Open");
        }


        // ── Пандус ───────────────────────────────────────────────────────

        // Протянул 2 × 6 к +Z, высота 1: коробка с полным скосом верхнего ребра на стороне начала — подъём к концу протягивания.
        [Test]
        public void RampMode_DragAndTypedHeight_MakesFullChamferOnStartSide()
        {
            var part = DrawRamp(false);

            Assert.AreEqual(new Vector3(2f, 1f, 6f), part.Size, "коробка — протянутая и высотой 1");
            Assert.IsTrue(BoxModelRamps.IsRamp(part, out var risesToward), "пандус");
            Assert.AreEqual(4, risesToward, "поднимается к +Z — к концу протягивания");
            Assert.IsTrue(part.TryGetChamfer(2, 5, out var chamfer) && chamfer.IsFullOnFirstFace && chamfer.IsFullOnSecondFace, "полный скос ребра верх / −Z");
        }


        [Test]
        public void RampMode_Tab_TurnsRise()
        {
            var part = DrawRamp(true);

            Assert.IsTrue(BoxModelRamps.IsRamp(part, out var risesToward));
            Assert.AreEqual(BoxModelRamps.Clockwise(4), risesToward, "Tab — четверть оборота по часовой: к +X");
            Assert.AreEqual(1, part.Chamfers.Count, "старого скоса не осталось");
        }


        // Коробку растянули — скат во всю новую длину и высоту: хранится «Full», а не число.
        [Test]
        public void Ramp_BoxResized_StaysFull()
        {
            var part = DrawRamp(false);
            Undo.RecordObject(part, "Resize");
            part.Size = new Vector3(2f, 2f, 10f);
            var model = part.GetComponentInParent<BoxModel>();
            BoxModelRebuilder.Rebuild(model);

            Assert.IsTrue(part.TryGetChamfer(2, 5, out var chamfer));
            Assert.AreEqual(10f, chamfer.SizeOnFirstFaceIn(part.Size), Tolerance, "по верху — во всю новую длину");
            Assert.AreEqual(2f, chamfer.SizeOnSecondFaceIn(part.Size), Tolerance, "по стороне — во всю новую высоту");
            Assert.AreEqual(1f, HeightUnder(part, 1f, 5f), 0.02f, "на середине длины — середина высоты");
        }


        // Луч сверху попадает на наклон: на четверти длины — четверть высоты.
        [Test]
        public void Ramp_Collider_FollowsSlope()
        {
            var part = DrawRamp(false);

            Assert.AreEqual(0.25f, HeightUnder(part, 1f, 1.5f), 0.02f, "четверть длины — четверть высоты");
            Assert.AreEqual(0.75f, HeightUnder(part, 1f, 4.5f), 0.02f, "три четверти — три четверти");
        }


        // Новый объект — World: скат развёрнут по своей плоскости, V по скату — его длина, без растяжения.
        [Test]
        public void RampSlope_UvLengthEqualsSlopeLength()
        {
            var part = DrawRamp(false);
            var mesh = part.GetComponentInParent<BoxModel>().GetComponent<MeshFilter>().sharedMesh;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var slopeUvs = new List<Vector2>();

            foreach (var index in mesh.triangles)
                if (Mathf.Abs(normals[index].y) > 0.1f && Mathf.Abs(normals[index].y) < 0.99f)
                    slopeUvs.Add(uvs[index]);

            Assert.IsNotEmpty(slopeUvs, "у ската есть грани");
            Assert.AreEqual(Mathf.Sqrt(6f * 6f + 1f), slopeUvs.Max(uv => uv.y) - slopeUvs.Min(uv => uv.y), Tolerance, "V — длина ската");
            Assert.AreEqual(2f, slopeUvs.Max(uv => uv.x) - slopeUvs.Min(uv => uv.x), Tolerance, "U — ширина пандуса");
        }

        private static BoxModelPart DrawRamp(bool isTurned)
        {
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Ramp;
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            driver.Hold(FloorRay(0f, 0f), FloorRay(2f, 6f));

            if (isTurned)
                Assert.IsTrue(driver.Press(KeyCode.Tab), "Tab забрал пандус");

            driver.Release();
            driver.Type("1");
            driver.Press(KeyCode.Return);
            return InTestScene<BoxModelPart>().Single();
        }

        // Высота поверхности над полом под точкой (x, z) в осях объекта коробки — лучом сверху по коллайдеру.
        private static float HeightUnder(BoxModelPart part, float x, float z)
        {
            var model = part.GetComponentInParent<BoxModel>();
            var point = model.transform.TransformPoint(part.transform.localPosition + new Vector3(x, 0f, z));
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(point + Vector3.up * 20f, Vector3.down, out var hit, 40f), "луч попал");
            return hit.point.y - point.y;
        }


        // ── Двускатная крыша ─────────────────────────────────────────────

        // Здание 12 × 8: два пандуса спиной к спине в «Roof», конёк вдоль 12 м на 4 · tg 30° над крышей, свес 0,3 со всех сторон.
        [Test]
        public void GableRoof_Rectangle12x8_TwoRampsRidgeAlongLongSide()
        {
            var building = CreateRectangleBuilding(12f, 8f, 1);
            var roof = SplineBuildingGableRoof.Build(building, 30f, 0.3f);

            Assert.IsNotNull(roof, "крыша встала");
            Assert.AreEqual(SplineBuildingStoreys.FindRoof(building).transform, roof.transform.parent, "в этаже Roof");

            var parts = roof.GetComponentsInChildren<BoxModelPart>();
            Assert.AreEqual(2, parts.Length, "две коробки");
            Assert.IsTrue(parts.All(part => BoxModelRamps.IsRamp(part, out _)), "обе — пандусы");
            Assert.IsTrue(parts.All(part => Mathf.Abs(part.Size.x - 12.6f) < Tolerance), "конёк вдоль 12 м со свесом");

            var bounds = roof.GetComponent<MeshRenderer>().bounds;
            var roofTop = SplineBuildingStoreys.FindRoof(building).transform.position.y;
            Assert.AreEqual(4f * Mathf.Tan(30f * Mathf.Deg2Rad), bounds.max.y - roofTop, Tolerance, "конёк на 4 · tg 30° над крышей");
            Assert.AreEqual(12.6f, bounds.size.x, Tolerance, "свес 0,3 по длине");
            Assert.AreEqual(8.6f, bounds.size.z, Tolerance, "свес 0,3 по ширине");
            Assert.AreEqual(Origin.x - 0.3f, bounds.min.x, Tolerance, "свес за стену");
        }


        // Нажал ещё раз — тот же объект, снова две коробки; добавили этаж — крыша поднялась с этажом Roof.
        [Test]
        public void GableRoof_PressedAgain_SameObject_RisesWithNewStorey()
        {
            var building = CreateRectangleBuilding(12f, 8f, 1);
            var roof = SplineBuildingGableRoof.Build(building, 30f, 0.3f);
            var again = SplineBuildingGableRoof.Build(building, 30f, 0.3f);

            Assert.AreSame(roof, again, "тот же объект");
            Assert.AreEqual(2, again.GetComponentsInChildren<BoxModelPart>().Length, "две коробки, а не четыре");

            var heightBefore = roof.transform.position.y;
            building.StoreyCount = 2;
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.AreEqual(heightBefore + building.StoreyHeight, roof.transform.position.y, Tolerance, "крыша поднялась на этаж");
        }


        [Test]
        public void GableRoof_LShapedContour_CannotBuild()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 4f), new Vector3(4f, 0f, 4f),
                new Vector3(4f, 0f, 8f), new Vector3(0f, 0f, 8f));

            Assert.IsFalse(SplineBuildingGableRoof.CanBuild(building, out var reason), "Г-образный — нельзя");
            Assert.IsNotEmpty(reason, "с причиной");
            Assert.IsNull(SplineBuildingGableRoof.Build(building, 30f, 0.3f));
        }


        // Крыша и выключенный парапет — один Ctrl+Z.
        [Test]
        public void GableRoof_OneUndo_RemovesRoofAndBringsParapetBack()
        {
            var building = CreateRectangleBuilding(12f, 8f, 1);
            building.HasParapet = true;
            Undo.IncrementCurrentGroup();

            SplineBuildingGableRoof.Build(building, 30f, 0.3f);
            Assert.IsFalse(building.HasParapet, "парапет выключен");

            Undo.PerformUndo();
            Assert.IsNull(SplineBuildingGableRoof.FindRoofObject(SplineBuildingStoreys.FindRoof(building).transform), "крыши нет");
            Assert.IsTrue(building.HasParapet, "парапет вернулся");
        }

        private SplineBuilding CreateRectangleBuilding(float length, float width, int storeys)
        {
            return testScene.CreateBuilding(storeys, Vector3.zero, new Vector3(length, 0f, 0f), new Vector3(length, 0f, width), new Vector3(0f, 0f, width));
        }


        // ── Колонна ──────────────────────────────────────────────────────

        // Режим Column: протянул квадрат 1 × 1, высота 3 — колонна из 16 сторон, сечение касается сторон квадрата.
        [Test]
        public void ColumnMode_DragSquareAndHeight_MakesColumn()
        {
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Column;
            LiveBuilderBoxModeSettings.ColumnSides = 16;
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            driver.Drag(FloorRay(0f, 0f), FloorRay(1f, 1f));
            driver.Type("3");
            driver.Press(KeyCode.Return);

            var column = InTestScene<LiveBuilderColumn>().Single();
            Assert.IsEmpty(InTestScene<BoxModelPart>(), "коробки нет — только колонна");
            Assert.AreEqual(16, column.Sides);
            Assert.AreEqual(new Vector3(1f, 3f, 1f), column.Size, "квадрат и высота");

            var bounds = column.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(1f, bounds.size.x, Tolerance, "диаметр — сторона квадрата");
            Assert.AreEqual(1f, bounds.size.z, Tolerance);
            Assert.AreEqual(3f, bounds.size.y, Tolerance);
            Assert.IsNotNull(column.GetComponent<MeshCollider>(), "с коллайдером");
        }


        // Без фаски: бок — 4 вершины на сторону, верх и низ — середина и угол на сторону.
        [Test]
        public void Column_Sides4And32_VertexCount()
        {
            Assert.AreEqual(4 * 4 + 2 * (4 + 1), CreateColumn(null, 4).GetComponent<MeshFilter>().sharedMesh.vertexCount, "4 стороны");
            Assert.AreEqual(4 * 32 + 2 * (32 + 1), CreateColumn(null, 32).GetComponent<MeshFilter>().sharedMesh.vertexCount, "32 стороны");

            var square = CreateColumn(null, 4).GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(1f, square.size.x, Tolerance, "4 стороны — квадрат во весь прямоугольник");
        }


        // Фаска из двух кусков: по две полосы у верхней и нижней кромки, крышки утоплены на её размер.
        [Test]
        public void Column_Bevel_AddsRimRings()
        {
            var column = CreateColumn(null, 16);
            column.BevelSize = 0.05f;
            column.BevelSegments = 2;
            LiveBuilderColumnRebuilder.Rebuild(column);

            var mesh = column.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(5 * 4 * 16 + 2 * (16 + 1), mesh.vertexCount, "две полосы снизу, бок, две сверху");

            var topCap = mesh.vertices.Where(vertex => Mathf.Abs(vertex.y - 3f) < 0.0001f).ToList();
            Assert.Less(topCap.Max(vertex => vertex.x), 1f - 0.04f, "верх утоплен на фаску");
        }


        // Вырез «всё, чего касается» режет объект вокруг, а колонну насквозь — нет.
        [Test]
        public void Column_NotCutByCutterCrossingIt()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(4f, 0.5f, 4f), BoxModelPartMode.Add);
            var column = LiveBuilderColumnFactory.CreateInBox(model, Matrix4x4.identity, new Vector3(1f, 0.5f, 1f), new Vector3(2f, 3.5f, 2f));
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var columnVerticesBefore = column.GetComponent<MeshFilter>().sharedMesh.vertexCount;

            var cutter = BoxModelFactory.CreateCutter(model.transform, Origin + new Vector3(1.5f, 0f, 1.5f), Quaternion.identity, new Vector2(3f, 2f), 3f);
            cutter.Scope = GeometryCutterScope.EverythingItTouches;
            LiveBuilderRebuildQueue.RunFinalPassNow();

            Assert.AreEqual(columnVerticesBefore, column.GetComponent<MeshFilter>().sharedMesh.vertexCount, "колонна не прорезана");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1.5f, 1.5f, -1f), Vector3.forward, 6f), "колонна стоит в вырезе");
        }


        // Материал, брошенный на бок, — в бок колонны; сверху — в верх.
        [Test]
        public void Column_MaterialDrop_AssignsSide()
        {
            var column = CreateColumn(null, 16);
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            Physics.SyncTransforms();

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(new Ray(Origin + new Vector3(0.5f, 1.5f, -5f), Vector3.forward), false, out var side));
            Assert.AreSame(column, side.Owner, "место — колонна");
            Assert.IsTrue(side.TryAssign(material));
            Assert.AreSame(material, column.Material, "бок");

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(new Ray(Origin + new Vector3(0.5f, 10f, 0.5f), Vector3.down), false, out var top));
            Assert.AreEqual("column top", top.Description);
            Object.DestroyImmediate(material);
        }


        // Экземпляры префаба в разных местах — один меш префаба.
        [Test]
        public void Column_PrefabInstances_ShareMesh()
        {
            AssetDatabase.CreateFolder("Assets", "LiveBuilderOutdoorShapesTemp");
            var column = CreateColumn(null, 12);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(column.gameObject, TempFolder + "/Column.prefab", InteractionMode.AutomatedAction);
            var other = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<LiveBuilderColumn>();
            other.transform.position = Origin + new Vector3(5.53f, 0f, 2.29f);

            BoxModelPrefabInstances.ForgetAllChecks();
            LiveBuilderColumnRebuilder.RebuildChangedColumns();

            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(column, out _), "первый экземпляр — меш префаба");
            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(other, out _), "второй — тоже");
            Assert.AreSame(column.GetComponent<MeshFilter>().sharedMesh, other.GetComponent<MeshFilter>().sharedMesh);
        }


        // Повтор на колонне — копии в её же меше.
        [Test]
        public void Column_WithArray_CopiesInOwnMesh()
        {
            var column = CreateColumn(null, 8);
            var single = column.GetComponent<MeshFilter>().sharedMesh.vertexCount;
            var array = column.gameObject.AddComponent<LiveBuilderArray>();
            array.CountX = 3;
            array.CountY = 1;
            array.StepX = 2f;
            LiveBuilderRebuildQueue.RunFinalPassNow();

            var mesh = column.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(single * 3, mesh.vertexCount, "три колонны в одном меше");
            Assert.AreEqual(5f, mesh.bounds.size.x, Tolerance, "через 2 м");
        }


        // Клик выделяет колонну, луч упирается в её коллайдер; «Коробка» её правит.
        [Test]
        public void Column_ClickSelects_RayHitsCollider()
        {
            var column = CreateColumn(null, 16);
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            var ray = new Ray(Origin + new Vector3(0.5f, 1.5f, -5f), Vector3.forward);
            Physics.SyncTransforms();

            var hit = LiveBuilderSceneHit.Find(ray);
            Assert.IsTrue(hit.IsColumnCloser, "под лучом — колонна");
            Assert.IsNull(hit.PathOwner, "не стена");
            Assert.IsTrue(boxTool.CanEdit(column.gameObject), "Коробка правит колонну");
            Assert.IsTrue(LiveBuilderClickSelection.TrySelectOwn(ray, boxTool), "клик забрал");
            Assert.AreSame(column.gameObject, Selection.activeGameObject, "выделена колонна");
        }

        // Колонна 1 × 3 × 1 в углу Origin, собрана.
        private static LiveBuilderColumn CreateColumn(BoxModel parentModel, int sides)
        {
            var column = LiveBuilderColumnFactory.CreateInBox(parentModel, Matrix4x4.Translate(Origin), Vector3.zero, new Vector3(1f, 3f, 1f));
            column.Sides = sides;
            LiveBuilderColumnRebuilder.Rebuild(column);
            return column;
        }


        // ── Готово, когда ────────────────────────────────────────────────

        // Отдельно стоящий двор: плита, пандус к ней, четыре колонны, стена с аркой насквозь и навес 6 × 4 с двускатной крышей — всё Live Builder.
        [Test]
        public void OutdoorObject_RampColumnsArchAndGableCanopy_WithoutProBuilder()
        {
            var yard = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(yard, Vector3.zero, new Vector3(10f, 1f, 6f), BoxModelPartMode.Add);
            var ramp = BoxModelFactory.CreatePart(yard, new Vector3(10f, 0f, 0f), new Vector3(16f, 1f, 2f), BoxModelPartMode.Add);
            BoxModelRamps.MakeRamp(ramp, 1);

            for (var index = 0; index < 4; index++)
                LiveBuilderColumnFactory.CreateInBox(yard, Matrix4x4.identity, new Vector3(1f + index * 2.5f, 1f, 4.5f), new Vector3(1.4f + index * 2.5f, 4f, 4.9f));

            BoxModelFactory.CreatePart(yard, new Vector3(0f, 1f, 0f), new Vector3(10f, 4f, 0.4f), BoxModelPartMode.Add);
            var arch = BoxModelFactory.CreateCutter(yard.transform, Origin + new Vector3(5f, 1f, 0.2f), Quaternion.identity, new Vector2(2f, 2.5f), 1f);
            arch.TopCornerRadius = 1f;

            var canopy = testScene.CreateBuilding(1, new Vector3(20f, 0f, 0f), new Vector3(26f, 0f, 0f), new Vector3(26f, 0f, 4f), new Vector3(20f, 0f, 4f));
            var gable = SplineBuildingGableRoof.Build(canopy, 30f, 0.3f);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            LiveBuilderDraftBuilds.FinishAll();

            Assert.IsTrue(BoxModelRamps.IsRamp(ramp, out var risesToward) && risesToward == 1, "пандус поднимается к плите");
            Assert.AreEqual(4, yard.GetComponentsInChildren<LiveBuilderColumn>().Length, "четыре колонны");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 2.5f, -2f), Vector3.forward, 2.3f), "в арке — проход");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4.2f, 3.4f, -2f), Vector3.forward, 2.3f), "у плеча арки — стена");
            Assert.IsNotNull(gable, "навес с двускатной крышей");

            foreach (var filter in new[] { yard.gameObject, canopy.gameObject }.SelectMany(root => root.GetComponentsInChildren<MeshFilter>()))
                Assert.Greater(filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0, 0, filter.name + " собран");

            var proBuilderParts = new[] { yard.gameObject, canopy.gameObject }.SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null && component.GetType().Namespace != null && component.GetType().Namespace.Contains("ProBuilder"));
            Assert.IsEmpty(proBuilderParts, "без ProBuilder");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Луч сверху на пол текущего этажа, в метрах от начала тестовой сцены.
        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }

        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
