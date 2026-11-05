using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Перекрытия здания: две вырезающие коробки, что заходят одна на другую, дают одну общую дыру — торцы плиты только по её краю,
    /// внутри соседней дыры торцов нет. Контур, который пересекает сам себя, разрезается на треугольники не целиком — и это видно.
    /// </summary>
    public class SplineBuildingSlabTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

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


        // ── Дыры в перекрытии ────────────────────────────────────────────

        // Квадраты 2 × 2 со сдвигом на 1 по обеим осям: край общей дыры — 12 м, как у её габарита 3 × 3.
        [Test]
        public void OverlappingCutBoxes_LeaveNoEdgeFacesInsideEachOther()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuildingIn(model);
            var firstFloor = building.FirstFloorWorldHeight - Origin.y;
            BoxModelFactory.CreatePart(model, new Vector3(2f, firstFloor + 2f, 2f), new Vector3(4f, firstFloor + 6f, 4f), BoxModelPartMode.Cut);
            BoxModelFactory.CreatePart(model, new Vector3(3f, firstFloor + 2f, 3f), new Vector3(5f, firstFloor + 6f, 5f), BoxModelPartMode.Cut);
            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.Rebuild(building);

            var slab = SplineBuildingParts.Find(building, SplineBuildingPartKind.Floor, 1);
            var edgeArea = 0f;

            foreach (var (center, area) in VerticalTrianglesOf(slab))
            {
                var plan = new Vector2(center.x - Origin.x, center.z - Origin.z);
                Assert.IsFalse(IsStrictlyInside(plan, 2f, 4f) || IsStrictlyInside(plan, 3f, 5f), "торец внутри дыры: " + plan);
                edgeArea += area;
            }

            Assert.AreEqual(12f * building.SlabThickness, edgeArea, 0.01f, "торцы — только по краю общей дыры");
        }

        // Здание 8 × 8 в два этажа по 4 м внутри объекта: его вырезающие коробки режут перекрытия.
        private static SplineBuilding CreateBuildingIn(BoxModel model)
        {
            var building = BoxModelFactory.CreateBuilding(model, Origin, Quaternion.identity);
            building.StoreyCount = 2;
            building.StoreyHeight = 4f;
            building.WallThickness = 0.3f;
            building.WallType = null;
            building.ContourSide = SplineBuildingContourSide.Outside;

            var spline = building.GetComponent<SplineContainer>().Spline;
            spline.Clear();

            foreach (var point in new[] { Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f) })
                spline.Add(new BezierKnot((float3)point), TangentMode.Linear);

            spline.Closed = true;
            SplineBuildingRebuilder.Rebuild(building);
            return building;
        }

        // Стоячие треугольники меша части (торцы плиты): середина в мире и площадь.
        private static List<(Vector3 Center, float Area)> VerticalTrianglesOf(SplineBuildingPart part)
        {
            var triangles = new List<(Vector3 Center, float Area)>();
            var mesh = part.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var indices = mesh.triangles;

            for (var index = 0; index < indices.Length; index += 3)
            {
                var a = part.transform.TransformPoint(vertices[indices[index]]);
                var b = part.transform.TransformPoint(vertices[indices[index + 1]]);
                var c = part.transform.TransformPoint(vertices[indices[index + 2]]);
                var cross = Vector3.Cross(b - a, c - a);

                if (cross.sqrMagnitude > 1e-10f && Mathf.Abs(cross.normalized.y) < 0.1f)
                    triangles.Add(((a + b + c) / 3f, cross.magnitude / 2f));
            }

            return triangles;
        }

        // Точка внутри квадрата от min до max по обеим осям, не на его краю.
        private static bool IsStrictlyInside(Vector2 point, float min, float max)
        {
            const float margin = 0.01f;
            return point.x > min + margin && point.x < max - margin && point.y > min + margin && point.y < max - margin;
        }


        // ── Треугольники контура ─────────────────────────────────────────

        [Test]
        public void SelfIntersectingContour_IsNotTriangulatedCompletely()
        {
            PlanPolygons.Triangulate(new List<Vector2> { new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f) }, out var isSquareComplete);
            Assert.IsTrue(isSquareComplete, "квадрат — целиком");

            // Сторона 2 → 3 пересекает сторону 4 → 5: остаётся «бабочка», у которой нет ни одного «уха».
            PlanPolygons.Triangulate(new List<Vector2> { new(8f, 1f), new(8f, 2f), new(0f, 5f), new(0f, 4f), new(6f, 5f) }, out var isCrossedComplete);
            Assert.IsFalse(isCrossedComplete, "пересекает сам себя — не целиком");
        }
    }
}
