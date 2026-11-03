using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Настройки, которые помнит редактор: числа, флажки и материалы — у каждого человека свои.
    /// Окно присваивает их на каждой перерисовке, поэтому то же значение заново не пишется.
    /// </summary>
    public static class LiveBuilderPreferences
    {
        private const string Prefix = "FeatherKit.LiveBuilder.";


        public static float GetFloat(string name, float fallback)
        {
            return EditorPrefs.GetFloat(Prefix + name, fallback);
        }


        public static void SetFloat(string name, float value)
        {
            if (!EditorPrefs.HasKey(Prefix + name) || !Mathf.Approximately(EditorPrefs.GetFloat(Prefix + name), value))
                EditorPrefs.SetFloat(Prefix + name, value);
        }


        public static bool GetBool(string name, bool fallback)
        {
            return EditorPrefs.GetBool(Prefix + name, fallback);
        }


        public static void SetBool(string name, bool value)
        {
            if (!EditorPrefs.HasKey(Prefix + name) || EditorPrefs.GetBool(Prefix + name) != value)
                EditorPrefs.SetBool(Prefix + name, value);
        }


        public static int GetInt(string name, int fallback)
        {
            return EditorPrefs.GetInt(Prefix + name, fallback);
        }


        public static void SetInt(string name, int value)
        {
            if (!EditorPrefs.HasKey(Prefix + name) || EditorPrefs.GetInt(Prefix + name) != value)
                EditorPrefs.SetInt(Prefix + name, value);
        }


        // Материал — по адресу файла: так он переживает перезапуск редактора.
        public static Material GetMaterial(string name)
        {
            var guid = EditorPrefs.GetString(Prefix + name, "");
            return guid.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }


        public static void SetMaterial(string name, Material material)
        {
            SetGuid(name, material);
        }


        // Тип стены — тоже по адресу файла.
        public static SplineWallType GetWallType(string name)
        {
            var guid = EditorPrefs.GetString(Prefix + name, "");
            return guid.Length > 0 ? AssetDatabase.LoadAssetAtPath<SplineWallType>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }


        public static void SetWallType(string name, SplineWallType wallType)
        {
            SetGuid(name, wallType);
        }


        private static void SetGuid(string name, Object asset)
        {
            var guid = asset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) : "";

            if (EditorPrefs.GetString(Prefix + name, "") != guid)
                EditorPrefs.SetString(Prefix + name, guid);
        }
    }


    /// <summary>Как рисуется комната: прямоугольником, протянутым по полу, или контуром — кликами точек.</summary>
    public enum LiveBuilderRoomShape
    {
        Rectangle,
        Contour
    }


    /// <summary>Настройки инструмента «Комната»: форма, толщина стен и перекрытий, материалы пола, стен и потолка.</summary>
    public static class LiveBuilderRoomSettings
    {
        // Сменилась форма — начатое рисование отменяется, как при смене инструмента.
        public static event System.Action ShapeChanged;


        public static LiveBuilderRoomShape Shape
        {
            get => (LiveBuilderRoomShape)LiveBuilderPreferences.GetInt("Room.Shape", (int)LiveBuilderRoomShape.Rectangle);
            set
            {
                if (Shape == value)
                    return;

                LiveBuilderPreferences.SetInt("Room.Shape", (int)value);
                ShapeChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        // Толщина стен вокруг комнаты в пустоте, м. Внутри дома стены — то, что осталось от его объёма.
        public static float WallThickness
        {
            get => LiveBuilderPreferences.GetFloat("Room.WallThickness", 0.3f);
            set => LiveBuilderPreferences.SetFloat("Room.WallThickness", Mathf.Max(0.05f, value));
        }

        // Толщина пола и потолка, м. Высота комнаты — этаж минус перекрытие: 4 − 0,2 = 3,8.
        public static float SlabThickness
        {
            get => LiveBuilderPreferences.GetFloat("Room.SlabThickness", 0.2f);
            set => LiveBuilderPreferences.SetFloat("Room.SlabThickness", Mathf.Clamp(value, 0.05f, 1f));
        }

        public static Material FloorMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.FloorMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.FloorMaterial", value);
        }

        public static Material WallMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.WallMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.WallMaterial", value);
        }

        public static Material CeilingMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Room.CeilingMaterial");
            set => LiveBuilderPreferences.SetMaterial("Room.CeilingMaterial", value);
        }

        // Высота комнаты от пола до потолка, м.
        public static float ClearHeight => Mathf.Max(0.5f, LiveBuilderStoreys.StoreyHeight - SlabThickness);
    }


    /// <summary>Настройки инструмента «Стена»: размеры новой стены, толщина перегородки в здании и какой будет новая точка — угол или плавно.</summary>
    public static class LiveBuilderWallSettings
    {
        public static float Thickness
        {
            get => LiveBuilderPreferences.GetFloat("Wall.Thickness", 0.3f);
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
            get => LiveBuilderPreferences.GetFloat("Wall.PartitionThickness", 0.15f);
            set => LiveBuilderPreferences.SetFloat("Wall.PartitionThickness", Mathf.Max(0.02f, value));
        }

        public static SplineWallAlignment Alignment
        {
            get => (SplineWallAlignment)LiveBuilderPreferences.GetInt("Wall.Alignment", (int)SplineWallAlignment.Center);
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


    /// <summary>Настройки инструмента «Здание» для нового здания: этажи, стены, перекрытия, материалы. Высота этажа — общая, из этажей окна.</summary>
    public static class LiveBuilderBuildingSettings
    {
        public static int StoreyCount
        {
            get => LiveBuilderPreferences.GetInt("Building.StoreyCount", 1);
            set => LiveBuilderPreferences.SetInt("Building.StoreyCount", Mathf.Clamp(value, 1, SplineBuilding.MaxStoreys));
        }

        public static float WallThickness
        {
            get => LiveBuilderPreferences.GetFloat("Building.WallThickness", 0.3f);
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
            get => (SplineBuildingContourSide)LiveBuilderPreferences.GetInt("Building.ContourSide", (int)SplineBuildingContourSide.Outside);
            set => LiveBuilderPreferences.SetInt("Building.ContourSide", (int)value);
        }

        public static float SlabThickness
        {
            get => LiveBuilderPreferences.GetFloat("Building.SlabThickness", 0.2f);
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


    /// <summary>Форма нового проёма: прямоугольник, арка (верх полукругом) или круг.</summary>
    public enum LiveBuilderCutterShape
    {
        Rectangle,
        Arch,
        Circle
    }


    /// <summary>
    /// Каким будет вырез (<see cref="GeometryCutter"/>), который ставит инструмент «Проём»: форма, насквозь или ниша и её глубина
    /// (у протянутого), материал стенок дыры. Готовые размеры для клика — <see cref="LiveBuilderOpeningPresets"/>.
    /// </summary>
    public static class LiveBuilderCutterSettings
    {
        public static LiveBuilderCutterShape Shape
        {
            get => (LiveBuilderCutterShape)LiveBuilderPreferences.GetInt("Cutter.Shape", (int)LiveBuilderCutterShape.Rectangle);
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
            get => LiveBuilderPreferences.GetFloat("Cutter.NicheDepth", 0.15f);
            set => LiveBuilderPreferences.SetFloat("Cutter.NicheDepth", Mathf.Max(0.01f, value));
        }

        // Материал стенок дыры; пусто — как у того, что режем.
        public static Material RevealMaterial
        {
            get => LiveBuilderPreferences.GetMaterial("Cutter.RevealMaterial");
            set => LiveBuilderPreferences.SetMaterial("Cutter.RevealMaterial", value);
        }
    }


    /// <summary>Настройки инструмента «Лестница»: вид, высота, ширина по умолчанию, пробивать ли перекрытие.</summary>
    public static class LiveBuilderStairsSettings
    {
        public static BoxModelStairsStyle Style
        {
            get => (BoxModelStairsStyle)LiveBuilderPreferences.GetInt("Stairs.Style", (int)BoxModelStairsStyle.SeparateSteps);
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
