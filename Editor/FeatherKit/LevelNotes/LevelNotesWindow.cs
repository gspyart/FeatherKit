using System.Collections.Generic;
using FeatherKit.LevelNotes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно-список всех заметок открытых сцен: цвет типа, первая строка, кто оставил, галка «сделано».
    /// Клик по строке — выделить заметку и навести на неё камеру сцены.
    /// Сверху — фильтр по типам и «скрыть сделанные». Меню — FeatherKit → Level Notes.
    /// </summary>
    public class LevelNotesWindow : EditorWindow
    {
        private const float RowHeight = 22f;
        private const float SwatchWidth = 4f;
        private const float DoneToggleWidth = 18f;
        private const float AuthorWidth = 110f;
        private const float FrameSize = 4f;
        private const string DoneUndoName = "Toggle Level Note Done";
        private const string EmptyListNote = "На открытых сценах заметок нет. Поставить — мышь над окном сцены и N.";
        private const string NothingShownNote = "Все заметки скрыты фильтром.";

        private static readonly Color SelectedRowColor = new(0.24f, 0.48f, 0.9f, 0.35f);
        private static readonly Color SceneHeaderColor = new(0f, 0f, 0f, 0.2f);

        private static GUIStyle titleStyle;
        private static GUIStyle doneTitleStyle;

        [SerializeField] private bool isDoneHidden;
        [SerializeField] private List<LevelNoteKind> hiddenKinds = new();

        private readonly List<LevelNote> notes = new();
        private Vector2 scrollPosition;
        [System.NonSerialized] private bool isRefreshScheduled;


        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };

        private static GUIStyle DoneTitleStyle => doneTitleStyle ??= new GUIStyle(TitleStyle) { normal = { textColor = Color.gray } };


        // ── Открытие ─────────────────────────────────────

        [MenuItem("FeatherKit/Level Notes")]
        public static void Open()
        {
            var window = GetWindow<LevelNotesWindow>();
            window.titleContent = new GUIContent("Level Notes", EditorGUIUtility.ObjectContent(null, typeof(LevelNote)).image);
            window.Show();
        }


        // ── Список ───────────────────────────────────────

        // Сцена по порядку открытия, внутри — сначала несделанные, по номеру в имени.
        private void Refresh()
        {
            notes.Clear();
            notes.AddRange(FindObjectsByType<LevelNote>(FindObjectsInactive.Include));
            notes.RemoveAll(note => EditorUtility.IsPersistent(note) || !note.gameObject.scene.IsValid());
            notes.Sort(CompareNotes);
            Repaint();
        }

        private static int CompareNotes(LevelNote first, LevelNote second)
        {
            var sceneOrder = SceneIndex(first.gameObject.scene).CompareTo(SceneIndex(second.gameObject.scene));

            if (sceneOrder != 0)
                return sceneOrder;

            if (first.IsDone != second.IsDone)
                return first.IsDone ? 1 : -1;

            return EditorUtility.NaturalCompare(first.name, second.name);
        }

        private static int SceneIndex(Scene scene)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index) == scene)
                    return index;
            }

            return int.MaxValue;
        }


        private bool IsShown(LevelNote note)
        {
            return note != null && !(isDoneHidden && note.IsDone) && !hiddenKinds.Contains(note.Kind);
        }


        // ── Фильтр ───────────────────────────────────────

        private void DrawFilter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                foreach (var kind in LevelNoteKindStyle.AllKinds)
                    DrawKindToggle(kind);

                GUILayout.FlexibleSpace();
                isDoneHidden = GUILayout.Toggle(isDoneHidden, "Скрыть сделанные", EditorStyles.toolbarButton);
            }
        }

        private void DrawKindToggle(LevelNoteKind kind)
        {
            var isShown = !hiddenKinds.Contains(kind);
            var previousColor = GUI.contentColor;
            GUI.contentColor = isShown ? LevelNoteKindStyle.ColorOf(kind) : Color.gray;

            var newIsShown = GUILayout.Toggle(isShown, $"● {LevelNoteKindStyle.LabelOf(kind)}", EditorStyles.toolbarButton);
            GUI.contentColor = previousColor;

            if (newIsShown == isShown)
                return;

            if (newIsShown)
                hiddenKinds.Remove(kind);
            else
                hiddenKinds.Add(kind);
        }


        // ── Строки ───────────────────────────────────────

        private void DrawRows()
        {
            if (notes.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyListNote, MessageType.None);
                return;
            }

            var hasManyScenes = SceneManager.sceneCount > 1;
            var shownCount = 0;
            Scene? previousScene = null;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var note in notes)
            {
                if (!IsShown(note))
                    continue;

                if (hasManyScenes && previousScene != note.gameObject.scene)
                    DrawSceneHeader(note.gameObject.scene);

                previousScene = note.gameObject.scene;
                DrawRow(note);
                shownCount++;
            }

            if (shownCount == 0)
                EditorGUILayout.HelpBox(NothingShownNote, MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSceneHeader(Scene scene)
        {
            var headerRect = EditorGUILayout.GetControlRect(false, RowHeight);
            EditorGUI.DrawRect(headerRect, SceneHeaderColor);
            GUI.Label(headerRect, scene.name, EditorStyles.boldLabel);
        }

        private static void DrawRow(LevelNote note)
        {
            var rowRect = EditorGUILayout.GetControlRect(false, RowHeight);

            if (Selection.activeGameObject == note.gameObject)
                EditorGUI.DrawRect(rowRect, SelectedRowColor);

            var swatchRect = new Rect(rowRect.x, rowRect.y + 2f, SwatchWidth, rowRect.height - 4f);
            EditorGUI.DrawRect(swatchRect, LevelNoteKindStyle.ColorOf(note));

            var toggleRect = new Rect(swatchRect.xMax + 4f, rowRect.y, DoneToggleWidth, rowRect.height);
            DrawDoneToggle(toggleRect, note);

            var authorRect = new Rect(rowRect.xMax - AuthorWidth, rowRect.y, AuthorWidth, rowRect.height);
            GUI.Label(authorRect, note.Author, EditorStyles.miniLabel);

            var titleRect = new Rect(toggleRect.xMax + 2f, rowRect.y, authorRect.x - toggleRect.xMax - 6f, rowRect.height);
            var title = new GUIContent(string.IsNullOrEmpty(note.FirstLine) ? $"({note.name})" : note.FirstLine, note.Text);

            if (GUI.Button(titleRect, title, note.IsDone ? DoneTitleStyle : TitleStyle))
                SelectAndFrame(note);
        }

        private static void DrawDoneToggle(Rect toggleRect, LevelNote note)
        {
            var isDone = EditorGUI.Toggle(toggleRect, new GUIContent("", "Сделано"), note.IsDone);

            if (isDone == note.IsDone)
                return;

            Undo.RecordObject(note, DoneUndoName);
            note.IsDone = isDone;
        }

        private static void SelectAndFrame(LevelNote note)
        {
            Selection.activeGameObject = note.gameObject;
            EditorGUIUtility.PingObject(note.gameObject);

            var sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null)
                sceneView.Frame(new Bounds(note.transform.position + Vector3.up, Vector3.one * FrameSize), false);
        }


        // ── Обновление ───────────────────────────────────

        // Правка любого объекта может быть правкой заметки; событий за кадр много — перечитываем один раз, после них.
        private void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            ScheduleRefresh();
        }

        private void ScheduleRefresh()
        {
            if (isRefreshScheduled)
                return;

            isRefreshScheduled = true;

            EditorApplication.delayCall += () =>
            {
                isRefreshScheduled = false;

                if (this != null)
                    Refresh();
            };
        }


        // Вход в игру и выход из неё пересоздают объекты сцен: прежние ссылки на заметки мертвы.
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change is PlayModeStateChange.EnteredEditMode or PlayModeStateChange.EnteredPlayMode)
                Refresh();
        }


        private void OnSceneClosed(Scene scene) => Refresh();

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => Refresh();

        private void OnSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) => Refresh();


        // ── Unity ────────────────────────────────────────

        private void OnEnable()
        {
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            Undo.undoRedoPerformed += Refresh;
            Selection.selectionChanged += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.newSceneCreated += OnSceneCreated;
            Refresh();
        }

        private void OnDisable()
        {
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            Undo.undoRedoPerformed -= Refresh;
            Selection.selectionChanged -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.newSceneCreated -= OnSceneCreated;
        }

        private void OnFocus()
        {
            Refresh();
        }

        // Мёртвая ссылка — заметку удалили или сцену пересоздали: перечитать, а не просто выкинуть.
        private void OnGUI()
        {
            if (notes.Exists(note => note == null))
                Refresh();

            DrawFilter();
            DrawRows();
        }
    }
}
