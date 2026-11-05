using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Настройки инструмента «Стена»: размеры новой стены, толщина перегородки в здании и какой будет новая точка — угол или плавно.</summary>
    public static class LiveBuilderWallSettings
    {
        // Заводские значения, пока человек их не поменял: с ними сверяется «Ещё» окна — изменено ли.
        public const float FactoryThickness = 0.3f;
        public const float FactoryPartitionThickness = 0.15f;


        public static float Thickness
        {
            get => LiveBuilderPreferences.GetFloat("Wall.Thickness", FactoryThickness);
            set => LiveBuilderPreferences.SetFloat("Wall.Thickness", Mathf.Max(0.02f, value));
        }

        // Высота — до потолка этажа (этаж минус перекрытие), иначе своя.
        public static bool IsStoreyHigh
        {
            get => LiveBuilderPreferences.GetBool("Wall.IsStoreyHigh", true);
            set => LiveBuilderPreferences.SetBool("Wall.IsStoreyHigh", value);
        }

        public static float CustomHeight
        {
            get => LiveBuilderPreferences.GetFloat("Wall.CustomHeight", 3.8f);
            set => LiveBuilderPreferences.SetFloat("Wall.CustomHeight", Mathf.Max(0.05f, value));
        }

        // Толщина перегородки внутри здания по контуру, м.
        public static float PartitionThickness
        {
            get => LiveBuilderPreferences.GetFloat("Wall.PartitionThickness", FactoryPartitionThickness);
            set => LiveBuilderPreferences.SetFloat("Wall.PartitionThickness", Mathf.Max(0.02f, value));
        }

        public static SplineWallAlignment Alignment
        {
            get => LiveBuilderPreferences.GetEnum("Wall.Alignment", SplineWallAlignment.Center);
            set => LiveBuilderPreferences.SetInt("Wall.Alignment", (int)value);
        }

        // Новая точка — плавный изгиб, а не угол. Shift на клике — наоборот.
        public static bool IsSmooth
        {
            get => LiveBuilderPreferences.GetBool("Wall.IsSmooth", false);
            set => LiveBuilderPreferences.SetBool("Wall.IsSmooth", value);
        }

        public static Material Material
        {
            get => LiveBuilderPreferences.GetMaterial("Wall.Material");
            set => LiveBuilderPreferences.SetMaterial("Wall.Material", value);
        }

        // Тип новой стены вне здания; пусто — толщина и материал свои.
        public static SplineWallType WallType
        {
            get => LiveBuilderPreferences.GetWallType("Wall.WallType");
            set => LiveBuilderPreferences.SetWallType("Wall.WallType", value);
        }

        // Тип перегородки в здании; пусто — толщина перегородки и материал свои.
        public static SplineWallType PartitionWallType
        {
            get => LiveBuilderPreferences.GetWallType("Wall.PartitionWallType");
            set => LiveBuilderPreferences.SetWallType("Wall.PartitionWallType", value);
        }

        public static float Height => IsStoreyHigh ? LiveBuilderRoomSettings.ClearHeight : CustomHeight;
    }
}
