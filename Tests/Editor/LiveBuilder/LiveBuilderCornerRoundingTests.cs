using FeatherKit.EditorTools.LiveBuilder;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Скругление углов контура: дуга лежит на окружности заданного радиуса, один кусок — срез, слишком большой радиус
    /// ужимается до половины отрезков, у конца открытой линии угла нет, радиус 0 убирает запись.
    /// </summary>
    public class LiveBuilderCornerRoundingTests
    {
        private const float Tolerance = 0.001f;


        // ── Дуга ─────────────────────────────────────────────────────────

        [Test]
        public void RoundedCorner_LiesOnCircle()
        {
            var spline = CreateCorner();
            LiveBuilderCornerRadii.SetRadius(spline, 1, 1f);

            var path = SplineWallPath.FromSpline(spline, 0.5f, 6);
            var center = new Vector3(3f, 0f, 1f);

            Assert.AreEqual(9, path.Points.Count, "начало, семь точек дуги, конец");

            for (var index = 1; index < 8; index++)
                Assert.AreEqual(1f, Vector3.Distance(path.Points[index], center), Tolerance, "точка дуги " + index);
        }


        [Test]
        public void OneSegment_IsChamfer()
        {
            var spline = CreateCorner();
            LiveBuilderCornerRadii.SetRadius(spline, 1, 1f);

            var path = SplineWallPath.FromSpline(spline, 0.5f, 1);

            Assert.AreEqual(4, path.Points.Count);
            AssertNear(new Vector3(3f, 0f, 0f), path.Points[1]);
            AssertNear(new Vector3(4f, 0f, 1f), path.Points[2]);
        }


        [Test]
        public void TooBigRadius_ShrinksToHalfOfSegments()
        {
            var spline = CreateCorner();
            LiveBuilderCornerRadii.SetRadius(spline, 1, 10f);

            var path = SplineWallPath.FromSpline(spline, 0.5f, 6);

            AssertNear(new Vector3(2f, 0f, 0f), path.Points[1]);
            AssertNear(new Vector3(4f, 0f, 2f), path.Points[^2]);
        }


        [Test]
        public void OpenLineEnd_HasNoCorner()
        {
            var spline = CreateCorner();
            LiveBuilderCornerRadii.SetRadius(spline, 0, 1f);

            Assert.AreEqual(3, SplineWallPath.FromSpline(spline, 0.5f, 6).Points.Count);
        }


        // ── Запись радиуса ───────────────────────────────────────────────

        [Test]
        public void ZeroRadius_RemovesRecord()
        {
            var spline = CreateCorner();
            LiveBuilderCornerRadii.SetRadius(spline, 1, 0.5f);
            LiveBuilderCornerRadii.SetRadius(spline, 1, 0f);

            Assert.AreEqual(0f, LiveBuilderCornerRadii.RadiusOf(spline, 1));
            Assert.IsTrue(!spline.TryGetFloatData(LiveBuilderCornerRadii.Key, out var data) || data.Count == 0);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Открытая линия с прямым углом: (0, 0, 0) → (4, 0, 0) → (4, 0, 4).
        private static Spline CreateCorner()
        {
            var spline = new Spline();
            spline.Add(new BezierKnot(new float3(0f, 0f, 0f)), TangentMode.Linear);
            spline.Add(new BezierKnot(new float3(4f, 0f, 0f)), TangentMode.Linear);
            spline.Add(new BezierKnot(new float3(4f, 0f, 4f)), TangentMode.Linear);
            return spline;
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual), Tolerance, "ждали " + expected + ", вышло " + actual);
        }
    }
}
