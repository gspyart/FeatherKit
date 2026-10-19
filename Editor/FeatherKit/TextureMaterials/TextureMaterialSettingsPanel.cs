using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Поля настроек создания материалов: куда класть, образец, что пропускать, импорт текстур.
    /// Только рисует и сохраняет — что пересчитать после правки, решает окно по ответу <see cref="Draw"/>.
    /// </summary>
    public static class TextureMaterialSettingsPanel
    {
        private static readonly int[] TextureSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
        private static readonly string[] TextureSizeLabels = { "32", "64", "128", "256", "512", "1024", "2048", "4096" };


        /// <summary>Нарисовать поля. true — что-то поменяли, план пора пересобрать.</summary>
        public static bool Draw(TextureMaterialSettings settings)
        {
            EditorGUI.BeginChangeCheck();

            DrawOutput(settings);
            EditorGUILayout.Space();
            DrawTemplate(settings);
            EditorGUILayout.Space();
            DrawSkipping(settings);
            EditorGUILayout.Space();
            DrawTextureImport(settings);

            if (!EditorGUI.EndChangeCheck())
                return false;

            settings.Save();

            return true;
        }

        private static void DrawOutput(TextureMaterialSettings settings)
        {
            EditorGUILayout.LabelField("Куда класть", EditorStyles.boldLabel);

            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(settings.OutputFolder);
            var newFolder = EditorGUILayout.ObjectField(new GUIContent("Папка материалов",
                "Внутри появится папка с именем пака или папки текстур"), folder, typeof(DefaultAsset), false);

            if (newFolder != folder && newFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(newFolder)))
                settings.OutputFolder = AssetDatabase.GetAssetPath(newFolder);

            settings.RepeatSubfolders = EditorGUILayout.Toggle(new GUIContent("Повторять подпапки",
                "Pack/Walls/brick.png → Materials/Pack/Walls/brick.mat. Выключено — все материалы пака в одной папке"),
                settings.RepeatSubfolders);
        }

        private static void DrawTemplate(TextureMaterialSettings settings)
        {
            EditorGUILayout.LabelField("Образец", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(settings.TemplateMaterial != null))
            {
                settings.Shader = (Shader)EditorGUILayout.ObjectField(new GUIContent("Шейдер",
                    "Шейдер новых материалов, когда образца нет. Пусто — URP Lit"),
                    settings.Shader, typeof(Shader), false);
            }

            settings.TemplateMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Материал-образец",
                "Новые материалы копируют его настройки и меняют только текстуру. Пусто — чистый материал на шейдере выше.\n" +
                "Дубликатом считается материал на том же шейдере: родные материалы пака на других шейдерах не мешают"),
                settings.TemplateMaterial, typeof(Material), false);
        }

        private static void DrawSkipping(TextureMaterialSettings settings)
        {
            EditorGUILayout.LabelField("Что пропускать", EditorStyles.boldLabel);

            settings.SkipNormalMaps = EditorGUILayout.Toggle(new GUIContent("Карты рельефа",
                "Текстуры, импортированные как Normal map, — не цвет поверхности"), settings.SkipNormalMaps);

            settings.SkippedNameEndingsText = EditorGUILayout.DelayedTextField(new GUIContent("Окончания имён",
                "Служебные карты по концу имени, через запятую: brick_rough, brick_ao — не станут материалами"),
                settings.SkippedNameEndingsText);
        }

        private static void DrawTextureImport(TextureMaterialSettings settings)
        {
            EditorGUILayout.LabelField("Заодно поправить импорт текстур", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                settings.ChangeTextureFilter = EditorGUILayout.Toggle(new GUIContent("Фильтр",
                    "Point — резкие пиксели, как в старых играх"), settings.ChangeTextureFilter);

                using (new EditorGUI.DisabledScope(!settings.ChangeTextureFilter))
                    settings.TextureFilter = (FilterMode)EditorGUILayout.EnumPopup(settings.TextureFilter);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                settings.LimitTextureSize = EditorGUILayout.Toggle(new GUIContent("Наибольший размер",
                    "Большие текстуры ужмутся при импорте, маленькие останутся как есть"), settings.LimitTextureSize);

                using (new EditorGUI.DisabledScope(!settings.LimitTextureSize))
                    settings.MaxTextureSize = EditorGUILayout.IntPopup(settings.MaxTextureSize, TextureSizeLabels, TextureSizes);
            }
        }
    }
}
