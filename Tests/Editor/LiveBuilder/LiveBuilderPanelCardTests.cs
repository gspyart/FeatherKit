using System.Collections.Generic;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Карточка выделенного в панели — без рисования, по её данным: какие секции и в каком порядке у каждого вида, пустые — «+ Add»,
    /// путь в заголовке, плашки видов при разном выделении, «—» у разных чисел, Make Cut / Make Box с Ctrl+Z, «Into» над зданием,
    /// подгонка текстуры Fit и Reset, Array «+ Add».
    /// </summary>
    public class LiveBuilderPanelCardTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private LiveBuilderSelectionCardSet cards;
        private LiveBuilderTextureTestKit kit;
        private Object[] selectionBefore;
        private int storeyBefore;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            cards = new LiveBuilderSelectionCardSet();
            kit = new LiveBuilderTextureTestKit();
            selectionBefore = Selection.objects;
            storeyBefore = LiveBuilderStoreys.CurrentStorey;
            Selection.objects = new Object[0];
        }


        [TearDown]
        public void CloseScene()
        {
            LiveBuilderIntoTarget.RememberHover(null, null);
            LiveBuilderStoreys.CurrentStorey = storeyBefore;
            cards.Dispose();
            kit.Dispose();
            testScene.Dispose();
            Selection.objects = selectionBefore;
        }


        // ── Секции по видам ──────────────────────────────────────────────

        [Test]
        public void Box_Sections_InFixedOrder_EmptyAreAdd()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(4f, 3f, 2.5f), BoxModelPartMode.Add);

            CollectionAssert.AreEqual(new[] { "Construction", "Material", "Chamfer +", "Array +", "Move & Copies" }, SectionsOf(part.gameObject));
        }


        [Test]
        public void Cut_Sections_InFixedOrder()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(4f, 3f, 4f), BoxModelPartMode.Add);
            var cutter = BoxModelFactory.CreateCutterBox(model, new Vector3(1f, 1f, 1f), new Vector3(2f, 2f, 2f));

            CollectionAssert.AreEqual(new[] { "Construction", "Cut", "Material", "Array +" }, SectionsOf(cutter.gameObject),
                "вырез двигают его ручки, точного переноса у него нет");
        }


        [Test]
        public void Building_Sections_ConstructionFirst_PointsAndInside_ThenMaterialArrayMove()
        {
            var building = CreateSectionWithBuilding(out _);

            CollectionAssert.AreEqual(new[] { "Construction", "Points", "Inside", "Material", "Array +", "Move & Copies" }, SectionsOf(building.gameObject));
        }


        [Test]
        public void NotLiveBuilderObject_OnlyGroup()
        {
            var plain = new GameObject("Plain");

            CollectionAssert.AreEqual(new[] { "Group" }, SectionsOf(plain));
        }


        // Повтор уже есть — секция не «+ Add», а с числами; в заголовке — сколько копий.
        [Test]
        public void ArrayAdd_AlongX_FillsArraySection()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 3f, 2f), BoxModelPartMode.Add);

            LiveBuilderArrayActions.AddAlongAxis(part.gameObject, 0);
            var array = part.GetComponent<LiveBuilderArray>();

            Assert.IsNotNull(array, "Along X ставит повтор");
            Assert.AreEqual(3, array.CountX);
            Assert.AreEqual(1, array.CountY, "по другим осям копий нет");
            Assert.Greater(array.StepX, 2f, "шаг — шире коробки, копии не слиплись");
            Assert.Contains("Array", SectionsOf(part.gameObject), "секция Array — с полями, не «+ Add»");
        }


        // ── Заголовок ────────────────────────────────────────────────────

        [Test]
        public void CutInBuildingStorey_Path_IsSectionBuildingStoreyWindow()
        {
            var building = CreateSectionWithBuilding(out var section);
            var storey = SplineBuildingStoreys.ContainerOf(building, 1);
            var window = BoxModelFactory.CreateCutter(storey.transform, storey.transform.position + new Vector3(3f, 1f, 0f), Quaternion.identity,
                new Vector2(1.2f, 1.5f), 0.5f);
            window.name = "Window";

            var path = LiveBuilderCardHeader.PathOf(window.gameObject).ConvertAll(link => link.name);

            CollectionAssert.AreEqual(new[] { "Section", "Building", storey.name, "Window" }, path);
            StringAssert.StartsWith("Storey 2", storey.name, "второй этаж здания");
        }


        [Test]
        public void TwoBoxesAndCut_KindChips_OnlySharedSections()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var first = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            var second = BoxModelFactory.CreatePart(model, new Vector3(3f, 0f, 0f), new Vector3(5f, 2f, 2f), BoxModelPartMode.Add);
            var cutter = BoxModelFactory.CreateCutterBox(model, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f));
            var selected = new[] { first.gameObject, second.gameObject, cutter.gameObject };

            Assert.AreEqual("2 Box · 1 Cut", LiveBuilderSelectionKinds.DescribeCounts(selected));
            CollectionAssert.AreEqual(new[] { "Array +" }, SectionsOf(selected), "общие: Array; Move — только если двигаются все");
            CollectionAssert.AreEqual(new[] { "Construction", "Material", "Chamfer +", "Array +", "Move & Copies" }, SectionsOf(first.gameObject, second.gameObject),
                "одного вида — все секции, правка всем");
            CollectionAssert.AreEquivalent(new[] { first.gameObject, second.gameObject }, LiveBuilderSelectionKinds.OfKind(selected, LiveBuilderSelectionKind.Box),
                "клик по плашке «2 Box» — только коробки");
        }


        [Test]
        public void ThreeBoxesWithDifferentWidth_SizeX_IsMixed()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var parts = new[]
            {
                BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(1f, 3f, 2f), BoxModelPartMode.Add),
                BoxModelFactory.CreatePart(model, new Vector3(4f, 0f, 0f), new Vector3(6f, 3f, 2f), BoxModelPartMode.Add),
                BoxModelFactory.CreatePart(model, new Vector3(8f, 0f, 0f), new Vector3(11f, 3f, 2f), BoxModelPartMode.Add)
            };

            var size = LiveBuilderConstructionCard.SizeOf(parts);

            Assert.IsTrue(size[0].IsMixed, "ширина разная — «—»");
            Assert.IsFalse(size[1].IsMixed, "высота одна");
            Assert.AreEqual(3f, size[1].Value, 0.001f);
            Assert.IsFalse(size[2].IsMixed, "глубина одна");
        }


        // ── Make Cut / Make Box ──────────────────────────────────────────

        [Test]
        public void MakeCut_ThenMakeBox_RoundTrip_OneUndoEach()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(4f, 3f, 4f), BoxModelPartMode.Add);
            var part = BoxModelFactory.CreatePart(model, new Vector3(1f, 1f, 1f), new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            var boxObject = part.gameObject;
            Selection.activeGameObject = boxObject;

            Undo.IncrementCurrentGroup();
            BoxModelActions.ToggleAddCutOfSelected();
            Assert.IsNotNull(boxObject.GetComponent<GeometryCutter>(), "Make Cut — вырез");
            CollectionAssert.Contains(SectionsOf(boxObject), "Cut", "у выреза — секция Cut");

            Undo.IncrementCurrentGroup();
            BoxModelActions.ToggleAddCutOfSelected();
            Assert.IsNotNull(boxObject.GetComponent<BoxModelPart>(), "Make Box — снова коробка");

            Undo.PerformUndo();
            Assert.IsNotNull(boxObject.GetComponent<GeometryCutter>(), "один Ctrl+Z — снова вырез");

            Undo.PerformUndo();
            Assert.IsNotNull(boxObject.GetComponent<BoxModelPart>(), "второй Ctrl+Z — снова коробка");
            Assert.IsNull(boxObject.GetComponent<GeometryCutter>());
        }


        // ── Into ─────────────────────────────────────────────────────────

        [Test]
        public void IntoLine_OnBuildingFloor_NamesStorey()
        {
            var building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(12f, 0f, 0f), new Vector3(12f, 0f, 8f), new Vector3(0f, 0f, 8f));
            building.name = "Building";
            Physics.SyncTransforms();
            LiveBuilderStoreys.CurrentStorey = SplineBuildingLevelAlignment.BaseStoreyOf(building) + 1;
            var secondFloor = building.FirstFloorWorldHeight + building.StoreyHeight;
            var ray = new Ray(Origin + new Vector3(6f, secondFloor + 1.5f, 4f), Vector3.down);

            Assert.IsTrue(LiveBuilderBuildingContext.TryFind(ray, out var context), "луч над полом второго этажа находит здание, этаж — выбранный");
            LiveBuilderIntoTarget.RememberHover(context, null);
            var into = LiveBuilderToolPalette.Get<LiveBuilderWallTool>().IntoTarget;

            StringAssert.Contains("Building", into);
            StringAssert.EndsWith("Storey 2", into, "куда встанет стена — второй этаж здания");
        }


        // ── Текстура ─────────────────────────────────────────────────────

        // Одна плитка ровно на всю грань — и по миру со сдвигом, и с поворотом.
        [Test]
        public void FitToFace_OneTilePerSide()
        {
            var faceMin = new Vector2(1f, 2f);
            var faceMax = new Vector2(4f, 4.5f);
            var worldShift = new Vector2(0.7f, 13.3f);
            var tile = new Vector2(2f, 3f);

            for (var turns = 0; turns < 4; turns++)
            {
                var fit = LiveBuilderTextureMapping.FitToFace(faceMin, faceMax, worldShift, tile, turns);
                var uvs = new[] { faceMin, faceMax, new Vector2(faceMin.x, faceMax.y), new Vector2(faceMax.x, faceMin.y) }
                    .Select(corner => LiveBuilderTextureMapping.Map(corner, worldShift, fit, tile)).ToArray();

                Assert.AreEqual(0f, uvs.Min(uv => uv.x), 0.001f, "поворот " + turns + ": U от 0");
                Assert.AreEqual(1f, uvs.Max(uv => uv.x), 0.001f, "поворот " + turns + ": U до 1");
                Assert.AreEqual(0f, uvs.Min(uv => uv.y), 0.001f, "поворот " + turns + ": V от 0");
                Assert.AreEqual(1f, uvs.Max(uv => uv.y), 0.001f, "поворот " + turns + ": V до 1");
            }
        }


        [Test]
        public void Reset_ClearsFitOnly()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            var brick = kit.CreateMaterial("Brick", new Vector2(1f, 1f));
            var top = LiveBuilderMaterialSlots.PlacesOf(part.gameObject).Find(place => place.Name == "Top").Slot;
            top.TryAssign(brick, new LiveBuilderTextureFit(new Vector2(0.5f, 0f), new Vector2(2f, 2f), 1));

            LiveBuilderMaterialCard.ResetFits(new[] { top });

            Assert.AreEqual(brick, part.TopMaterial, "материал остался");
            Assert.IsTrue(top.Fit.Get() == null || top.Fit.Get().IsDefault, "подгонка — как есть");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Названия секций по порядку; пустая — с «+».
        private List<string> SectionsOf(params GameObject[] selected)
        {
            return cards.ShownFor(selected).ConvertAll(card => card.IsEmptyFor(selected) ? card.Title + " +" : card.Title);
        }


        // Объект «Section», в нём здание «Building» 12 × 8 в три этажа, этажи-контейнеры на месте.
        private BoxModel CreateSectionModel()
        {
            var section = BoxModelFactory.CreateModel(Origin);
            section.name = "Section";
            return section;
        }

        private SplineBuilding CreateSectionWithBuilding(out BoxModel section)
        {
            section = CreateSectionModel();
            var building = testScene.CreateBuilding(section, 3, Vector3.zero, new Vector3(12f, 0f, 0f), new Vector3(12f, 0f, 8f), new Vector3(0f, 0f, 8f));
            building.name = "Building";

            for (var storey = 0; storey < building.StoreyCount; storey++)
                SplineBuildingStoreys.ContainerOf(building, storey);

            return building;
        }
    }
}
