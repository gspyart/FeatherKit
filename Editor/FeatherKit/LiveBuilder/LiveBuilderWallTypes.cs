using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Типы стен (<see cref="SplineWallType"/>) для окна Live Builder: список всех типов проекта выпадающим полем,
    /// «Без типа» и «Новый тип…» — файл в папке типов. Список типов запомнен: окно перерисовывается на каждое движение мыши,
    /// а поиск по проекту небыстрый. Забывается, когда в проекте что-то поменялось.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderWallTypes
    {
        // Папка типов, если в настройках проекта — путь не внутри Assets.
        private const string FallbackFolder = "Assets/Live Builder Wall Types";

        // Все типы проекта, по имени; null — искать заново.
        private static List<SplineWallType> cachedTypes;


        static LiveBuilderWallTypes()
        {
            EditorApplication.projectChanged += ForgetTypes;
        }


        private static LiveBuilderProjectSettings Settings => LiveBuilderProjectSettings.instance;


        // Выпадающий список: «Без типа», все типы, «Новый тип…». Возвращает выбранный.
        public static SplineWallType Popup(GUIContent label, SplineWallType current)
        {
            var types = AllTypes();
            var names = new List<string> { "Без типа — свои числа" };

            foreach (var type in types)
                names.Add(type.name + " · " + LiveBuilderSceneText.FormatMeters(type.Thickness) + " м");

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


        // Все типы проекта, по имени. Копия запомненного списка — его можно менять.
        public static List<SplineWallType> AllTypes()
        {
            cachedTypes ??= FindTypes();
            cachedTypes.RemoveAll(type => type == null);
            return new List<SplineWallType>(cachedTypes);
        }

        private static List<SplineWallType> FindTypes()
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


        // В проекте добавили, удалили или переименовали файлы — список типов ищется заново.
        private static void ForgetTypes()
        {
            cachedTypes = null;
        }


        // Файл типа в папке типов; новый, заданный человеком, — сразу выделен, чтобы назвать и настроить.
        private static SplineWallType CreateType(string name, float thickness, Material material, bool isSelecting)
        {
            var folder = TypesFolder();
            EnsureFolder(folder);

            var type = ScriptableObject.CreateInstance<SplineWallType>();
            type.Thickness = thickness;
            type.Material = material;

            AssetDatabase.CreateAsset(type, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset"));
            AssetDatabase.SaveAssets();
            ForgetTypes();

            if (isSelecting)
                EditorGUIUtility.PingObject(type);

            return type;
        }

        // Папка из настроек проекта; не внутри Assets — запасная, с предупреждением: файл вне Assets не создать.
        private static string TypesFolder()
        {
            var folder = Settings.WallTypesFolder.Replace('\\', '/');

            if (folder == "Assets" || folder.StartsWith("Assets/"))
                return folder;

            Debug.LogWarning("Live Builder: папка типов стен «" + folder + "» — не внутри Assets. Тип создан в «" + FallbackFolder + "».");
            return FallbackFolder;
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
