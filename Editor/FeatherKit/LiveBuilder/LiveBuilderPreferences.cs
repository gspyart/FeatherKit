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


        // Вариант из списка, хранится числом. Число не из списка (старая версия, правка руками) — fallback.
        public static TEnum GetEnum<TEnum>(string name, TEnum fallback) where TEnum : struct, System.Enum
        {
            var stored = EditorPrefs.GetInt(Prefix + name, System.Convert.ToInt32(fallback));
            return System.Enum.IsDefined(typeof(TEnum), stored) ? (TEnum)System.Enum.ToObject(typeof(TEnum), stored) : fallback;
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
}
