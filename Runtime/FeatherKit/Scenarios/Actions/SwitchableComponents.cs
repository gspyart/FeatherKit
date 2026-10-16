using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Включение и выключение компонента любого вида: у скрипта и света, рендерера, коллайдера
    /// свойство enabled своё, общего предка у них нет.
    /// </summary>
    public static class SwitchableComponents
    {
        // Пустое поле в Unity — не настоящий null, поэтому проверка до switch, а не через case null.
        public static bool IsEnabled(Component component)
        {
            if (component == null)
                throw new InvalidOperationException("Компонент не выбран");

            return component switch
            {
                Behaviour behaviour => behaviour.enabled,
                Renderer renderer => renderer.enabled,
                Collider collider => collider.enabled,
                _ => throw NotSwitchable(component)
            };
        }

        public static void SetEnabled(Component component, bool isEnabled)
        {
            if (component == null)
                throw new InvalidOperationException("Компонент не выбран");

            switch (component)
            {
                case Behaviour behaviour:
                    behaviour.enabled = isEnabled;
                    break;

                case Renderer renderer:
                    renderer.enabled = isEnabled;
                    break;

                case Collider collider:
                    collider.enabled = isEnabled;
                    break;

                default:
                    throw NotSwitchable(component);
            }
        }


        private static Exception NotSwitchable(Component component)
        {
            return new InvalidOperationException($"{component.GetType().Name} нельзя включить или выключить");
        }
    }
}
