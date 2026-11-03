using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Типы стен (<see cref="SplineWallType"/>) для окна Live Builder: список всех типов проекта выпадающим полем,
    /// «Без типа» и «Новый тип…» — файл в папке типов.
    /// </summary>
    public static class LiveBuilderWallTypes
    {
        private static LiveBuilderProjectSettings Settings => LiveBuilderProjectSettings.instance;


        // Выпадающий список: «Без типа», все типы, «Новый тип…». Возвращает выбранный.
        public static SplineWallType Popup(GUIContent label, SplineWallType current)
        {
            var types = AllTypes();
            var names = new List<string> { "Без типа — свои числа" };

            foreach (var type in types)
                names.Add(type.name + " · " + BoxModelSceneText.FormatMeters(type.Thickness) + " м");

            names.Add("Новый тип…");

            var index = current != null ? types.IndexOf(current) + 1 : 0;
            var chosen = EditorGUILayout.Popup(label, index, names.ToArray());

            if (chosen == index)
                return current;

            if (chosen == 0)
                return null;

            if (chosen == names.Count - 1)
                return CreateType("Wall Type", current != null ? current.Thickness : 0.3f, current != null ? current.Material : null, true);

            return types[chosen - 1];
        }


        // Новый тип по умолчанию — наружная 0,3; файл подсвечен в проекте, чтобы назвать.
        public static SplineWallType CreateNewType()
        {
            return CreateType("Wall Type", 0.3f, null, true);
        }


        // Три типа набора: наружная, перегородка, тонкая перегородка. Материалы — из настроек проекта.
        public static void CreateStarterTypes()
        {
            CreateType("Exterior Wall", 0.3f, Settings.ExteriorWallMaterial, false);
            CreateType("Partition", 0.15f, Settings.PartitionMaterial, false);
            CreateType("Thin Partition", 0.1f, Settings.PartitionMaterial, false);
        }


        // Все типы проекта, по имени.
        public static List<SplineWallType> AllTypes()
        {
            var types = new List<SplineWallType>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(SplineWallType)))
            {
                var type = AssetDatabase.LoadAssetAtPath<SplineWallType>(AssetDatabase.GUIDToAssetPath(guid));

                if (type != null)
                    types.Add(type);
            }

            types.Sort((first, second) => string.CompareOrdinal(first.name, second.name));
            return types;
        }


        // Файл типа в папке типов; новый, заданный человеком, — сразу выделен, чтобы назвать и настроить.
        private static SplineWallType CreateType(string name, float thickness, Material material, bool isSelecting)
        {
            EnsureFolder(Settings.WallTypesFolder);

            var type = ScriptableObject.CreateInstance<SplineWallType>();
            type.Thickness = thickness;
            type.Material = material;

            AssetDatabase.CreateAsset(type, AssetDatabase.GenerateUniqueAssetPath(Settings.WallTypesFolder + "/" + name + ".asset"));
            AssetDatabase.SaveAssets();

            if (isSelecting)
                EditorGUIUtility.PingObject(type);

            return type;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
