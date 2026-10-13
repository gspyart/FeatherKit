using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Делает из гуманоида рабочую копию для анимации: только скелет, меши и Animator.
    /// Игровые скрипты, коллайдеры и прочее снимаются, чтобы копия ничего не делала сама
    /// и не тянула за собой игру. Оригинал и его префаб не трогаются.
    /// </summary>
    internal static class HumanoidWorkingCopyCreator
    {
        private const string NameSuffix = " (Animation Rig)";
        private const string EditorOnlyTag = "EditorOnly";
        private const int StripPassesLimit = 5;


        // ── Поиск гуманоида ──────────────────────────────

        /// <summary>
        /// Найти в объекте Animator с правильным Humanoid-аватаром. Не нашли — в
        /// <paramref name="problem"/> объяснение для человека.
        /// </summary>
        public static bool TryFindHumanoidAnimator(GameObject source, out Animator animator, out string problem)
        {
            animator = null;
            problem = null;

            if (source == null)
            {
                problem = "Выделите персонажа в сцене или его модель в окне Project.";
                return false;
            }

            if (source.GetComponentInParent<HumanoidAnimationRig>() != null)
            {
                problem = "Это уже рабочая копия для анимации. Выделите исходного персонажа.";
                return false;
            }

            animator = source.GetComponentInChildren<Animator>(true);

            if (animator == null || animator.avatar == null)
            {
                problem = "У объекта нет Animator с аватаром.\n\nВыберите модель в Project, вкладка Rig → Animation Type: Humanoid.";
                return false;
            }

            if (!animator.avatar.isHuman || !animator.avatar.isValid)
            {
                problem = "Аватар не гуманоидный или собран с ошибками.\n\nВыберите модель в Project, вкладка Rig → Animation Type: Humanoid → Configure.";
                return false;
            }

            return true;
        }


        // ── Создание копии ──────────────────────────────

        /// <summary>
        /// Создать копию в открытой сцене. Undo не регистрирует: копию ещё достраивают,
        /// и отменять её надо целиком, уже готовую.
        /// </summary>
        public static GameObject Create(Animator source)
        {
            var copy = Object.Instantiate(source.gameObject);

            copy.name = source.gameObject.name + NameSuffix;
            copy.tag = EditorOnlyTag;
            copy.transform.SetParent(null);
            copy.transform.SetPositionAndRotation(ChoosePosition(source), source.transform.rotation);

            StripComponents(copy);
            PrepareAnimator(copy.GetComponent<Animator>());

            return copy;
        }

        // Из сцены ставим рядом с оригиналом, чтобы не наложились. Из Project — в начало координат.
        private static Vector3 ChoosePosition(Animator source)
        {
            if (!source.gameObject.scene.IsValid())
                return Vector3.zero;

            return source.transform.position + source.transform.right * 1.5f;
        }


        // Копия — только картинка и скелет. Всё остальное снимаем по кругу: компонент, на который
        // опирается другой, удалится не с первого прохода.
        private static void StripComponents(GameObject copy)
        {
            foreach (var child in copy.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

            for (var pass = 0; pass < StripPassesLimit; pass++)
            {
                var removable = CollectRemovableComponents(copy);

                if (removable.Count == 0)
                    return;

                foreach (var component in removable)
                    Object.DestroyImmediate(component);
            }
        }

        // Скрипты первыми, а внутри объекта — с конца: добавленные позже обычно опираются на ранние.
        private static List<Component> CollectRemovableComponents(GameObject copy)
        {
            var scripts = new List<Component>();
            var others = new List<Component>();

            foreach (var child in copy.GetComponentsInChildren<Transform>(true))
            {
                var components = child.GetComponents<Component>();

                for (var i = components.Length - 1; i >= 0; i--)
                {
                    var component = components[i];

                    if (component == null || IsKeptComponent(component))
                        continue;

                    if (component is MonoBehaviour)
                        scripts.Add(component);
                    else
                        others.Add(component);
                }
            }

            scripts.AddRange(others);
            return scripts;
        }

        private static bool IsKeptComponent(Component component) =>
            component is Transform
            || component is Animator
            || component is SkinnedMeshRenderer
            || component is MeshRenderer
            || component is MeshFilter;


        // Контроллер оригинала убираем: иначе новые клипы попали бы в игровой контроллер.
        // Окно Animation само заведёт новый, когда нажмёшь Create.
        private static void PrepareAnimator(Animator animator)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
