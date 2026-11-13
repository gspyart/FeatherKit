using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Где блок сценария лежит в меню «+» инспектора и как он подписан.
    /// Путь через «/»: последняя часть — название блока.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ScenarioBlockAttribute : Attribute
    {
        public ScenarioBlockAttribute(string menuPath)
        {
            MenuPath = menuPath;
        }


        public string MenuPath { get; }

        public string Title => MenuPath.Substring(MenuPath.LastIndexOf('/') + 1);


        /// <summary>Название блока или ноды по атрибуту; без атрибута — имя класса. Для подписей в игре, без редактора.</summary>
        public static string TitleOf(Type blockType)
        {
            if (blockType == null)
                return null;

            var attribute = (ScenarioBlockAttribute)GetCustomAttribute(blockType, typeof(ScenarioBlockAttribute), inherit: false);

            return attribute != null ? attribute.Title : blockType.Name;
        }
    }
}
