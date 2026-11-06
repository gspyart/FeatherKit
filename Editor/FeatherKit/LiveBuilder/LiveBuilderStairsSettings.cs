using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Настройки инструмента «Лестница»: форма (прямая или с площадкой), вид, высота, ширина по умолчанию, пробивать ли перекрытие.</summary>
    public static class LiveBuilderStairsSettings
    {
        // Заводские значения, пока человек их не поменял: с ними сверяется «Ещё» окна — изменено ли.
        public const float FactoryStepThickness = 0.2f;
        public const float FactoryDefaultWidth = 2f;


        // Прямая — один марш; с площадкой — протянутый прямоугольник это вся лестничная клетка, два марша туда-обратно.
        public static BoxModelStairsShape Shape
        {
            get => LiveBuilderPreferences.GetEnum("Stairs.Shape", BoxModelStairsShape.Straight);
            set => LiveBuilderPreferences.SetInt("Stairs.Shape", (int)value);
        }

        public static BoxModelStairsStyle Style
        {
            get => LiveBuilderPreferences.GetEnum("Stairs.Style", BoxModelStairsStyle.SeparateSteps);
            set => LiveBuilderPreferences.SetInt("Stairs.Style", (int)value);
        }

        // Толщина плиты ступени у вида «Ступени», м.
        public static float StepThickness
        {
            get => LiveBuilderPreferences.GetFloat("Stairs.StepThickness", FactoryStepThickness);
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
            get => LiveBuilderPreferences.GetFloat("Stairs.DefaultWidth", FactoryDefaultWidth);
            set => LiveBuilderPreferences.SetFloat("Stairs.DefaultWidth", Mathf.Max(0.5f, value));
        }

        // Пробивать перекрытие над маршем — сразу: лестница на этаж выше без дыры в полу бесполезна.
        public static bool CutsAbove
        {
            get => LiveBuilderPreferences.GetBool("Stairs.CutsAboveSlab", true);
            set => LiveBuilderPreferences.SetBool("Stairs.CutsAboveSlab", value);
        }

        public static float Height => IsStoreyHigh ? LiveBuilderStoreys.StoreyHeight : CustomHeight;
    }
}
