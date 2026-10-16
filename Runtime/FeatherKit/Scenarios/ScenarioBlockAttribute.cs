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
    }
}
