using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.HumanoidAnimation
{
    /// <summary>
    /// Рабочая копия гуманоида для анимации в редакторе. Помнит, где лежат ручки, ограничения
    /// Animation Rigging и конечности с режимами IK/FK, помнит свой контроллер с клипами
    /// (чтобы вернуть его, если Animator его потерял) и хранит настройки запекания в Humanoid-клип.
    ///
    /// Сам ничего не делает: создаёт и обслуживает его редакторная тула FeatherKit.
    /// Объект помечен EditorOnly и в билд не попадает.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [RequireComponent(typeof(Animator))]
    public class HumanoidAnimationRig : MonoBehaviour
    {
        public const string ControlsRootName = "Animation Controls";
        public const string ConstraintsRootName = "Animation Rig";

        [SerializeField] private Transform controlsRoot;
        [SerializeField] private Transform constraintsRoot;
        [SerializeField] private List<HumanoidRigControl> controls = new List<HumanoidRigControl>();
        [SerializeField] private List<HumanoidRigLimb> limbs = new List<HumanoidRigLimb>();
        [SerializeField] private bool hasFingerControls;
        [SerializeField] private bool controlsShown = true;
        [SerializeField] private RuntimeAnimatorController animationController;
        [SerializeField] private bool bakeOnSave = true;


        // ── Устройство рига ──────────────────────────────

        public Animator Animator => GetComponent<Animator>();

        /// <summary>Корень ручек. Пути в анимационных клипах начинаются с него.</summary>
        public Transform ControlsRoot => controlsRoot;

        /// <summary>Корень ограничений Animation Rigging.</summary>
        public Transform ConstraintsRoot => constraintsRoot;

        public IReadOnlyList<HumanoidRigControl> Controls => controls;

        /// <summary>Руки и ноги с режимами IK/FK. Пусто — риг собран до появления FK, его надо пересоздать.</summary>
        public IReadOnlyList<HumanoidRigLimb> Limbs => limbs;

        public bool HasFingerControls => hasFingerControls;

        /// <summary>Показывать ли ручки в окне сцены.</summary>
        public bool ControlsShown
        {
            get => controlsShown;
            set => controlsShown = value;
        }


        // ── Клипы ──────────────────────────────

        /// <summary>
        /// Контроллер, в котором окно Animation держит клипы рига. Копия на случай,
        /// если Animator его потеряет: после Ctrl+Z или пересоздания рига.
        /// </summary>
        public RuntimeAnimatorController AnimationController
        {
            get => animationController;
            set => animationController = value;
        }


        // ── Запекание ──────────────────────────────

        /// <summary>Пересобирать Humanoid-клипы при сохранении проекта.</summary>
        public bool BakeOnSave
        {
            get => bakeOnSave;
            set => bakeOnSave = value;
        }


        // ── Методы ──────────────────────────────

        /// <summary>Запомнить собранный риг. Зовёт только тула, которая его строит.</summary>
        public void AssignBuiltRig(Transform newControlsRoot, Transform newConstraintsRoot, List<HumanoidRigControl> newControls, List<HumanoidRigLimb> newLimbs, bool withFingers)
        {
            controlsRoot = newControlsRoot;
            constraintsRoot = newConstraintsRoot;
            controls = newControls;
            limbs = newLimbs;
            hasFingerControls = withFingers;
        }


        /// <summary>Найти ручку по кости и роли. Нет такой — null.</summary>
        public HumanoidRigControl FindControl(HumanBodyBones bone, HumanoidRigControlType type)
        {
            foreach (var control in controls)
            {
                if (control.Bone == bone && control.Type == type && control.Handle != null)
                    return control;
            }

            return null;
        }


        /// <summary>Конечность по названию. Риг старой сборки без FK — null.</summary>
        public HumanoidRigLimb FindLimb(HumanoidLimb limb)
        {
            foreach (var rigLimb in limbs)
            {
                if (rigLimb.Limb == limb)
                    return rigLimb;
            }

            return null;
        }


        /// <summary>Конечность, к которой относится ручка. Ручка корпуса — null.</summary>
        public HumanoidRigLimb FindLimbOf(HumanoidRigControl control)
        {
            foreach (var rigLimb in limbs)
            {
                var isIKControl = control.Type == HumanoidRigControlType.LimbTarget && control.Bone == rigLimb.EndBone
                    || control.Type == HumanoidRigControlType.LimbBendHint && control.Bone == rigLimb.MiddleBone;

                var isFKControl = control.Type == HumanoidRigControlType.BoneRotation && rigLimb.ContainsBone(control.Bone);

                if (isIKControl || isFKControl)
                    return rigLimb;
            }

            return null;
        }


        /// <summary>Лежит ли объект внутри этого рига.</summary>
        public bool Contains(Transform target) => target != null && target.IsChildOf(transform);
    }
}
