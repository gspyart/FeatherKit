using FeatherKit.EditorTools.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Направляющие от последней точки линии (<see cref="LiveBuilderLineGuides"/>): по оси, под 45°, продолжение прошлого отрезка, шаг 15° с Ctrl,
    /// длина вдоль луча — по сетке; дальше порога на экране — не прилипает. Экран — вид сверху, 20 пикселей на метр.
    /// </summary>
    public class LiveBuilderLineGuideTests
    {
        private const float PixelsPerMeter = 20f;

        private static readonly Matrix4x4 FloorFrame = Matrix4x4.identity;


        [Test]
        public void NearAxisX_SnapsOntoIt_LengthByGrid()
        {
            var isSnapped = Snap(Vector3.zero, null, new Vector3(5f, 0f, 0.2f), false, out var snapped, out var guide);

            Assert.IsTrue(isSnapped, "0,2 м от оси — 4 пикселя, прилипает");
            Assert.AreEqual("по оси X", guide.Label);
            Assert.That(Vector3.Distance(snapped, new Vector3(LiveBuilderGrid.Snap(5f, 0), 0f, 0f)), Is.LessThan(0.001f), "на оси, длина по сетке");
        }


        [Test]
        public void NearDiagonal_SnapsAt45Degrees()
        {
            Assert.IsTrue(Snap(Vector3.zero, null, new Vector3(3f, 0f, 3.1f), false, out var snapped, out var guide), "у диагонали — прилипает");
            Assert.AreEqual("45°", guide.Label);
            Assert.AreEqual(snapped.x, snapped.z, 0.001f, "точка ровно на 45°");
        }


        [Test]
        public void AlongPreviousSegment_SnapsAsItsContinuation()
        {
            var isSnapped = Snap(Vector3.zero, new Vector3(-4f, 0f, -3f), new Vector3(4f, 0f, 3.05f), false, out _, out var guide);

            Assert.IsTrue(isSnapped, "почти на продолжении отрезка — прилипает");
            Assert.AreEqual("продолжение отрезка", guide.Label);
            Assert.That(Vector3.Angle(guide.Direction, new Vector3(4f, 0f, 3f)), Is.LessThan(0.01f), "направление — как у отрезка");
        }


        [Test]
        public void WithCtrl_SnapsToFifteenDegreeSteps()
        {
            var direction = Quaternion.Euler(0f, -31f, 0f) * Vector3.right;
            Assert.IsTrue(Snap(Vector3.zero, null, direction * 6f, true, out _, out var guide), "с Ctrl — к шагу угла");
            Assert.AreEqual("30°", guide.Label);
        }


        [Test]
        public void FarFromEveryGuide_DoesNotSnap()
        {
            var direction = Quaternion.Euler(0f, -22f, 0f) * Vector3.right;
            Assert.IsFalse(Snap(Vector3.zero, null, direction * 6f, false, out _, out _), "22° — далеко и от оси, и от 45°");
        }


        // Вид сверху: X вправо, Z вверх по экрану.
        private static bool Snap(Vector3 last, Vector3? previous, Vector3 point, bool isAngleStep, out Vector3 snapped, out LiveBuilderLineGuide guide)
        {
            return LiveBuilderLineGuides.TrySnap(FloorFrame, 1, last, previous, point, isAngleStep, world => new Vector2(world.x, world.z) * PixelsPerMeter,
                out snapped, out guide);
        }
    }
}
