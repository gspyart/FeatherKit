using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ищет, где в проекте используется ассет: показывает список сцен, префабов, материалов
    /// и конфигов, которые на него ссылаются.
    ///
    /// Ищем подстроку GUID в тексте ассетов, а не обходим AssetDatabase.GetDependencies по
    /// каждому файлу: зависимости пришлось бы запрашивать тысячи раз (с покупными паками это
    /// десятки секунд), а в проекте включена текстовая сериализация, поэтому ссылка — это
    /// просто guid в YAML. Побочная польза: находятся и битые ссылки на удалённые объекты,
    /// которых в списке зависимостей уже нет.
    /// </summary>
    public class AssetUsageFinder : EditorWindow
    {
        // Ссылки живут только в этих типах. Текстуры, модели и звуки сами ни на кого
        // не ссылаются — гонять по ним поиск бессмысленно и долго.
        private static readonly string[] SearchableExtensions =
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim",
            ".overrideController", ".playable", ".spriteatlas", ".shadervariants", ".guiskin"
        };

        private Object target;
        private bool includeThirdParty;

        private readonly List<string> results = new List<string>();
        private Vector2 scroll;
        private string status = "Выбери ассет и нажми «Найти».";
        private int scannedCount;
        private long elapsedMs;
        private bool searched;


        [MenuItem("FeatherKit/Asset Usage Finder")]
        private static void Open()
        {
            GetWindow<AssetUsageFinder>("Где используется").minSize = new Vector2(420f, 320f);
        }

        // Дублируем в контекстное меню Project: искать удобнее прямо с правого клика по ассету.
        [MenuItem("Assets/Найти использования", false, 30)]
        private static void FindForSelected()
        {
            var window = GetWindow<AssetUsageFinder>("Где используется");
            window.target = Selection.activeObject;
            window.Search();
        }

        [MenuItem("Assets/Найти использования", true)]
        private static bool FindForSelectedValidate()
        {
            return Selection.activeObject != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
        }


        private void OnGUI()
        {
            EditorGUILayout.Space(4f);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                target = EditorGUILayout.ObjectField("Ассет", target, typeof(Object), false);
                if (check.changed)
                {
                    results.Clear();
                    searched = false;
                    status = "Нажми «Найти».";
                }
            }

            includeThirdParty = EditorGUILayout.ToggleLeft(
                "Искать и в ThirdParty (медленнее)", includeThirdParty);

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Найти", GUILayout.Height(26f)))
                    Search();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(status, MessageType.None);

            if (searched && results.Count == 0)
            {
                // Про Resources предупреждаем отдельно: оттуда грузят по строковому пути,
                // ссылки в ассетах нет, и «0 использований» не значит «можно удалять».
                EditorGUILayout.HelpBox(
                    "Ссылок не найдено. Это ещё не значит, что ассет не нужен: из папки Resources " +
                    "грузят через Resources.Load по имени, а из кода — через строковые пути. " +
                    "Такие обращения поиск по GUID не видит.",
                    MessageType.Warning);
            }

            DrawResults();
        }

        private void DrawResults()
        {
            if (results.Count == 0)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var path in results)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    var icon = AssetDatabase.GetCachedIcon(path);

                    if (GUILayout.Button(new GUIContent(" " + path, icon), EditorStyles.label))
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }


        private void Search()
        {
            results.Clear();
            searched = false;
            scannedCount = 0;

            var targetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(targetPath))
            {
                status = "У этого объекта нет файла на диске — искать нечего.";
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(guid))
            {
                status = "Не удалось получить GUID ассета.";
                return;
            }

            var timer = Stopwatch.StartNew();
            var candidates = CollectCandidates(targetPath);

            try
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    var path = candidates[i];

                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(
                            "Поиск использований", path, (float)i / candidates.Count))
                        break;

                    scannedCount++;

                    if (ContainsGuid(path, guid))
                        results.Add(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            timer.Stop();
            elapsedMs = timer.ElapsedMilliseconds;
            searched = true;

            status = results.Count == 0
                ? $"Использований не найдено. Просмотрено файлов: {scannedCount} за {elapsedMs} мс."
                : $"Найдено использований: {results.Count}. Просмотрено файлов: {scannedCount} за {elapsedMs} мс.";
        }

        // Свои — это проект и свои же пакеты (встроенные вроде FeatherKit). Пакеты Unity
        // пропускаем: на ассеты проекта они не ссылаются, а весят сотни файлов.
        private static bool IsOurs(string path)
        {
            if (path.StartsWith("Assets/"))
                return true;

            return path.StartsWith("Packages/") && !path.StartsWith("Packages/com.unity.");
        }

        private List<string> CollectCandidates(string targetPath)
        {
            var candidates = new List<string>();

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!IsOurs(path))
                    continue;

                // Сам себя ассет упоминает всегда — это не использование.
                if (path == targetPath)
                    continue;

                if (!includeThirdParty && path.StartsWith("Assets/ThirdParty/"))
                    continue;

                if (!IsSearchable(path))
                    continue;

                candidates.Add(path);
            }

            return candidates;
        }

        private static bool IsSearchable(string path)
        {
            var extension = Path.GetExtension(path);

            foreach (var searchable in SearchableExtensions)
            {
                if (string.Equals(extension, searchable, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsGuid(string path, string guid)
        {
            try
            {
                return File.ReadAllText(path).Contains(guid);
            }
            catch (IOException)
            {
                // Файл может быть занят или недоступен — не повод валить весь поиск.
                return false;
            }
        }
    }
}
