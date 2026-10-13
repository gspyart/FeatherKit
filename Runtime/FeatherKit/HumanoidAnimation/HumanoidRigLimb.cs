using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FeatherKit.HumanoidAnimation
{
    /// <summary>
    /// Рука или нога.
    /// </summary>
    public enum HumanoidLimb
    {
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
    }


    /// <summary>
    /// Конечность рига с двумя режимами управления. FK — вращаешь суставы по очереди,
    /// IK — тянешь кисть или стопу, сустав сгибается сам. Режим — это вес IK-ограничения:
    /// 0 — чистый FK, 1 — чистый IK, между ними — смесь. Вес анимируется, поэтому
    /// переход между режимами можно растянуть на несколько кадров.
    /// </summary>
    [Serializable]
    public class HumanoidRigLimb
    {
        [SerializeField] private HumanoidLimb limb;
        [SerializeField] private HumanBodyBones upperBone;
        [SerializeField] private HumanBodyBones middleBone;
        [SerializeField] private HumanBodyBones endBone;
        [SerializeField] private TwoBoneIKConstraint ikConstraint;

        public HumanoidRigLimb(HumanoidLimb limb, HumanBodyBones upperBone, HumanBodyBones middleBone, HumanBodyBones endBone, TwoBoneIKConstraint ikConstraint)
        {
            this.limb = limb;
            this.upperBone = upperBone;
            this.middleBone = middleBone;
            this.endBone = endBone;
            this.ikConstraint = ikConstraint;
        }


        // ── Устройство ──────────────────────────────

        public HumanoidLimb Limb => limb;

        /// <summary>Плечевой сустав или бедро.</summary>
        public HumanBodyBones UpperBone => upperBone;

        /// <summary>Предплечье или голень — кость, которая сгибается в локте или колене.</summary>
        public HumanBodyBones MiddleBone => middleBone;

        /// <summary>Кисть или стопа.</summary>
        public HumanBodyBones EndBone => endBone;

        public TwoBoneIKConstraint IKConstraint => ikConstraint;


        // ── Режим ──────────────────────────────

        /// <summary>Доля IK: 0 — FK, 1 — IK.</summary>
        public float IKWeight => ikConstraint != null ? ikConstraint.weight : 1f;

        /// <summary>Какой режим сейчас главный. Смесь ближе к IK считается IK.</summary>
        public bool IsInIK => IKWeight >= 0.5f;


        /// <summary>Входит ли кость в цепочку этой конечности.</summary>
        public bool ContainsBone(HumanBodyBones bone) => bone == upperBone || bone == middleBone || bone == endBone;
    }
}
