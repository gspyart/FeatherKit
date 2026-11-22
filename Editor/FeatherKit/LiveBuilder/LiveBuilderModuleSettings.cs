using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Настройки инструмента «Module», свои у каждого человека: какой префаб ставить (общий с палитрой уровня, <see cref="LevelPaletteActiveAsset"/>)
    /// и шаг с отступом ряда, который ставит Shift + клик.
    /// </summary>
    public static class LiveBuilderModuleSettings
    {
        public const float FactoryRowStep = 3f;
        public const float FactoryRowMargin = 1f;


        public static GameObject Prefab
        {
            get => LevelPaletteActiveAsset.Prefab;
            set => LevelPaletteActiveAsset.Prefab = value;
        }

        // Шаг модулей в ряду не меньше этого, м: Fill растягивает его, чтобы ряд лёг в сторону ровно.
        public static float RowStep
        {
            get => LiveBuilderPreferences.GetFloat("Module.RowStep", FactoryRowStep);
            set => LiveBuilderPreferences.SetFloat("Module.RowStep", Mathf.Max(0.1f, value));
        }

        // Отступ ряда от углов стороны, м.
        public static float RowMargin
        {
            get => LiveBuilderPreferences.GetFloat("Module.RowMargin", FactoryRowMargin);
            set => LiveBuilderPreferences.SetFloat("Module.RowMargin", Mathf.Max(0f, value));
        }
    }
}
