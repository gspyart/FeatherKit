using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.HumanoidAnimation
{
    /// <summary>
    /// Рабочая копия гуманоида для анимации в редакторе. Помнит, где лежат ручки, ограничения
    /// Animation Rigging и конечности с режимами IK/FK, помнит свой контроллер с клипами
    /// (чтобы вернуть его, если Animator его потерял), настройки анимирования — ключ на всю позу,
    /// пересборку игровых клипов — и точку, откуда смотреть на позу глазами персонажа.
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

        private const float DefaultEyesViewFieldOfView = 60f;

        [SerializeField] private Transform controlsRoot;
        [SerializeField] private Transform constraintsRoot;
        [SerializeField] private List<HumanoidRigControl> controls = new List<HumanoidRigControl>();
        [SerializeField] private List<HumanoidRigLimb> limbs = new List<HumanoidRigLimb>();
        [SerializeField] private bool hasFingerControls;
        [SerializeField] private bool controlsShown = true;
        [SerializeField] private RuntimeAnimatorController animationController;
        [SerializeField] private bool bakeOnSave = true;
        [SerializeField] private bool keyWholePose = true;
        [SerializeField] private bool hasEyesView;
        [SerializeField] private Vector3 eyesViewPosition;
        [SerializeField] private Quaternion eyesViewRotation = Quaternion.identity;
        [SerializeField] private float eyesViewFieldOfView = DefaultEyesViewFieldOfView;


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


        // ── Запекание и ключи ──────────────────────────────

        /// <summary>Пересобирать игровые Humanoid-клипы самим: при сохранении проекта и перед запуском игры.</summary>
        public bool BakeOnSave
        {
            get => bakeOnSave;
            set => bakeOnSave = value;
        }

        /// <summary>Сдвинул одну ручку при записи — ключ встаёт всем ручкам в этом кадре.</summary>
        public bool KeyWholePose
        {
            get => keyWholePose;
            set => keyWholePose = value;
        }


        // ── Вид глазами ──────────────────────────────

        /// <summary>Запомнена ли своя точка взгляда. Нет — тула считает её от головы.</summary>
        public bool HasEyesView => hasEyesView;

        /// <summary>Откуда смотрят глаза, в пространстве рига.</summary>
        public Vector3 EyesViewPosition => eyesViewPosition;

        /// <summary>Куда смотрят глаза, в пространстве рига.</summary>
        public Quaternion EyesViewRotation => eyesViewRotation;

        /// <summary>Угол обзора, как у камеры в игре.</summary>
        public float EyesViewFieldOfView => eyesViewFieldOfView;


        // ── Сборка ──────────────────────────────

        /// <summary>Запомнить собранный риг. Зовёт только тула, которая его строит.</summary>
        public void AssignBuiltRig(Transform newControlsRoot, Transform newConstraintsRoot, List<HumanoidRigControl> newControls, List<HumanoidRigLimb> newLimbs, bool withFingers)
        {
            controlsRoot = newControlsRoot;
            constraintsRoot = newConstraintsRoot;
            controls = newControls;
            limbs = newLimbs;
            hasFingerControls = withFingers;
        }


        // ── Поиск ручек и конечностей ──────────────────────────────

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


        /// <summary>Ручка, которой принадлежит объект. Не ручка — null.</summary>
        public HumanoidRigControl FindControlByHandle(Transform handle)
        {
            if (handle == null)
                return null;

            foreach (var control in controls)
            {
                if (control.Handle == handle)
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


        /// <summary>
        /// Работает ли ручка сейчас. Ручки корпуса работают всегда, у руки и ноги — только
        /// ручки текущего режима: в IK кольца стоят без дела, в FK — кубик и шарик.
        /// </summary>
        public bool IsInActiveMode(HumanoidRigControl control)
        {
            var limb = FindLimbOf(control);

            if (limb == null)
                return true;

            var isIKControl = control.Type != HumanoidRigControlType.BoneRotation;
            return isIKControl == limb.IsInIK;
        }


        /// <summary>Лежит ли объект внутри этого рига.</summary>
        public bool Contains(Transform target) => target != null && target.IsChildOf(transform);


        // ── Вид глазами ──────────────────────────────

        /// <summary>Запомнить свою точку взгляда: положение и поворот в пространстве рига и угол обзора.</summary>
        public void SetEyesView(Vector3 localPosition, Quaternion localRotation, float fieldOfView)
        {
            hasEyesView = true;
            eyesViewPosition = localPosition;
            eyesViewRotation = localRotation;
            eyesViewFieldOfView = fieldOfView;
        }


        /// <summary>Забыть свою точку взгляда — тула снова будет считать её от головы.</summary>
        public void ForgetEyesView()
        {
            hasEyesView = false;
            eyesViewFieldOfView = DefaultEyesViewFieldOfView;
        }
    }
}
