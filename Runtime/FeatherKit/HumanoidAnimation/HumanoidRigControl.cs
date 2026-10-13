using System;
using UnityEngine;

namespace FeatherKit.HumanoidAnimation
{
    /// <summary>
    /// Что делает ручка рига с костью.
    /// </summary>
    public enum HumanoidRigControlType
    {
        /// <summary>Таз: двигает и поворачивает всё тело.</summary>
        BodyRoot,

        /// <summary>Поворот одной кости: позвоночник, голова, плечо, палец.</summary>
        BoneRotation,

        /// <summary>Цель IK для кисти или стопы: тянешь — конечность сгибается сама.</summary>
        LimbTarget,

        /// <summary>Направление сгиба локтя или колена.</summary>
        LimbBendHint,
    }


    /// <summary>
    /// Одна ручка рига: какая кость, что ручка с ней делает и в какой позе ручка стояла
    /// при создании. Позу помним, чтобы ручку можно было вернуть на место.
    /// </summary>
    [Serializable]
    public class HumanoidRigControl
    {
        [SerializeField] private HumanBodyBones bone;
        [SerializeField] private HumanoidRigControlType type;
        [SerializeField] private Transform handle;
        [SerializeField] private Vector3 restLocalPosition;
        [SerializeField] private Quaternion restLocalRotation;

        public HumanoidRigControl(HumanBodyBones bone, HumanoidRigControlType type, Transform handle)
        {
            this.bone = bone;
            this.type = type;
            this.handle = handle;
            restLocalPosition = handle.localPosition;
            restLocalRotation = handle.localRotation;
        }

        /// <summary>Кость, к которой относится ручка. У подсказки сгиба — локоть или колено.</summary>
        public HumanBodyBones Bone => bone;

        public HumanoidRigControlType Type => type;

        /// <summary>Объект ручки, который двигают и анимируют.</summary>
        public Transform Handle => handle;

        public Vector3 RestLocalPosition => restLocalPosition;

        public Quaternion RestLocalRotation => restLocalRotation;
    }
}
