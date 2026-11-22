using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Каким будет вырез (<see cref="GeometryCutter"/>), который ставит инструмент «Cut», и где он встаёт кликом: форма, насквозь или ниша и её глубина
    /// (у протянутого), материал стенок дыры, шаг и отступ ряда по Shift + клику. Готовые размеры для клика — <see cref="LiveBuilderOpeningPresets"/>.
    /// </summary>
    public static class LiveBuilderCutterSettings
    {
        // Заводские значения, пока человек их не поменял: с ними сверяется «Ещё» окна — изменено ли.
        public const float FactoryNicheDepth = 0.15f;
        public const float FactoryRowStep = 3f;
        public const float FactoryRowMargin = 1f;


        public static LiveBuilderCutterShape Shape
        {
            get => LiveBuilderPreferences.GetEnum("Cutter.Shape", LiveBuilderCutterShape.Rectangle);
            set => LiveBuilderPreferences.SetInt("Cutter.Shape", (int)value);
        }

        // Насквозь — через всю толщину того, на чём рисуешь; иначе — ниша своей глубины.
        public static bool IsThrough
        {
            get => LiveBuilderPreferences.GetBool("Cutter.IsThrough", true);
            set => LiveBuilderPreferences.SetBool("Cutter.IsThrough", value);
        }

        // Глубина ниши от поверхности, м.
        public static float NicheDepth
        {
            get => LiveBuilderPreferences.GetFloat("Cutter.NicheDepth", FactoryNicheDepth);
            set => LiveBuilderPreferences.SetFloat("Cutter.NicheDepth", Mathf.Max(0.01f, value));
        }

        // Материал стенок дыры; пусто — как у того, что режем.
        public static Material RevealMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Cutter.RevealMaterial");
            set => LiveBuilderPreferences.SetMaterial("Cutter.RevealMaterial", value);
        }

        // Проём кликом — левым краем и низом по сетке; выключено — серединой ровно под мышью. По высоте в здании — всегда от пола его этажа.
        public static bool IsSnappedToGrid
        {
            get => LiveBuilderPreferences.GetBool("Cutter.IsSnappedToGrid", false);
            set => LiveBuilderPreferences.SetBool("Cutter.IsSnappedToGrid", value);
        }

        // Шаг проёмов в ряду по Shift + клику не меньше этого, м: Fill растягивает его, чтобы ряд лёг в сторону ровно.
        public static float RowStep
        {
            get => LiveBuilderPreferences.GetFloat("Cutter.RowStep", FactoryRowStep);
            set => LiveBuilderPreferences.SetFloat("Cutter.RowStep", Mathf.Max(0.1f, value));
        }

        // Отступ ряда от углов стороны, м.
        public static float RowMargin
        {
            get => LiveBuilderPreferences.GetFloat("Cutter.RowMargin", FactoryRowMargin);
            set => LiveBuilderPreferences.SetFloat("Cutter.RowMargin", Mathf.Max(0f, value));
        }
    }
}
