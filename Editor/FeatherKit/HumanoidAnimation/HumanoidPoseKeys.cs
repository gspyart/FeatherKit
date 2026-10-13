using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Ключи позы в клипе рига. Поза — все ручки тела разом, поэтому ключ позы — это ключи
    /// всех ручек в одном кадре, как в обычной анимации «поза к позе». Здесь: кадры с ключами,
    /// поставить ключ позы, убрать его, перенести на другой кадр.
    ///
    /// Клип правится через AnimationUtility с Undo — окно Animation видит правки сразу.
    /// Новый ключ берёт сглаживание соседнего слева, а без соседа — Clamped Auto, как в окне Animation.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidPoseKeys
    {
        private const string PositionProperty = "m_LocalPosition";
        private const string EulerProperty = "localEulerAnglesRaw";
        private const string QuaternionProperty = "m_LocalRotation";
        private const string ConstraintWeightProperty = "m_Weight";

        private static readonly string[] VectorAxes = { "x", "y", "z" };
        private static readonly string[] QuaternionAxes = { "x", "y", "z", "w" };

        private static readonly List<float> CachedKeyTimes = new List<float>();
        private static AnimationClip cachedClip;

        static HumanoidPoseKeys()
        {
            AnimationUtility.onCurveWasModified += (clip, binding, type) =>
            {
                if (clip == cachedClip)
                    cachedClip = null;
            };

            Undo.undoRedoPerformed += () => cachedClip = null;
        }


        // ── Кадры с ключами ──────────────────────────────

        /// <summary>Время ключей позы по возрастанию: кадры, где хоть у одной ручки рига есть ключ.</summary>
        public static IReadOnlyList<float> CollectKeyTimes(AnimationClip clip)
        {
            if (clip == cachedClip)
                return CachedKeyTimes;

            CachedKeyTimes.Clear();
            cachedClip = clip;

            if (clip == null)
                return CachedKeyTimes;

            var times = new List<float>();

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!IsRigBinding(binding))
                    continue;

                foreach (var key in AnimationUtility.GetEditorCurve(clip, binding).keys)
                    times.Add(key.time);
            }

            times.Sort();

            var tolerance = KeyTolerance(clip);

            foreach (var time in times)
            {
                if (CachedKeyTimes.Count == 0 || time - CachedKeyTimes[CachedKeyTimes.Count - 1] > tolerance)
                    CachedKeyTimes.Add(time);
            }

            return CachedKeyTimes;
        }


        public static bool HasKeyAt(AnimationClip clip, float time)
        {
            var tolerance = KeyTolerance(clip);

            foreach (var keyTime in CollectKeyTimes(clip))
            {
                if (Mathf.Abs(keyTime - time) <= tolerance)
                    return true;
            }

            return false;
        }


        /// <summary>Ближайший ключ позы раньше этого момента. Нет — null.</summary>
        public static float? FindPreviousKeyTime(AnimationClip clip, float time)
        {
            var keyTimes = CollectKeyTimes(clip);
            var tolerance = KeyTolerance(clip);

            for (var i = keyTimes.Count - 1; i >= 0; i--)
            {
                if (keyTimes[i] < time - tolerance)
                    return keyTimes[i];
            }

            return null;
        }


        /// <summary>Ближайший ключ позы позже этого момента. Нет — null.</summary>
        public static float? FindNextKeyTime(AnimationClip clip, float time)
        {
            var tolerance = KeyTolerance(clip);

            foreach (var keyTime in CollectKeyTimes(clip))
            {
                if (keyTime > time + tolerance)
                    return keyTime;
            }

            return null;
        }


        // ── Поставить ключ позы ──────────────────────────────

        /// <summary>
        /// Поставить ключ позы: всем работающим ручкам — то положение, в котором они сейчас.
        /// Где ключ уже есть, он получает текущее значение. Вес IK — только если он уже анимирован.
        /// </summary>
        public static void KeyPose(HumanoidAnimationRig rig, AnimationClip clip, float time)
        {
            var frameTime = SnapToFrame(clip, time);
            var existing = new HashSet<EditorCurveBinding>(AnimationUtility.GetCurveBindings(clip));
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();

            foreach (var control in rig.Controls)
            {
                if (control.Handle != null && rig.IsInActiveMode(control))
                    AddControlKeys(rig, clip, control, frameTime, existing, bindings, curves);
            }

            foreach (var limb in rig.Limbs)
                AddIKWeightKey(rig, clip, limb, frameTime, existing, bindings, curves);

            if (bindings.Count == 0)
                return;

            Undo.RegisterCompleteObjectUndo(clip, "Ключ позы");
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        }


        private static void AddControlKeys(HumanoidAnimationRig rig, AnimationClip clip, HumanoidRigControl control, float time,
            HashSet<EditorCurveBinding> existing, List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
            var handle = control.Handle;
            var path = AnimationUtility.CalculateTransformPath(handle, rig.transform);

            if (control.Type != HumanoidRigControlType.BoneRotation)
                AddVectorKeys(clip, path, PositionProperty, handle.localPosition, time, bindings, curves);

            if (control.Type == HumanoidRigControlType.LimbBendHint)
                return;

            if (existing.Contains(TransformBinding(path, QuaternionProperty, "w")))
            {
                var rotation = handle.localRotation;
                var values = new[] { rotation.x, rotation.y, rotation.z, rotation.w };

                for (var i = 0; i < QuaternionAxes.Length; i++)
                    AddKey(clip, TransformBinding(path, QuaternionProperty, QuaternionAxes[i]), values[i], time, bindings, curves);

                return;
            }

            var reference = EvaluateVector(clip, path, EulerProperty, time) ?? TransformUtils.GetInspectorRotation(handle);
            AddVectorKeys(clip, path, EulerProperty, ContinuousEuler(handle.localRotation, reference), time, bindings, curves);
        }


        private static void AddIKWeightKey(HumanoidAnimationRig rig, AnimationClip clip, HumanoidRigLimb limb, float time,
            HashSet<EditorCurveBinding> existing, List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
            if (limb.IKConstraint == null)
                return;

            var path = AnimationUtility.CalculateTransformPath(limb.IKConstraint.transform, rig.transform);
            var binding = EditorCurveBinding.FloatCurve(path, typeof(TwoBoneIKConstraint), ConstraintWeightProperty);

            if (existing.Contains(binding))
                AddKey(clip, binding, limb.IKConstraint.weight, time, bindings, curves);
        }


        private static void AddVectorKeys(AnimationClip clip, string path, string property, Vector3 value, float time,
            List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
            for (var i = 0; i < VectorAxes.Length; i++)
                AddKey(clip, TransformBinding(path, property, VectorAxes[i]), value[i], time, bindings, curves);
        }


        private static void AddKey(AnimationClip clip, EditorCurveBinding binding, float value, float time,
            List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding) ?? new AnimationCurve();
            var index = FindKeyIndex(curve, time, KeyTolerance(clip));

            if (index >= 0)
            {
                var key = curve[index];
                key.value = value;
                curve.MoveKey(index, key);
                RefreshTangents(curve, index);
            }
            else
            {
                index = curve.AddKey(new Keyframe(time, value));

                if (index >= 0)
                    ApplyNewKeyTangents(curve, index);
            }

            bindings.Add(binding);
            curves.Add(curve);
        }


        // Повороты пишем эйлером, как окно Animation при записи. Из всех эйлеров одного поворота
        // берём ближайший к соседнему ключу: иначе кривая прыгнула бы на 360° и рука сделала бы оборот.
        private static Vector3 ContinuousEuler(Quaternion rotation, Vector3 reference)
        {
            var euler = rotation.eulerAngles;
            var flipped = new Vector3(180f - euler.x, euler.y + 180f, euler.z + 180f);

            var first = Unwrap(euler, reference);
            var second = Unwrap(flipped, reference);

            return (first - reference).sqrMagnitude <= (second - reference).sqrMagnitude ? first : second;
        }

        private static Vector3 Unwrap(Vector3 euler, Vector3 reference) => new Vector3(
            reference.x + Mathf.DeltaAngle(reference.x, euler.x),
            reference.y + Mathf.DeltaAngle(reference.y, euler.y),
            reference.z + Mathf.DeltaAngle(reference.z, euler.z));


        // Значение трёх кривых свойства в этот момент. Хоть одной кривой нет — null.
        private static Vector3? EvaluateVector(AnimationClip clip, string path, string property, float time)
        {
            var value = Vector3.zero;

            for (var i = 0; i < VectorAxes.Length; i++)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, TransformBinding(path, property, VectorAxes[i]));

                if (curve == null || curve.length == 0)
                    return null;

                value[i] = curve.Evaluate(time);
            }

            return value;
        }


        // ── Убрать и перенести ──────────────────────────────

        /// <summary>Убрать ключ позы: ключи всех ручек рига в этом кадре. Кривая без ключей удаляется.</summary>
        public static void DeletePoseKey(AnimationClip clip, float time)
        {
            var tolerance = KeyTolerance(clip);
            var changedBindings = new List<EditorCurveBinding>();
            var changedCurves = new List<AnimationCurve>();
            var emptiedBindings = new List<EditorCurveBinding>();

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!IsRigBinding(binding))
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var index = FindKeyIndex(curve, time, tolerance);

                if (index < 0)
                    continue;

                curve.RemoveKey(index);

                if (curve.length == 0)
                {
                    emptiedBindings.Add(binding);
                    continue;
                }

                RefreshAllTangents(curve);
                changedBindings.Add(binding);
                changedCurves.Add(curve);
            }

            if (changedBindings.Count == 0 && emptiedBindings.Count == 0)
                return;

            Undo.RegisterCompleteObjectUndo(clip, "Убрать ключ позы");

            if (changedBindings.Count > 0)
                AnimationUtility.SetEditorCurves(clip, changedBindings.ToArray(), changedCurves.ToArray());

            foreach (var binding in emptiedBindings)
                AnimationUtility.SetEditorCurve(clip, binding, null);
        }


        /// <summary>
        /// Перенести ключ позы на другой кадр: ключи всех ручек едут вместе. Если там уже был
        /// ключ позы, он заменяется переносимым.
        /// </summary>
        public static void MovePoseKey(AnimationClip clip, float fromTime, float toTime)
        {
            var tolerance = KeyTolerance(clip);
            var targetTime = Mathf.Max(0f, SnapToFrame(clip, toTime));

            if (Mathf.Abs(targetTime - fromTime) <= tolerance)
                return;

            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!IsRigBinding(binding))
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var index = FindKeyIndex(curve, fromTime, tolerance);

                if (index < 0)
                    continue;

                var key = curve[index];
                curve.RemoveKey(index);

                var replaced = FindKeyIndex(curve, targetTime, tolerance);

                if (replaced >= 0)
                    curve.RemoveKey(replaced);

                key.time = targetTime;
                curve.AddKey(key);
                RefreshAllTangents(curve);

                bindings.Add(binding);
                curves.Add(curve);
            }

            if (bindings.Count == 0)
                return;

            Undo.RegisterCompleteObjectUndo(clip, "Перенести ключ позы");
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
        }


        // ── Кадры ──────────────────────────────

        /// <summary>Время, подровненное к ближайшему кадру клипа.</summary>
        public static float SnapToFrame(AnimationClip clip, float time)
        {
            var frameRate = FrameRate(clip);
            return Mathf.Round(time * frameRate) / frameRate;
        }


        public static int ToFrame(AnimationClip clip, float time) => Mathf.RoundToInt(time * FrameRate(clip));


        public static float FrameRate(AnimationClip clip) => clip != null && clip.frameRate > 0f ? clip.frameRate : 30f;


        // ── Общие помощники ──────────────────────────────

        // Ключи ближе половины кадра — один и тот же кадр.
        private static float KeyTolerance(AnimationClip clip) => 0.5f / FrameRate(clip);


        // Кривые ручек и весов IK — всё, что лежит под корнем ручек или ограничений рига.
        private static bool IsRigBinding(EditorCurveBinding binding) =>
            IsUnder(binding.path, HumanoidAnimationRig.ControlsRootName) || IsUnder(binding.path, HumanoidAnimationRig.ConstraintsRootName);

        private static bool IsUnder(string path, string rootName) => path == rootName || path.StartsWith(rootName + "/");


        private static EditorCurveBinding TransformBinding(string path, string property, string axis) =>
            EditorCurveBinding.FloatCurve(path, typeof(Transform), property + "." + axis);


        private static int FindKeyIndex(AnimationCurve curve, float time, float tolerance)
        {
            for (var i = 0; i < curve.length; i++)
            {
                if (Mathf.Abs(curve[i].time - time) <= tolerance)
                    return i;
            }

            return -1;
        }


        // Новый ключ сглаживается как сосед слева: между ступеньками блокинга встанет ступенька,
        // между плавными — плавный.
        private static void ApplyNewKeyTangents(AnimationCurve curve, int index)
        {
            var mode = index > 0
                ? AnimationUtility.GetKeyRightTangentMode(curve, index - 1)
                : AnimationUtility.TangentMode.ClampedAuto;

            AnimationUtility.SetKeyLeftTangentMode(curve, index, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, index, mode);
        }


        // Ключ поменялся — авто-сглаживание пересчитывается у него и у соседей.
        // Тот же режим заново — Unity сама пересчитает касательные вокруг ключа.
        private static void RefreshTangents(AnimationCurve curve, int index)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.GetKeyLeftTangentMode(curve, index));
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.GetKeyRightTangentMode(curve, index));
        }

        private static void RefreshAllTangents(AnimationCurve curve)
        {
            for (var i = 0; i < curve.length; i++)
                RefreshTangents(curve, i);
        }
    }
}
