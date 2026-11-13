using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Какая переменная подходит полю и как объект переменной переводится в тип поля.</summary>
    public class ScenarioVariableKindsTests
    {
        // ── Вид по типу поля ─────────────────────────────────────────────

        [TestCase(typeof(bool), ScenarioVariableKind.Flag)]
        [TestCase(typeof(int), ScenarioVariableKind.Counter)]
        [TestCase(typeof(float), ScenarioVariableKind.Number)]
        [TestCase(typeof(Light), ScenarioVariableKind.Object)]
        [TestCase(typeof(GameObject), ScenarioVariableKind.Object)]
        [TestCase(typeof(AudioClip), ScenarioVariableKind.Object)]
        public void TryGetKind_KnownTypes(System.Type valueType, ScenarioVariableKind expectedKind)
        {
            Assert.That(ScenarioVariableKinds.TryGetKind(valueType, out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(expectedKind));
        }

        [TestCase(typeof(string))]
        [TestCase(typeof(Vector3))]
        [TestCase(typeof(double))]
        public void TryGetKind_OtherTypes_HaveNoVariables(System.Type valueType)
        {
            Assert.That(ScenarioVariableKinds.TryGetKind(valueType, out _), Is.False);
        }


        // ── Подходит ли переменная ───────────────────────────────────────

        [Test]
        public void Fits_ByKindAndObjectType()
        {
            var flag = new ScenarioVariableDefinition("Flag", ScenarioVariableKind.Flag);
            var lightVariable = new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object) { ObjectType = typeof(Light) };
            var gameObjectVariable = new ScenarioVariableDefinition("Thing", ScenarioVariableKind.Object) { ObjectType = typeof(GameObject) };
            var clipVariable = new ScenarioVariableDefinition("Clip", ScenarioVariableKind.Object) { ObjectType = typeof(AudioClip) };

            Assert.That(ScenarioVariableKinds.Fits(flag, typeof(bool)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(flag, typeof(int)), Is.False);
            Assert.That(ScenarioVariableKinds.Fits(lightVariable, typeof(Light)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(lightVariable, typeof(GameObject)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(gameObjectVariable, typeof(Light)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(clipVariable, typeof(Light)), Is.False);
            Assert.That(ScenarioVariableKinds.Fits(lightVariable, typeof(float)), Is.False);
        }


        // ── Перевод объекта ──────────────────────────────────────────────

        [Test]
        public void ConvertObject_BetweenGameObjectAndComponent()
        {
            var lamp = EditorUtility.CreateGameObjectWithHideFlags("Lamp", HideFlags.HideAndDontSave);

            try
            {
                var light = lamp.AddComponent<Light>();

                Assert.That(ScenarioVariableKinds.ConvertObject(lamp, typeof(Light)), Is.SameAs(light));
                Assert.That(ScenarioVariableKinds.ConvertObject(light, typeof(GameObject)), Is.SameAs(lamp));
                Assert.That(ScenarioVariableKinds.ConvertObject(light, typeof(Light)), Is.SameAs(light));
                Assert.That(ScenarioVariableKinds.ConvertObject(light, typeof(Transform)), Is.SameAs(lamp.transform));
                Assert.That(ScenarioVariableKinds.ConvertObject(lamp, typeof(AudioSource)), Is.Null);
                Assert.That(ScenarioVariableKinds.ConvertObject(null, typeof(Light)), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(lamp);
            }
        }


        // ── Тип переменной Object ────────────────────────────────────────

        [Test]
        public void ObjectType_IsStoredWithoutVersion()
        {
            var variable = new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object) { ObjectType = typeof(Light) };

            Assert.That(ScenarioVariableDefinition.StoredTypeNameOf(typeof(Light)), Is.EqualTo("UnityEngine.Light, UnityEngine.CoreModule"));
            Assert.That(variable.ObjectType, Is.EqualTo(typeof(Light)));
        }
    }
}
