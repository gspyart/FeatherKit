using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Раздел «Materials» окна уровня — материалы из настроек проекта (<see cref="LiveBuilderProjectSettings"/>), правка сразу пишется на диск:
    /// - материал новых построек и кнопка палитры уровня (FeatherKit Level Palette), откуда материал тянут на грань
    /// - таблица плиток: материал → метров на один повтор текстуры по U и V (<see cref="LiveBuilderMaterialTiling"/>)
    /// </summary>
    public static class LiveBuilderMaterialSettingsDrawer
    {
        private const float MetersFieldWidth = 38f;
        private const float RemoveButtonWidth = 20f;

        private const string LevelPaletteMenu = "FeatherKit/Level Palette";


        public static void Draw()
        {
            var settings = LiveBuilderProjectSettings.instance;

            // Одиночка настроек создаётся только для чтения — иначе поля не правятся.
            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;

            var serializedSettings = new SerializedObject(settings);
            DrawLevelPaletteButton();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(serializedSettings.FindProperty("defaultMaterial"),
                new GUIContent("New Builds", "Материал граней у новых объектов из коробок, стен и зданий. Пусто — серый материал FeatherKit"));

            EditorGUILayout.Space(4f);
            DrawTileSizes(serializedSettings.FindProperty("materialTileSizes"));

            if (!EditorGUI.EndChangeCheck())
                return;

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveChanges();
            SceneView.RepaintAll();
        }


        // Палитра уровня — модуль FeatherKit без своей сборки у Live Builder: открываем её пунктом меню.
        private static void DrawLevelPaletteButton()
        {
#if FEATHERKIT_PROBUILDER
            if (GUILayout.Button(new GUIContent("Open Level Palette", "Палитра материалов и префабов уровня: материал из неё тянут мышью на грань постройки")))
                EditorApplication.ExecuteMenuItem(LevelPaletteMenu);
#endif
        }


        // ── Плитки ───────────────────────────────────────────────────────

        // Строка — материал, U × V м, «×» убрать; ниже — добавить строку. Нет в таблице — 1 × 1 м.
        private static void DrawTileSizes(SerializedProperty rows)
        {
            GUILayout.Label(new GUIContent("Tile Size, m (U × V)",
                "Сколько метров на один повтор текстуры у материала. Нет в списке — 1 × 1 м. " +
                "Фасадная панель 3 × 3 — текстура ложится ровно по панели при шаге сетки 3 м"), EditorStyles.miniBoldLabel);

            for (var index = 0; index < rows.arraySize; index++)
                if (DrawTileSizeRow(rows.GetArrayElementAtIndex(index)))
                {
                    rows.DeleteArrayElementAtIndex(index);
                    GUI.changed = true;
                    break;
                }

            if (!GUILayout.Button(new GUIContent("+ Add Material", "Новая строка таблицы: выбери материал и его размер плитки")))
                return;

            rows.arraySize++;
            var added = rows.GetArrayElementAtIndex(rows.arraySize - 1);
            added.FindPropertyRelative("material").objectReferenceValue = null;
            added.FindPropertyRelative("metersPerTile").vector2Value = LiveBuilderMaterialTiling.DefaultMetersPerTile;
            GUI.changed = true;
        }

        // Возвращает, нажали ли «×».
        private static bool DrawTileSizeRow(SerializedProperty row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var material = row.FindPropertyRelative("material");
                EditorGUILayout.PropertyField(material, GUIContent.none, GUILayout.MinWidth(70f));

                var metersPerTile = row.FindPropertyRelative("metersPerTile");
                var size = metersPerTile.vector2Value;
                size.x = EditorGUILayout.FloatField(new GUIContent("", "Метров на повтор по U"), size.x, GUILayout.Width(MetersFieldWidth));
                GUILayout.Label("×", EditorStyles.miniLabel, GUILayout.Width(10f));
                size.y = EditorGUILayout.FloatField(new GUIContent("", "Метров на повтор по V"), size.y, GUILayout.Width(MetersFieldWidth));
                metersPerTile.vector2Value = size;

                return GUILayout.Button(new GUIContent("×", "Убрать строку: материал снова 1 × 1 м"), EditorStyles.miniButton, GUILayout.Width(RemoveButtonWidth));
            }
        }
    }
}
