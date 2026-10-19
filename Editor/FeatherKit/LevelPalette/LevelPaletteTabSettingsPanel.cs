using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Настройки открытой вкладки — раскрываются в окне палитры по кнопке ⚙.
    /// - имя и вид ассетов
    /// - папки-источники: добавить перетаскиванием в поле, убрать крестиком; брать ли подпапки
    /// - ручные ассеты: сколько их и очистить разом
    /// - удалить вкладку
    /// Правит вкладку только через <see cref="LevelPaletteStorage"/>: там отмена и сохранение.
    /// Правка, что меняет число строк, обрывает кадр (ExitGUI): иначе остаток кадра рисовался бы по старой раскладке.
    /// </summary>
    public static class LevelPaletteTabSettingsPanel
    {
        private const float RemoveButtonWidth = 22f;


        private static LevelPaletteStorage Storage => LevelPaletteStorage.instance;


        public static void Draw(LevelPaletteTab tab)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawNameAndKind(tab);
                EditorGUILayout.Space(4f);
                DrawSourceFolders(tab);
                EditorGUILayout.Space(4f);
                DrawManualAssets(tab);
                EditorGUILayout.Space(4f);
                DrawRemoveTab(tab);
            }
        }

        private static void DrawNameAndKind(LevelPaletteTab tab)
        {
            var name = EditorGUILayout.DelayedTextField("Имя вкладки", tab.Name);

            if (name != tab.Name && !string.IsNullOrWhiteSpace(name))
                Storage.EditTab(tab, "Переименовать вкладку палитры", changed => changed.Name = name.Trim());

            var kind = (PaletteAssetKind)EditorGUILayout.Popup(new GUIContent("Что в ней",
                "Из папок берутся только ассеты этого вида. Текстуры: клик создаёт материал, если его ещё нет"),
                (int)tab.AssetKind, PaletteAssetKindRules.DisplayNames);

            if (kind != tab.AssetKind)
                Storage.EditTab(tab, "Вид ассетов вкладки палитры", changed => changed.AssetKind = kind);
        }

        private static void DrawSourceFolders(LevelPaletteTab tab)
        {
            EditorGUILayout.LabelField("Папки-источники", EditorStyles.boldLabel);

            foreach (var guid in tab.SourceFolderGuids.ToList())
                DrawSourceFolderRow(tab, guid);

            var added = EditorGUILayout.ObjectField(new GUIContent("Добавить папку", "Перетащи папку из Project"),
                null, typeof(DefaultAsset), false);

            if (added != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(added)))
            {
                Storage.AddSourceFolder(tab, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(added)));
                GUIUtility.ExitGUI();
            }

            var includeSubfolders = EditorGUILayout.Toggle("Из подпапок тоже", tab.IncludeSubfolders);

            if (includeSubfolders != tab.IncludeSubfolders)
                Storage.EditTab(tab, "Подпапки вкладки палитры", changed => changed.IncludeSubfolders = includeSubfolders);
        }

        // Папку удалили или переименовали снаружи — строка остаётся с пометкой, пока её не уберут.
        private static void DrawSourceFolderRow(LevelPaletteTab tab, string guid)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var label = AssetDatabase.IsValidFolder(path) ? path : "(папка не найдена)";

                EditorGUILayout.LabelField(new GUIContent(label, EditorGUIUtility.IconContent("Folder Icon").image), EditorStyles.miniLabel);

                if (GUILayout.Button(new GUIContent("×", "Убрать папку из вкладки"), GUILayout.Width(RemoveButtonWidth)))
                {
                    Storage.RemoveSourceFolder(tab, guid);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void DrawManualAssets(LevelPaletteTab tab)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Добавлено вручную: {tab.ManualAssetGuids.Count}");

                using (new EditorGUI.DisabledScope(tab.ManualAssetGuids.Count == 0))
                {
                    if (GUILayout.Button("Очистить", GUILayout.Width(80f)))
                        Storage.EditTab(tab, "Очистить ручные ассеты палитры", changed => changed.ManualAssetGuids.Clear());
                }
            }
        }

        private static void DrawRemoveTab(LevelPaletteTab tab)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Удалить вкладку", GUILayout.Width(120f))
                    && EditorUtility.DisplayDialog("Палитра", $"Удалить вкладку «{tab.Name}»? Ассеты в проекте не пострадают.",
                        "Удалить", "Отмена"))
                {
                    Storage.RemoveTab(tab);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
