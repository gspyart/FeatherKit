using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Проводит пачку префабов через переделку в варианты шаблона: готовит редактор, переделывает
    /// по одному и возвращает редактор как было.
    ///
    /// Ссылки на каждый префаб чинятся СРАЗУ после его сохранения, а не в конце: следующий
    /// в пачке может содержать предыдущий, и открывать его надо уже с исправленными ссылками.
    ///
    /// Всё, что живёт в памяти копией файла, — несохранённые ассеты, открытые сцены, открытый
    /// префаб — до начала сбрасывается на диск: починка идёт по файлам, и копия в памяти при
    /// следующем сохранении молча затёрла бы её.
    /// </summary>
    public static class PrefabToVariantBatch
    {
        private const string ProgressTitle = "Переделываю префабы в варианты";


        /// <summary>
        /// Переделывает префабы в варианты шаблона. Отказ всей пачке — в refusal, тогда ничего
        /// не тронуто; отказы и итоги по каждому префабу — в возвращённом списке.
        /// </summary>
        public static List<PrefabConversionResult> Run(IReadOnlyList<string> prefabGuids, GameObject template, bool removeTemplateExtras, out string refusal)
        {
            var results = new List<PrefabConversionResult>();

            refusal = FindEditorRefusal(template);
            if (refusal != null)
                return results;

            AssetDatabase.SaveAssets();

            var reopenedPrefabPath = LeavePrefabStage(out refusal);
            if (refusal != null || !SaveOpenScenes(out refusal))
            {
                ReopenPrefabStage(reopenedPrefabPath);
                return results;
            }

            var remapper = PrefabReferenceRemapper.Build();
            if (remapper == null)
            {
                refusal = "Отменено: поиск ссылок прерван, ничего не изменено.";
                ReopenPrefabStage(reopenedPrefabPath);
                return results;
            }

            var changedFiles = new HashSet<string>();

            try
            {
                for (var index = 0; index < prefabGuids.Count; index++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                    EditorUtility.DisplayProgressBar(ProgressTitle, Path.GetFileNameWithoutExtension(path), (float)index / prefabGuids.Count);

                    var result = ConvertOne(path, template, removeTemplateExtras, remapper);
                    results.Add(result);
                    changedFiles.UnionWith(result.FixedFiles);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ReloadChangedOpenScenes(changedFiles);
                ReopenPrefabStage(reopenedPrefabPath);
            }

            LogResults(results, template);
            return results;
        }

        private static string FindEditorRefusal(GameObject template)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Сначала выйди из игры.";

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "Дождись, пока Unity закончит компиляцию и импорт.";

            if (!PrefabReferenceRemapper.IsProjectTextSerialized)
                return "Проект хранит ассеты не текстом (Project Settings → Editor → Asset Serialization). " +
                       "Без этого ссылки на переделанные префабы не починить.";

            return PrefabToVariantConverter.CanBeTemplate(template, out var templateRefusal) ? null : templateRefusal;
        }

        // Открытый префаб — такая же копия в памяти, как сцена. Выходим из него, запомнив,
        // какой был открыт, и после пачки открываем обратно.
        private static string LeavePrefabStage(out string refusal)
        {
            refusal = null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return null;

            var path = stage.assetPath;
            StageUtility.GoToMainStage();

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                refusal = "Закрой режим редактирования префаба.";

            return path;
        }

        // «Не сохранять» в диалоге Unity оставляет сцену изменённой — и она затёрла бы
        // починку при следующем сохранении. Такую пачку не начинаем.
        private static bool SaveOpenScenes(out string refusal)
        {
            refusal = null;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                refusal = "Отменено: сцены не сохранены, ничего не изменено.";
                return false;
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (!SceneManager.GetSceneAt(index).isDirty)
                    continue;

                refusal = "Есть несохранённые сцены. Сохрани их — иначе починка ссылок в них пропадёт при следующем сохранении.";
                return false;
            }

            return true;
        }

        private static PrefabConversionResult ConvertOne(string path, GameObject template, bool removeTemplateExtras, PrefabReferenceRemapper remapper)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var guid = AssetDatabase.AssetPathToGUID(path);

            using (var converter = new PrefabToVariantConverter(prefab, template, removeTemplateExtras))
            {
                var result = converter.Convert();
                if (result.Status != PrefabConversionStatus.Ready)
                    return result;

                // Право записи — заранее и на всё: переписанный префаб с неисправленными
                // ссылками на него — худший исход из возможных.
                var affectedFiles = remapper.FindAffectedFiles(guid);
                affectedFiles.Add(path);

                var lockedFiles = MakeWritable(affectedFiles);
                if (lockedFiles.Count > 0)
                {
                    result.Fail("Нельзя записать файлы: " + string.Join(", ", lockedFiles.Take(3)) +
                                (lockedFiles.Count > 3 ? $" и ещё {lockedFiles.Count - 3}" : "") + ". Префаб не тронут.");
                    return result;
                }

                if (!converter.Save())
                    return result;

                remapper.Reindex(path);

                var fixedFiles = remapper.Remap(guid, result.FileIdChanges, out var failedFiles);
                result.AddFixedFiles(fixedFiles);

                foreach (var failedFile in failedFiles)
                    result.AddWarning($"Не удалось исправить ссылки в «{failedFile}» — их придётся поправить руками.");

                return result;
            }
        }

        // Plastic и другие системы контроля версий отдают файлы только на чтение, пока их
        // не взяли в работу. MakeEditable берёт; что осталось запертым — возвращаем.
        private static List<string> MakeWritable(ICollection<string> paths)
        {
            var notEditable = new List<string>();
            AssetDatabase.MakeEditable(paths.ToArray(), null, notEditable);

            return notEditable.Concat(paths.Where(IsReadOnlyOnDisk)).Distinct().ToList();
        }

        private static bool IsReadOnlyOnDisk(string path) =>
            File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;

        // Открытая сцена в памяти не знает о починке на диске. Перечитываем её — все сцены
        // сохранены до начала, так что терять нечего.
        private static void ReloadChangedOpenScenes(HashSet<string> changedFiles)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (!setup.Any(scene => changedFiles.Contains(scene.path)))
                return;

            var restorable = setup.Where(scene => !string.IsNullOrEmpty(scene.path)).ToArray();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (restorable.Length == 0)
                return;

            if (!restorable.Any(scene => scene.isActive))
                restorable[0].isActive = true;

            EditorSceneManager.RestoreSceneManagerSetup(restorable);
        }

        private static void LogResults(List<PrefabConversionResult> results, GameObject template)
        {
            foreach (var result in results)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);

                if (result.Status == PrefabConversionStatus.Converted)
                    Debug.Log($"[Префабы в варианты] «{result.PrefabName}» теперь вариант «{template.name}». " +
                              $"Ссылки исправлены в файлах: {result.FixedFiles.Count}.", asset);
                else
                    Debug.LogWarning($"[Префабы в варианты] «{result.PrefabName}» не переделан: {result.Reason}", asset);

                foreach (var warning in result.Warnings)
                    Debug.LogWarning($"[Префабы в варианты] «{result.PrefabName}»: {warning}", asset);
            }
        }


        // ── Общие помощники ──────────────────────────

        private static void ReopenPrefabStage(string path)
        {
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                PrefabStageUtility.OpenPrefab(path);
        }
    }
}
