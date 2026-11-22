using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>Отпечаток «по виду» (<see cref="LiveBuilderLookDigest"/>): не видит, на какие объекты разбита геометрия, и видит сдвиг грани.</summary>
    public class LiveBuilderLookDigestTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;


        [Test]
        public void SameBoxes_InOneOrTwoObjects_LookTheSame()
        {
            List<string> oneObject;
            List<string> twoObjects;

            using (new LiveBuilderTestScene())
            {
                var model = BoxModelFactory.CreateModel(Origin);
                BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
                BoxModelFactory.CreatePart(model, new Vector3(4f, 0f, 0f), new Vector3(5f, 3f, 1f), BoxModelPartMode.Add);
                BoxModelRebuilder.Rebuild(model);
                oneObject = LiveBuilderLookDigest.DescribeScene(model.gameObject.scene);
            }

            using (new LiveBuilderTestScene())
            {
                var first = BoxModelFactory.CreateModel(Origin);
                BoxModelFactory.CreatePart(first, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
                var second = BoxModelFactory.CreateModel(Origin + new Vector3(4f, 0f, 0f));
                BoxModelFactory.CreatePart(second, Vector3.zero, new Vector3(1f, 3f, 1f), BoxModelPartMode.Add);
                BoxModelRebuilder.Rebuild(first);
                BoxModelRebuilder.Rebuild(second);
                twoObjects = LiveBuilderLookDigest.DescribeScene(first.gameObject.scene);
            }

            var whole = LiveBuilderLookDigest.WholeSection;
            Assert.IsNotEmpty(LiveBuilderLookDigest.SectionOf(oneObject, whole), "грани есть");
            var differences = LiveBuilderLookDigest.Compare(LiveBuilderLookDigest.SectionOf(oneObject, whole), LiveBuilderLookDigest.SectionOf(twoObjects, whole));
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }


        [Test]
        public void BoxMovedByCentimetre_LooksDifferent()
        {
            using (new LiveBuilderTestScene())
            {
                var model = BoxModelFactory.CreateModel(Origin);
                var box = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
                BoxModelRebuilder.Rebuild(model);
                var before = LiveBuilderLookDigest.DescribeScene(model.gameObject.scene);

                box.transform.localPosition += new Vector3(0.01f, 0f, 0f);
                BoxModelRebuilder.ForgetCachedResults();
                BoxModelRebuilder.Rebuild(model);
                var after = LiveBuilderLookDigest.DescribeScene(model.gameObject.scene);

                Assert.IsNotEmpty(LiveBuilderLookDigest.Compare(before, after), "сдвиг на 1 см виден");
            }
        }
    }
}
