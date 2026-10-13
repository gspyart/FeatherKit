using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Куда на теле гуманоида попал луч мышки. Тело — набор капсул вдоль костей: таз,
    /// позвоночник, голова, руки, ноги, по желанию пальцы. Меш не нужен: капсулы считаются
    /// по костям аватара, поэтому работает с любым Humanoid и в любой позе.
    ///
    /// Из капсул, в которые попал луч, берётся ближняя к камере. Если рядом по глубине
    /// несколько (палец на ладони, рука у груди) — та, к оси которой луч прошёл ближе.
    /// </summary>
    internal static class HumanoidBodyHitTest
    {
        // Толщина частей тела — в долях роста.
        private const float PelvisRadius = 0.075f;
        private const float TorsoRadius = 0.08f;
        private const float NeckRadius = 0.03f;
        private const float HeadRadius = 0.06f;
        private const float HeadLength = 0.11f;
        private const float ClavicleRadius = 0.03f;
        private const float UpperArmRadius = 0.03f;
        private const float ForearmRadius = 0.026f;
        private const float HandRadius = 0.028f;
        private const float HandLength = 0.06f;
        private const float ThighRadius = 0.045f;
        private const float ShinRadius = 0.036f;
        private const float FootRadius = 0.03f;
        private const float ToesRadius = 0.022f;
        private const float ToesLength = 0.04f;
        private const float FingerRadius = 0.008f;
        private const float FingertipLength = 0.015f;

        // Капсулы, начало которых ближе к камере не дальше этого, считаются одним слоем.
        private const float SameLayerDepth = 0.05f;

        // Рост — сумма длин костей, а не расстояние «голова — стопа»: в приседе тело ниже, а толщина та же.
        private const float HeightPerChainLength = 1.2f;

        private static readonly HumanBodyBones[] TorsoChain =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
        };

        private static readonly List<BodyCapsule> Capsules = new List<BodyCapsule>();


        // ── Попадание ──────────────────────────────

        /// <summary>Найти кость, в которую попал луч: кость, расстояние до тела и ось её капсулы. Не попал — false.</summary>
        public static bool TryHit(Animator animator, Ray ray, bool withFingers, out HumanoidBodyHit hit)
        {
            hit = default;

            if (animator == null || animator.GetBoneTransform(HumanBodyBones.Hips) == null)
                return false;

            var height = MeasureHeight(animator);
            CollectCapsules(animator, height, withFingers);

            var nearestEntry = float.MaxValue;

            foreach (var capsule in Capsules)
            {
                if (capsule.TryIntersect(ray, out var entry, out _))
                    nearestEntry = Mathf.Min(nearestEntry, entry);
            }

            if (nearestEntry == float.MaxValue)
                return false;

            var bestCloseness = float.MaxValue;

            foreach (var capsule in Capsules)
            {
                if (!capsule.TryIntersect(ray, out var entry, out var closeness) || entry > nearestEntry + SameLayerDepth * height)
                    continue;

                if (closeness >= bestCloseness)
                    continue;

                bestCloseness = closeness;
                hit = new HumanoidBodyHit(capsule.Bone, entry, capsule.From, capsule.To);
            }

            return true;
        }


        // ── Капсулы тела ──────────────────────────────

        private static void CollectCapsules(Animator animator, float height, bool withFingers)
        {
            Capsules.Clear();

            AddTorso(animator, height);
            AddArm(animator, height, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal);
            AddArm(animator, height, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal);
            AddLeg(animator, height, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            AddLeg(animator, height, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);

            if (withFingers)
            {
                AddFingers(animator, height, HumanBodyBones.LeftThumbProximal);
                AddFingers(animator, height, HumanBodyBones.RightThumbProximal);
            }
        }


        // Позвоночник от таза до шеи — капсула от каждой кости к следующей. Голова — от себя вверх
        // по линии шеи.
        private static void AddTorso(Animator animator, float height)
        {
            for (var i = 0; i < TorsoChain.Length; i++)
            {
                var bone = TorsoChain[i];
                var boneTransform = animator.GetBoneTransform(bone);

                if (boneTransform == null)
                    continue;

                if (bone == HumanBodyBones.Head)
                {
                    var below = FindPreviousExisting(animator, i - 1);
                    var up = below != null ? (boneTransform.position - below.position).normalized : Vector3.up;
                    AddCapsule(bone, boneTransform.position, boneTransform.position + up * (HeadLength * height), HeadRadius * height);
                    continue;
                }

                var next = FindNextExisting(animator, i + 1);

                if (next == null)
                    continue;

                var radius = bone == HumanBodyBones.Hips ? PelvisRadius : bone == HumanBodyBones.Neck ? NeckRadius : TorsoRadius;
                AddCapsule(bone, boneTransform.position, next.position, radius * height);
            }
        }

        private static Transform FindNextExisting(Animator animator, int fromIndex)
        {
            for (var i = fromIndex; i < TorsoChain.Length; i++)
            {
                var boneTransform = animator.GetBoneTransform(TorsoChain[i]);

                if (boneTransform != null)
                    return boneTransform;
            }

            return null;
        }

        private static Transform FindPreviousExisting(Animator animator, int fromIndex)
        {
            for (var i = fromIndex; i >= 0; i--)
            {
                var boneTransform = animator.GetBoneTransform(TorsoChain[i]);

                if (boneTransform != null)
                    return boneTransform;
            }

            return null;
        }


        private static void AddArm(Animator animator, float height, HumanBodyBones clavicle, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand, HumanBodyBones middleFinger)
        {
            AddBetween(animator, clavicle, upper, ClavicleRadius * height);
            AddBetween(animator, upper, lower, UpperArmRadius * height);
            AddBetween(animator, lower, hand, ForearmRadius * height);
            AddTip(animator, hand, middleFinger, lower, HandLength * height, HandRadius * height);
        }


        private static void AddLeg(Animator animator, float height, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot, HumanBodyBones toes)
        {
            AddBetween(animator, upper, lower, ThighRadius * height);
            AddBetween(animator, lower, foot, ShinRadius * height);

            if (animator.GetBoneTransform(toes) != null)
            {
                AddBetween(animator, foot, toes, FootRadius * height);
                AddTip(animator, toes, null, foot, ToesLength * height, ToesRadius * height);
            }
            else
            {
                AddTip(animator, foot, null, lower, ToesLength * 2f * height, FootRadius * height);
            }
        }


        // Пять пальцев по три фаланги: фаланга — до следующей, последняя — чуть дальше себя.
        private static void AddFingers(Animator animator, float height, HumanBodyBones firstFingerBone)
        {
            const int FingersPerHand = 5;
            const int BonesPerFinger = 3;

            for (var finger = 0; finger < FingersPerHand; finger++)
            {
                for (var phalanx = 0; phalanx < BonesPerFinger; phalanx++)
                {
                    var bone = firstFingerBone + finger * BonesPerFinger + phalanx;
                    var isLast = phalanx == BonesPerFinger - 1;

                    if (isLast)
                        AddTip(animator, bone, null, bone - 1, FingertipLength * height, FingerRadius * height);
                    else
                        AddBetween(animator, bone, bone + 1, FingerRadius * height);
                }
            }
        }


        private static void AddBetween(Animator animator, HumanBodyBones bone, HumanBodyBones next, float radius)
        {
            var from = animator.GetBoneTransform(bone);
            var to = animator.GetBoneTransform(next);

            if (from != null && to != null)
                AddCapsule(bone, from.position, to.position, radius);
        }


        // Кость на конце цепочки: до кости-продолжения, а без неё — дальше по направлению от родителя.
        private static void AddTip(Animator animator, HumanBodyBones bone, HumanBodyBones? next, HumanBodyBones parent, float length, float radius)
        {
            var boneTransform = animator.GetBoneTransform(bone);

            if (boneTransform == null)
                return;

            var nextTransform = next.HasValue ? animator.GetBoneTransform(next.Value) : null;

            if (nextTransform != null)
            {
                AddCapsule(bone, boneTransform.position, nextTransform.position, radius);
                return;
            }

            var parentTransform = animator.GetBoneTransform(parent);
            var direction = parentTransform != null ? (boneTransform.position - parentTransform.position).normalized : Vector3.up;
            AddCapsule(bone, boneTransform.position, boneTransform.position + direction * length, radius);
        }


        private static void AddCapsule(HumanBodyBones bone, Vector3 from, Vector3 to, float radius) =>
            Capsules.Add(new BodyCapsule(bone, from, to, radius));


        private static float MeasureHeight(Animator animator)
        {
            var length = ChainLength(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)
                + ChainLength(animator, TorsoChain);

            return length > 0f ? length * HeightPerChainLength : 1.8f;
        }

        private static float ChainLength(Animator animator, params HumanBodyBones[] bones)
        {
            var length = 0f;
            Transform previous = null;

            foreach (var bone in bones)
            {
                var boneTransform = animator.GetBoneTransform(bone);

                if (boneTransform == null)
                    continue;

                if (previous != null)
                    length += Vector3.Distance(previous.position, boneTransform.position);

                previous = boneTransform;
            }

            return length;
        }


        /// <summary>Капсула вдоль кости: отрезок и толщина.</summary>
        private readonly struct BodyCapsule
        {
            public readonly HumanBodyBones Bone;
            public readonly Vector3 From;
            public readonly Vector3 To;
            private readonly float radius;

            public BodyCapsule(HumanBodyBones bone, Vector3 from, Vector3 to, float radius)
            {
                Bone = bone;
                From = from;
                To = to;
                this.radius = radius;
            }


            // Попал ли луч: ближайшая точка оси к лучу не дальше толщины. entry — где луч входит
            // в капсулу, closeness — насколько близко к оси прошёл: 0 — по оси, 1 — по краю.
            public bool TryIntersect(Ray ray, out float entry, out float closeness)
            {
                var distance = DistanceToRay(ray, From, To, out var alongRay);

                entry = alongRay - Mathf.Sqrt(Mathf.Max(0f, radius * radius - distance * distance));
                closeness = distance / radius;

                return distance <= radius && alongRay > 0f;
            }


            // Расстояние между лучом и отрезком: несколько шагов «спроецировать точку на луч,
            // потом на отрезок» сходятся к ближайшей паре точек.
            private static float DistanceToRay(Ray ray, Vector3 from, Vector3 to, out float alongRay)
            {
                var segment = to - from;
                var segmentLengthSquared = Mathf.Max(segment.sqrMagnitude, 1e-8f);
                var segmentShare = 0.5f;
                alongRay = 0f;

                for (var step = 0; step < 4; step++)
                {
                    var pointOnSegment = from + segment * segmentShare;
                    alongRay = Mathf.Max(0f, Vector3.Dot(pointOnSegment - ray.origin, ray.direction));

                    var pointOnRay = ray.origin + ray.direction * alongRay;
                    segmentShare = Mathf.Clamp01(Vector3.Dot(pointOnRay - from, segment) / segmentLengthSquared);
                }

                var closestOnSegment = from + segment * segmentShare;
                alongRay = Mathf.Max(0f, Vector3.Dot(closestOnSegment - ray.origin, ray.direction));

                return Vector3.Distance(ray.origin + ray.direction * alongRay, closestOnSegment);
            }
        }
    }


    /// <summary>Куда попал клик по телу: кость, глубина и ось её капсулы — для подсветки.</summary>
    internal readonly struct HumanoidBodyHit
    {
        public readonly HumanBodyBones Bone;
        public readonly float Depth;
        public readonly Vector3 From;
        public readonly Vector3 To;

        public HumanoidBodyHit(HumanBodyBones bone, float depth, Vector3 from, Vector3 to)
        {
            Bone = bone;
            Depth = depth;
            From = from;
            To = to;
        }
    }
}
