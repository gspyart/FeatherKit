using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Копит позы гуманоида кадр за кадром и превращает их в кривые Humanoid-клипа:
    /// положение и поворот тела (RootT, RootQ) и все мышцы аватара.
    ///
    /// Имена мышц в клипе не совпадают с <see cref="HumanTrait.MuscleName"/> у пальцев:
    /// «Left Thumb 1 Stretched» в клипе называется «LeftHand.Thumb.1 Stretched».
    /// Перевод живёт здесь же.
    /// </summary>
    internal sealed class HumanPoseCurves
    {
        private static readonly string[] BodyPositionNames = { "RootT.x", "RootT.y", "RootT.z" };
        private static readonly string[] BodyRotationNames = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        private readonly List<Keyframe>[] bodyPositionKeys = CreateKeyLists(3);
        private readonly List<Keyframe>[] bodyRotationKeys = CreateKeyLists(4);
        private readonly List<Keyframe>[] muscleKeys = CreateKeyLists(HumanTrait.MuscleCount);

        private Quaternion previousBodyRotation = Quaternion.identity;
        private bool hasPreviousBodyRotation;


        // ── Накопление ──────────────────────────────

        public void AddPose(float time, HumanPose pose)
        {
            AddVector(bodyPositionKeys, time, pose.bodyPosition);
            AddRotation(time, pose.bodyRotation);

            for (var i = 0; i < muscleKeys.Length && i < pose.muscles.Length; i++)
                muscleKeys[i].Add(new Keyframe(time, pose.muscles[i]));
        }

        private static void AddVector(List<Keyframe>[] keys, float time, Vector3 value)
        {
            keys[0].Add(new Keyframe(time, value.x));
            keys[1].Add(new Keyframe(time, value.y));
            keys[2].Add(new Keyframe(time, value.z));
        }


        // q и -q — один и тот же поворот. Если знак скачет между кадрами, кривые
        // прыгают через ноль и тело кувыркается. Держим знак ближе к прошлому кадру.
        private void AddRotation(float time, Quaternion rotation)
        {
            if (hasPreviousBodyRotation && Quaternion.Dot(previousBodyRotation, rotation) < 0f)
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);

            previousBodyRotation = rotation;
            hasPreviousBodyRotation = true;

            bodyRotationKeys[0].Add(new Keyframe(time, rotation.x));
            bodyRotationKeys[1].Add(new Keyframe(time, rotation.y));
            bodyRotationKeys[2].Add(new Keyframe(time, rotation.z));
            bodyRotationKeys[3].Add(new Keyframe(time, rotation.w));
        }


        // ── Запись в клип ──────────────────────────────

        /// <summary>Записать все кривые в клип одним вызовом. Старые кривые клипа не трогает.</summary>
        public void WriteTo(AnimationClip clip)
        {
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();

            AddCurves(bindings, curves, BodyPositionNames, bodyPositionKeys);
            AddCurves(bindings, curves, BodyRotationNames, bodyRotationKeys);

            for (var i = 0; i < muscleKeys.Length; i++)
                AddCurve(bindings, curves, ClipPropertyName(HumanTrait.MuscleName[i]), muscleKeys[i]);

            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        }

        private static void AddCurves(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string[] names, List<Keyframe>[] keys)
        {
            for (var i = 0; i < names.Length; i++)
                AddCurve(bindings, curves, names[i], keys[i]);
        }

        private static void AddCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string propertyName, List<Keyframe> keys)
        {
            bindings.Add(EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), propertyName));
            curves.Add(new AnimationCurve(WithLinearTangents(keys)));
        }


        // Ключ на каждом кадре, поэтому прямые между ними точнее любых сглаживаний:
        // кривая не вылезает за то, что реально было в позе.
        private static Keyframe[] WithLinearTangents(List<Keyframe> keys)
        {
            var result = keys.ToArray();

            for (var i = 0; i < result.Length; i++)
            {
                var incoming = i > 0 ? Slope(result[i - 1], result[i]) : 0f;
                var outgoing = i < result.Length - 1 ? Slope(result[i], result[i + 1]) : 0f;

                result[i].inTangent = i > 0 ? incoming : outgoing;
                result[i].outTangent = i < result.Length - 1 ? outgoing : incoming;
            }

            return result;
        }

        private static float Slope(Keyframe from, Keyframe to)
        {
            var duration = to.time - from.time;
            return duration > 0f ? (to.value - from.value) / duration : 0f;
        }


        // «Left Thumb 1 Stretched» → «LeftHand.Thumb.1 Stretched», «Left Thumb Spread» → «LeftHand.Thumb.Spread».
        // У остальных мышц имя в клипе совпадает с именем в аватаре.
        private static string ClipPropertyName(string muscleName)
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                foreach (var finger in FingerNames)
                {
                    var prefix = side + " " + finger + " ";

                    if (muscleName.StartsWith(prefix))
                        return side + "Hand." + finger + "." + muscleName.Substring(prefix.Length);
                }
            }

            return muscleName;
        }


        private static List<Keyframe>[] CreateKeyLists(int count)
        {
            var lists = new List<Keyframe>[count];

            for (var i = 0; i < count; i++)
                lists[i] = new List<Keyframe>();

            return lists;
        }
    }
}
