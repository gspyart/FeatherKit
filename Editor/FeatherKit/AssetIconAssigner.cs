using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ставит ассетам свои иконки: выделил в Project один файл или сразу несколько,
    /// выбрал картинку — применил. Он же показывает, что на выделенном стоит сейчас,
    /// и умеет иконку снять.
    /// Иконка ложится в .meta ассета, поэтому видна и в Project, и в тех полях
    /// инспектора, куда этот ассет кладут.
    /// </summary>
    public class AssetIconAssigner : EditorWindow
    {
        private const string Title = "Иконки ассетов";

        private const float ButtonHeight = 26f;

        private Texture2D icon;

        // Окно следит за выделением в Project. Закреплённое — не следит: иначе клик
        // по картинке-иконке в Project сбрасывал бы список, который для неё и набирали.
        private bool locked;

        private string status = "Выдели ассеты в Project.";

        private readonly List<Object> targets = new List<Object>();
        private Vector2 scroll;


        [MenuItem("FeatherKit/Asset Icons")]
        private static void Open()
        {
            GetWindow<AssetIconAssigner>(Title).minSize = new Vector2(320f, 260f);
        }

        // Дублируем в контекстное меню Project: ставить иконку удобнее прямо с правого клика.
        [MenuItem("Assets/Назначить иконку", false, 31)]
        private static void OpenForSelected()
        {
            var window = GetWindow<AssetIconAssigner>(Title);

            window.locked = false;
            window.CollectTargets();
        }

        [MenuItem("Assets/Назначить иконку", true)]
        private static bool OpenForSelectedValidate()
        {
            return Selection.activeObject != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
        }


        // ── Иконки ────────────────────────────────────────────────────────

        // Переимпорт обязателен: иконка живёт в .meta, а его переписывает импортёр —
        // без этого правка осталась бы в памяти редактора и пропала при перезапуске.
        private void Apply(Texture2D applied)
        {
            foreach (var target in targets)
            {
                EditorGUIUtility.SetIconForObject(target, applied);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(target), ImportAssetOptions.ForceUpdate);
            }

            EditorApplication.RepaintProjectWindow();

            status = applied == null
                ? $"Иконка снята: {targets.Count}."
                : $"Иконка поставлена: {targets.Count}.";
        }

        // Берём только файлы: иконку ставят ассету, а объекту сцены её ставить некуда.
        // Папки пропускаем — своей иконки у них не бывает.
        private void CollectTargets()
        {
            targets.Clear();

            foreach (var selected in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(selected);

                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;

                targets.Add(selected);
            }

            icon = SharedIcon(targets);

            status = targets.Count == 0
                ? "Выдели ассеты в Project."
                : $"Выбрано ассетов: {targets.Count}.";
        }

        // Что стоит на выделенном сейчас. У разных иконка одна на всех — показываем её,
        // разные — поле остаётся пустым: показать одну значило бы соврать про остальные.
        private static Texture2D SharedIcon(List<Object> assets)
        {
            if (assets.Count == 0)
                return null;

            var first = EditorGUIUtility.GetIconForObject(assets[0]);

            for (var i = 1; i < assets.Count; i++)
            {
                if (EditorGUIUtility.GetIconForObject(assets[i]) != first)
                    return null;
            }

            return first;
        }


        // ── Отрисовка ─────────────────────────────────────────────────────

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(status);

                locked = GUILayout.Toggle(
                    locked,
                    new GUIContent("Закрепить", "Не менять список при смене выделения в Project"),
                    EditorStyles.miniButton,
                    GUILayout.Width(90f));
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(icon == null))
                {
                    if (GUILayout.Button("Применить", GUILayout.Height(ButtonHeight)))
                        Apply(icon);
                }

                if (GUILayout.Button("Убрать иконку", GUILayout.Height(ButtonHeight)))
                    Apply(null);
            }
        }

        private void DrawTargets()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var target in targets)
            {
                var path = AssetDatabase.GetAssetPath(target);
                var current = EditorGUIUtility.GetIconForObject(target);
                var label = new GUIContent(
                    " " + target.name,
                    current != null ? current : AssetDatabase.GetCachedIcon(path));

                if (GUILayout.Button(label, EditorStyles.label))
                    EditorGUIUtility.PingObject(target);
            }

            EditorGUILayout.EndScrollView();
        }


        // ── Unity-события ─────────────────────────────────────────────────

        private void OnEnable()
        {
            CollectTargets();
        }

        private void OnSelectionChange()
        {
            if (locked)
                return;

            CollectTargets();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);

            DrawHeader();

            icon = (Texture2D)EditorGUILayout.ObjectField("Иконка", icon, typeof(Texture2D), false);

            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                DrawButtons();
            }

            EditorGUILayout.Space(4f);

            DrawTargets();
        }
    }
}
