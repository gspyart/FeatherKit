using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Настройки инструмента «Здание» для нового здания: этажи, стены, перекрытия, материалы. Высота этажа — общая, из этажей окна.</summary>
    public static class LiveBuilderBuildingSettings
    {
        // Заводские значения, пока человек их не поменял: с ними сверяется «Ещё» окна — изменено ли.
        public const float FactoryWallThickness = 0.3f;
        public const float FactorySlabThickness = 0.2f;


        public static int StoreyCount
        {
            get => LiveBuilderPreferences.GetInt("Building.StoreyCount", 1);
            set => LiveBuilderPreferences.SetInt("Building.StoreyCount", Mathf.Clamp(value, 1, SplineBuilding.MaxStoreys));
        }

        public static float WallThickness
        {
            get => LiveBuilderPreferences.GetFloat("Building.WallThickness", FactoryWallThickness);
            set => LiveBuilderPreferences.SetFloat("Building.WallThickness", Mathf.Max(0.02f, value));
        }

        // Тип наружных стен нового здания; пусто — толщина и материал свои.
        public static SplineWallType WallType
        {
            get => LiveBuilderPreferences.GetWallType("Building.WallType");
            set => LiveBuilderPreferences.SetWallType("Building.WallType", value);
        }

        public static SplineBuildingContourSide ContourSide
        {
            get => LiveBuilderPreferences.GetEnum("Building.ContourSide", SplineBuildingContourSide.Outside);
            set => LiveBuilderPreferences.SetInt("Building.ContourSide", (int)value);
        }

        public static float SlabThickness
        {
            get => LiveBuilderPreferences.GetFloat("Building.SlabThickness", FactorySlabThickness);
            set => LiveBuilderPreferences.SetFloat("Building.SlabThickness", Mathf.Max(0.02f, value));
        }

        public static bool HasFloor
        {
            get => LiveBuilderPreferences.GetBool("Building.HasFloor", true);
            set => LiveBuilderPreferences.SetBool("Building.HasFloor", value);
        }

        public static bool HasRoof
        {
            get => LiveBuilderPreferences.GetBool("Building.HasRoof", true);
            set => LiveBuilderPreferences.SetBool("Building.HasRoof", value);
        }

        // Парапет у нового здания: стены над крышей бортиком.
        public static bool HasParapet
        {
            get => LiveBuilderPreferences.GetBool("Building.HasParapet", false);
            set => LiveBuilderPreferences.SetBool("Building.HasParapet", value);
        }

        public static float ParapetHeight
        {
            get => LiveBuilderPreferences.GetFloat("Building.ParapetHeight", 1f);
            set => LiveBuilderPreferences.SetFloat("Building.ParapetHeight", Mathf.Max(0.05f, value));
        }

        public static Material OuterWallMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Building.OuterWallMaterial");
            set => LiveBuilderPreferences.SetMaterial("Building.OuterWallMaterial", value);
        }

        public static Material InnerWallMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Building.InnerWallMaterial");
            set => LiveBuilderPreferences.SetMaterial("Building.InnerWallMaterial", value);
        }

        public static Material FloorMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Building.FloorMaterial");
            set => LiveBuilderPreferences.SetMaterial("Building.FloorMaterial", value);
        }

        public static Material CeilingMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Building.CeilingMaterial");
            set => LiveBuilderPreferences.SetMaterial("Building.CeilingMaterial", value);
        }

        public static Material RoofMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Building.RoofMaterial");
            set => LiveBuilderPreferences.SetMaterial("Building.RoofMaterial", value);
        }
    }
}
