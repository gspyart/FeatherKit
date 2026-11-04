using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Настройки инструмента «Лестница»: вид, высота, ширина по умолчанию, пробивать ли перекрытие.</summary>
    public static class LiveBuilderStairsSettings
    {
        public static BoxModelStairsStyle Style
        {
            get => LiveBuilderPreferences.GetEnum("Stairs.Style", BoxModelStairsStyle.SeparateSteps);
            set => LiveBuilderPreferences.SetInt("Stairs.Style", (int)value);
        }

        // Толщина плиты ступени у вида «Ступени», м.
        public static float StepThickness
        {
            get => LiveBuilderPreferences.GetFloat("Stairs.StepThickness", 0.2f);
            set => LiveBuilderPreferences.SetFloat("Stairs.StepThickness", Mathf.Max(0.02f, value));
        }

        // Высота — на этаж, иначе своя.
        public static bool IsStoreyHigh
        {
            get => LiveBuilderPreferences.GetBool("Stairs.IsStoreyHigh", true);
            set => LiveBuilderPreferences.SetBool("Stairs.IsStoreyHigh", value);
        }

        public static float CustomHeight
        {
            get => LiveBuilderPreferences.GetFloat("Stairs.CustomHeight", 2f);
            set => LiveBuilderPreferences.SetFloat("Stairs.CustomHeight", Mathf.Max(0.2f, value));
        }

        // Ширина, если протянул почти без ширины — просто линию, м.
        public static float DefaultWidth
        {
            get => LiveBuilderPreferences.GetFloat("Stairs.DefaultWidth", 2f);
            set => LiveBuilderPreferences.SetFloat("Stairs.DefaultWidth", Mathf.Max(0.5f, value));
        }

        public static bool CutsAbove
        {
            get => LiveBuilderPreferences.GetBool("Stairs.CutsAboveByDefault", false);
            set => LiveBuilderPreferences.SetBool("Stairs.CutsAboveByDefault", value);
        }

        public static float Height => IsStoreyHigh ? LiveBuilderStoreys.StoreyHeight : CustomHeight;
    }
}
