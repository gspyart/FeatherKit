using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Какой префаб выбран в палитре уровня сейчас: палитра пишет, инструменты читают (Module в Live Builder).
    /// Своя сборка — палитра требует ProBuilder, а читающим ссылаться на неё нельзя. Помнится до перезапуска редактора.
    /// </summary>
    public static class LevelPaletteActiveAsset
    {
        private const string SessionKey = "FeatherKit.LevelPalette.ActivePrefab";


        // Выбрали другой префаб — окна, которые его показывают, перерисовываются.
        public static event Action Changed;


        // Префаб-ассет; не префаб и не ассет — не запоминается.
        public static GameObject Prefab
        {
            get
            {
                var path = AssetDatabase.GUIDToAssetPath(SessionState.GetString(SessionKey, ""));
                return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            set
            {
                var guid = value != null && PrefabUtility.IsPartOfPrefabAsset(value)
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))
                    : "";

                if (guid == SessionState.GetString(SessionKey, ""))
                    return;

                SessionState.SetString(SessionKey, guid);
                Changed?.Invoke();
            }
        }
    }
}
