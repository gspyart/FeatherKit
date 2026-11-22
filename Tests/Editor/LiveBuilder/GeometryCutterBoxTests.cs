using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Вырез-коробка (<see cref="GeometryCutter"/> по осям объекта, <see cref="GeometryCutterSolverBoxes"/>): режет свой объект теми же клетками,
    /// что вырезающая коробка; повёрнутая — вычитанием; мельче стены — ниша вычитанием, насквозь — проём по линии; материалы сторон — на стенках дыры;
    /// «Add ⇄ Cut» превращает коробку в вырез и обратно одним шагом Ctrl+Z.
    /// </summary>
    public class GeometryCutterBoxTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;
        private static readonly Vector3 HoleMin = new(0.5f, 1f, 0.5f);
        private static readonly Vector3 HoleMax = new(1.5f, 5f, 1.5f);

        private LiveBuilderTestScene testScene;
        private Material floorMaterial;
        private Material wallMaterial;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Cutter Box Floor" };
            wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Cutter Box Wall" };
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
            Object.DestroyImmediate(floorMaterial);
            Object.DestroyImmediate(wallMaterial);
        }


        // ── Клетки солвера ───────────────────────────────────────────────

        [Test]
        public void CutterBox_GivesSameCellsAsCutBoxInSamePlace()
        {
            var model = CreateBlock();
            var cutBox = BoxModelFactory.CreatePart(model, HoleMin, HoleMax, BoxModelPartMode.Cut);
            BoxModelFactory.CreatePart(model, new Vector3(1f, 0f, 1f), new Vector3(3f, 1.5f, 3f), BoxModelPartMode.Add);
            var cutBoxCells = CellsOf(model, out var cutBoxSolverBox);
            var cutBoxVertices = MeshOf(model).vertices;

            Object.DestroyImmediate(cutBox.gameObject);
            var cutterBox = BoxModelFactory.CreateCutterBox(model, HoleMin, HoleMax);
            cutterBox.transform.SetSiblingIndex(1);
            var cutterCells = CellsOf(model, out var cutterSolverBox);

            Assert.IsTrue(GeometryCutterSolverBoxes.IsSolverPiece(cutterBox), "коробка по осям объекта — кусок объекта");
            Assert.AreEqual(cutBoxSolverBox.Box.Min, cutterSolverBox.Box.Min, "угол — до миллиметра");
            Assert.AreEqual(cutBoxSolverBox.Box.Max, cutterSolverBox.Box.Max, "размер — до миллиметра");
            Assert.AreEqual(cutBoxCells, cutterCells, "клетки те же");
            CollectionAssert.AreEqual(cutBoxVertices, MeshOf(model).vertices, "меш тот же");
            CollectionAssert.IsEmpty(GeometryCutterLookup.CuttersFor(model, new Bounds(Origin, Vector3.one * 100f)), "свой объект вырез не вычитает");
        }


        [Test]
        public void RotatedCutterBox_CutsBySubtraction()
        {
            var model = CreateBlock();
            var cutter = BoxModelFactory.CreateCutterBox(model, HoleMin, HoleMax);
            cutter.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            BoxModelRebuilder.Rebuild(model);

            Assert.IsFalse(GeometryCutterSolverBoxes.IsSolverPiece(cutter), "повёрнутая на 30° — не коробка объекта");
            Assert.AreEqual(LiveBuilderCutMethod.MeshSubtraction, LiveBuilderCutRules.MethodOf(cutter, model));
            Assert.IsFalse(HasSolverBoxOf(model, cutter), "в солвере её нет");
            CollectionAssert.Contains(GeometryCutterLookup.CuttersFor(model, new Bounds(Origin, Vector3.one * 100f)), cutter, "вычитается из меша");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1f, 5f, 1f), Vector3.down, 3.5f), "дыра есть");
        }


        // ── Стены ────────────────────────────────────────────────────────

        [Test]
        public void CutterBoxThinnerThanWall_IsNiche_ThroughWall_IsLineHole()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var wall = testScene.CreateWall(model, Vector3.zero, new Vector3(6f, 0f, 0f));
            var niche = BoxModelFactory.CreateCutterBox(model, new Vector3(1f, 0.5f, -0.5f), new Vector3(2f, 2f, 0f));
            var door = BoxModelFactory.CreateCutterBox(model, new Vector3(4f, 0f, -0.5f), new Vector3(5f, 2f, 0.5f));
            RebuildAll(model, wall);

            Assert.AreEqual(LiveBuilderCutMethod.MeshSubtraction, LiveBuilderCutRules.MethodOf(niche, wall), "мельче толщины — ниша вычитанием");
            Assert.AreEqual(LiveBuilderCutMethod.WallHoles, LiveBuilderCutRules.MethodOf(door, wall), "насквозь — проём по линии");

            var holes = SplineWallRebuilder.CollectHoles(wall, SplineWallRebuilder.PathsOf(wall))[0];
            Assert.AreEqual(1, holes.Count, "проём по линии — только у сквозной");
            Assert.AreEqual(4f, holes[0].AlongFrom, 0.001f);
            Assert.AreEqual(5f, holes[0].AlongTo, 0.001f);

            var subtracted = GeometryCutterLookup.CuttersFor(wall, new Bounds(Origin, Vector3.one * 100f));
            CollectionAssert.AreEqual(new[] { niche }, subtracted, "вычитается только ниша");

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4.5f, 1f, -2f), Vector3.forward, 4f), "сквозь дверь видно");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1.5f, 1f, -2f), Vector3.forward, 4f), "дно ниши закрывает");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1.5f, 1f, -2f), Vector3.forward, 1.95f), "ниша вглубь стены, до середины");
        }


        // ── Перекрытия ───────────────────────────────────────────────────

        // Здание 8 × 8 в два этажа в объекте: вырез-коробка — дыра в плане; повёрнутая — вычитание, стенки дыры — как торцы здания.
        [Test]
        public void CutterBoxInBuilding_IsSlabHole_RotatedSubtractsWithBuildingEdgeMaterial()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var firstFloor = building.FirstFloorWorldHeight - Origin.y;
            var cutter = BoxModelFactory.CreateCutterBox(model, new Vector3(3f, firstFloor + 2f, 3f), new Vector3(5f, firstFloor + 6f, 5f));
            BoxModelRebuilder.ForgetCachedResults();
            SplineBuildingRebuilder.Rebuild(building);
            var slab = SplineBuildingParts.Find(building, SplineBuildingPartKind.Slab, 1);
            var holeRay = Origin + new Vector3(4f, firstFloor + 5.5f, 4f);

            Assert.AreEqual(LiveBuilderCutMethod.SlabHoles, LiveBuilderCutRules.MethodOf(cutter, slab));
            Assert.AreEqual(1, SplineBuildingSlabHoles.Collect(building).Count, "дыра в плане");
            CollectionAssert.DoesNotContain(GeometryCutterLookup.CuttersFor(slab, new Bounds(Origin, Vector3.one * 100f)), cutter, "не вычитается");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(holeRay, Vector3.down, 3f), "перекрытие прорезано");

            cutter.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            BoxModelRebuilder.ForgetCachedResults();
            SplineBuildingRebuilder.Rebuild(building);

            Assert.AreEqual(LiveBuilderCutMethod.MeshSubtraction, LiveBuilderCutRules.MethodOf(cutter, slab));
            Assert.AreEqual(0, SplineBuildingSlabHoles.Collect(building).Count, "дыры в плане нет");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(holeRay, Vector3.down, 3f), "перекрытие прорезано вычитанием");
            Assert.IsTrue(AreHoleEdgesOuterWall(slab, Origin + new Vector3(4f, 0f, 4f)), "стенки дыры — материал торцов здания");
        }

        // Вертикальные треугольники перекрытия у дыры (в плане ближе 1,6 м к её середине) — все в части наружных стен.
        private static bool AreHoleEdgesOuterWall(SplineBuildingPart slab, Vector3 worldHoleCenter)
        {
            var mesh = slab.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var edgeCount = 0;

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index < triangles.Length; index += 3)
                {
                    var a = slab.transform.TransformPoint(vertices[triangles[index]]);
                    var b = slab.transform.TransformPoint(vertices[triangles[index + 1]]);
                    var c = slab.transform.TransformPoint(vertices[triangles[index + 2]]);
                    var center = (a + b + c) / 3f;
                    var isVertical = Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < 0.01f;

                    if (!isVertical || Mathf.Abs(center.x - worldHoleCenter.x) > 1.6f || Mathf.Abs(center.z - worldHoleCenter.z) > 1.6f)
                        continue;

                    if (submesh != SplineBuildingMeshBuilder.OuterWallSubmesh)
                        return false;

                    edgeCount++;
                }
            }

            return edgeCount > 0;
        }


        // ── Материалы сторон ─────────────────────────────────────────────

        [Test]
        public void SideMaterials_LieOnHoleFaces()
        {
            var model = CreateBlock();
            var cutter = BoxModelFactory.CreateCutterBox(model, HoleMin, HoleMax);
            cutter.SideMaterials.Bottom = floorMaterial;
            cutter.SideMaterials.Sides = wallMaterial;
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(floorMaterial, MaterialUnder(model, new Ray(Origin + new Vector3(1f, 5f, 1f), Vector3.down)), "пол дыры — низ выреза");
            Assert.AreEqual(wallMaterial, MaterialUnder(model, new Ray(Origin + new Vector3(1f, 2f, 1f), Vector3.right)), "стена дыры — стороны выреза");

            // Тот же вырез, повёрнутый на 30°, — вычитанием, материалы сторон те же.
            cutter.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(floorMaterial, MaterialUnder(model, new Ray(Origin + new Vector3(1f, 5f, 1f), Vector3.down)), "вычитание — пол дыры");
            Assert.AreEqual(wallMaterial, MaterialUnder(model, new Ray(Origin + new Vector3(1f, 2f, 1f), Vector3.right)), "вычитание — стена дыры");
        }


        [Test]
        public void PaintingHoleFloor_GoesToCutterBottom()
        {
            var model = CreateBlock();
            var cutter = BoxModelFactory.CreateCutterBox(model, HoleMin, HoleMax);
            BoxModelRebuilder.Rebuild(model);
            Physics.SyncTransforms();

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(new Ray(Origin + new Vector3(1f, 5f, 1f), Vector3.down), false, out var slot));
            slot.TryAssign(floorMaterial);

            Assert.AreEqual(floorMaterial, cutter.SideMaterials.Bottom, "пол дыры — низ выреза");
        }


        // ── Add ⇄ Cut ────────────────────────────────────────────────────

        [Test]
        public void AddCutToggle_TurnsBoxIntoCutterAndBack_OneUndo()
        {
            var model = CreateBlock();
            var part = BoxModelFactory.CreatePart(model, HoleMin, HoleMax, BoxModelPartMode.Add);
            part.SideMaterial = wallMaterial;
            var boxObject = part.gameObject;
            BoxModelRebuilder.Rebuild(model);
            Selection.activeGameObject = boxObject;
            Undo.IncrementCurrentGroup();

            BoxModelActions.ToggleAddCutOfSelected();
            var cutter = boxObject.GetComponent<GeometryCutter>();

            Assert.IsNull(boxObject.GetComponent<BoxModelPart>(), "коробки больше нет");
            Assert.IsNotNull(cutter, "на её месте — вырез");
            Assert.AreEqual(wallMaterial, cutter.SideMaterials.Sides, "материал боков — стенам дыры");
            Assert.IsTrue(HasSolverBoxOf(model, cutter), "режет свой объект клетками");
            Assert.IsTrue(RebuildAndCheckOpen(model), "дыра есть");

            Undo.PerformUndo();
            var restored = boxObject.GetComponent<BoxModelPart>();

            Assert.IsNull(boxObject.GetComponent<GeometryCutter>(), "Ctrl+Z — выреза нет");
            Assert.IsNotNull(restored, "Ctrl+Z — коробка вернулась одним шагом");
            Assert.AreEqual(BoxModelPartMode.Add, restored.Mode);
            Assert.AreEqual(HoleMax - HoleMin, restored.Size);

            BoxModelActions.ToggleAddCutOfSelected();
            BoxModelActions.ToggleAddCutOfSelected();
            var backToPart = boxObject.GetComponent<BoxModelPart>();

            Assert.IsNotNull(backToPart, "вырез-коробка — снова коробка");
            Assert.AreEqual(BoxModelPartMode.Add, backToPart.Mode);
            Assert.AreEqual(HoleMax - HoleMin, backToPart.Size);
            Assert.AreEqual(HoleMin, BoxModelShapeCollector.LocalMinOf(model, boxObject.transform), "на том же месте");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Брусок 2 × 4 × 2 м от Origin, объект на новом правиле вырезов, уже собран. Дыра выходит наверх.
        private static BoxModel CreateBlock()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 4f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            return model;
        }

        private static void RebuildAll(BoxModel model, SplineWall wall)
        {
            BoxModelRebuilder.ForgetCachedResults();
            BoxModelRebuilder.Rebuild(model);
            SplineWallRebuilder.Rebuild(wall);
            Physics.SyncTransforms();
        }

        private static bool RebuildAndCheckOpen(BoxModel model)
        {
            BoxModelRebuilder.Rebuild(model);
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(1f, 5f, 1f), Vector3.down, 3.5f);
        }

        // Клетки эталонного солвера по нынешним коробкам объекта и его правилу — строкой: углы и номера граней каждой.
        private static string CellsOf(BoxModel model, out BoxModelSolverBox holeBox)
        {
            BoxModelRebuilder.ForgetCachedResults();
            var boxes = BoxModelRebuilder.GetSolverBoxes(model);
            holeBox = boxes[1];
            var shapes = new List<BoxModelPartShape>();

            foreach (var box in boxes)
                shapes.Add(box.Shape);

            var cells = BoxModelSolver.Solve(shapes, LiveBuilderCutRules.RuleOf(model), LiveBuilderCutRules.SolverFilterOf(model, boxes));
            var text = new System.Text.StringBuilder();

            foreach (var cell in cells)
                text.Append(cell.Box.Min).Append(cell.Box.Max).Append(string.Join(",", cell.FaceSources)).Append(';');

            return text.ToString();
        }

        private static Mesh MeshOf(BoxModel model)
        {
            BoxModelRebuilder.Rebuild(model);
            return model.GetComponent<MeshFilter>().sharedMesh;
        }

        private static bool HasSolverBoxOf(BoxModel model, GeometryCutter cutter)
        {
            BoxModelRebuilder.ForgetCachedResults();

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Owner == cutter)
                    return true;

            return false;
        }

        // Материал части меша, на чьём треугольнике луч упёрся в объект: треугольник ищется в меше рендерера по точке попадания.
        private static Material MaterialUnder(BoxModel model, Ray ray)
        {
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(ray, out var hit, 20f), "луч не попал в объект");

            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var localPoint = model.transform.InverseTransformPoint(hit.point);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index < triangles.Length; index += 3)
                    if (IsOnTriangle(localPoint, vertices[triangles[index]], vertices[triangles[index + 1]], vertices[triangles[index + 2]]))
                        return model.GetComponent<MeshRenderer>().sharedMaterials[submesh];
            }

            return null;
        }

        private static bool IsOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            const float tolerance = 0.001f;
            var normal = Vector3.Cross(b - a, c - a);

            if (normal.sqrMagnitude < 1e-12f || Mathf.Abs(Vector3.Dot(point - a, normal.normalized)) > tolerance)
                return false;

            return Vector3.Dot(Vector3.Cross(b - a, point - a), normal) >= -tolerance
                   && Vector3.Dot(Vector3.Cross(c - b, point - b), normal) >= -tolerance
                   && Vector3.Dot(Vector3.Cross(a - c, point - c), normal) >= -tolerance;
        }
    }
}
