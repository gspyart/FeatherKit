using System;
using FeatherKit.LevelNotes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ставит заметки на уровень.
    /// - клавиша N над окном сцены: точка на поверхности под курсором, в сцене того, на что навели; поле текста у курсора (<see cref="LevelNoteTextPopup"/>)
    /// - <see cref="CreateNote"/> — заметка одной строкой кода на активной сцене, для агентов через Unity MCP
    /// - новая заметка — объект «Note N» с тегом EditorOnly в группе из <see cref="LevelNotesProjectSettings"/>; всё отменяется Ctrl+Z
    /// </summary>
    [InitializeOnLoad]
    public static class LevelNotePlacer
    {
        private const string NoteNamePrefix = "Note ";
        private const string EditorOnlyTag = "EditorOnly";
        private const string CreateUndoName = "Create Level Note";

        // Под курсором ни поверхности, ни земли ближе этого — заметка встаёт на столько перед камерой, м.
        private const float MaxGroundDistance = 500f;
        private const float InFrontOfCameraDistance = 10f;

        // Поле текста встаёт чуть в стороне от курсора — чтобы не закрывать призрак флажка.
        private static readonly Vector2 PopupOffsetFromMouse = new(16f, 16f);

        private static SceneView mouseSceneView;
        private static Vector2 mousePositionInSceneView;
        private static SceneView requestedSceneView;
        private static LevelNoteDraft shownDraft;
        private static LevelNoteKind lastUsedKind;


        static LevelNotePlacer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }


        // Кто оставил заметку клавишей: имя в Unity, без входа — имя пользователя компьютера.
        private static string PersonName
        {
            get
            {
                var unityName = CloudProjectSettings.userName;
                return string.IsNullOrEmpty(unityName) || unityName == "anonymous" ? Environment.UserName : unityName;
            }
        }


        // ── Заметка из кода ──────────────────────────────

        /// <summary>Заметка в точке мира на активной сцене. Для агентов: зовётся через Unity MCP, отменяется Ctrl+Z.</summary>
        public static LevelNote CreateNote(Vector3 point, string text, LevelNoteKind kind, string author)
        {
            return CreateNoteInScene(SceneManager.GetActiveScene(), point, text, kind, author);
        }

        private static LevelNote CreateNoteInScene(Scene scene, Vector3 point, string text, LevelNoteKind kind, string author)
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(CreateUndoName);

            var group = FindOrCreateGroup(scene, LevelNotesProjectSettings.instance.NewNotesGroup);

            var noteObject = new GameObject(NextNoteName(scene)) { tag = EditorOnlyTag };

            if (noteObject.scene != scene)
                SceneManager.MoveGameObjectToScene(noteObject, scene);

            noteObject.transform.SetParent(group, false);
            noteObject.transform.position = point;

            var note = noteObject.AddComponent<LevelNote>();
            note.Text = text;
            note.Kind = kind;
            note.Author = author;

            Undo.RegisterCreatedObjectUndo(noteObject, CreateUndoName);
            Undo.CollapseUndoOperations(undoGroup);
            return note;
        }

        // Путь «Level/Debug/Notes» от корня сцены; недостающие группы создаются. Пустой путь — корень сцены.
        private static Transform FindOrCreateGroup(Scene scene, string groupPath)
        {
            if (groupPath.Length == 0)
                return null;

            Transform group = null;

            foreach (var groupName in groupPath.Split('/'))
            {
                var child = group != null ? group.Find(groupName) : FindRoot(scene, groupName);

                if (child == null)
                    child = CreateGroup(scene, groupName, group);

                group = child;
            }

            return group;
        }

        private static Transform FindRoot(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                    return root.transform;
            }

            return null;
        }

        private static Transform CreateGroup(Scene scene, string groupName, Transform parent)
        {
            var groupObject = new GameObject(groupName);

            if (groupObject.scene != scene)
                SceneManager.MoveGameObjectToScene(groupObject, scene);

            groupObject.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(groupObject, CreateUndoName);
            return groupObject.transform;
        }

        // Номер — следующий за самым большим на сцене: удалённые номера не занимаются заново.
        private static string NextNoteName(Scene scene)
        {
            var highestNumber = 0;

            foreach (var note in Object.FindObjectsByType<LevelNote>(FindObjectsInactive.Include))
            {
                if (note.gameObject.scene != scene || !note.name.StartsWith(NoteNamePrefix, StringComparison.Ordinal))
                    continue;

                if (int.TryParse(note.name.Substring(NoteNamePrefix.Length), out var number))
                    highestNumber = Mathf.Max(highestNumber, number);
            }

            return NoteNamePrefix + (highestNumber + 1);
        }


        // ── Клавиша N ────────────────────────────────────

        // Срабатывает в любом окне, если мышь над окном сцены: точку ищем уже внутри его отрисовки — там готова его камера.
        [Shortcut("FeatherKit/Add Level Note", KeyCode.N)]
        private static void RequestNoteUnderMouse()
        {
            if (EditorWindow.mouseOverWindow is not SceneView sceneView)
                return;

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                sceneView.ShowNotification(new GUIContent("Заметки ставятся на сцену, а не в префаб"));
                return;
            }

            requestedSceneView = sceneView;
            sceneView.Repaint();
        }


        private static void OnSceneGUI(SceneView sceneView)
        {
            var currentEvent = Event.current;

            if (currentEvent.type is EventType.MouseMove or EventType.MouseDrag or EventType.MouseDown)
            {
                mouseSceneView = sceneView;
                mousePositionInSceneView = currentEvent.mousePosition;
            }

            if (requestedSceneView == sceneView)
            {
                requestedSceneView = null;
                StartNote(sceneView == mouseSceneView ? mousePositionInSceneView : currentEvent.mousePosition);
            }

            if (shownDraft != null && currentEvent.type == EventType.Repaint)
                LevelNoteGizmoDrawer.DrawPreview(shownDraft.Point, shownDraft.Kind, sceneView.camera);
        }

        // Окно открываем после отрисовки сцены: из её середины открывать окна нельзя.
        // Ответы окна привязаны к своему черновику: закрытие прежнего окна не трогает новый.
        private static void StartNote(Vector2 mousePosition)
        {
            var draft = new LevelNoteDraft(FindSurfacePoint(mousePosition), SceneUnderMouse(mousePosition), lastUsedKind);
            shownDraft = draft;

            var popupScreenPosition = GUIUtility.GUIToScreenPoint(mousePosition) + PopupOffsetFromMouse;
            EditorApplication.delayCall += () => LevelNoteTextPopup.Open(popupScreenPosition, draft.Kind,
                kind => ChangeDraftKind(draft, kind), (text, kind) => ConfirmDraft(draft, text, kind), () => ReleaseDraft(draft));

            SceneView.RepaintAll();
        }

        // Сначала меши — встаёт и на геометрию без коллайдера, потом коллайдеры, потом земля, потом просто перед камерой.
        private static Vector3 FindSurfacePoint(Vector2 mousePosition)
        {
            if (HandleUtility.PlaceObject(mousePosition, out var meshPoint, out _))
                return meshPoint;

            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Physics.SyncTransforms();

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;

            var ground = new Plane(Vector3.up, Vector3.zero);

            if (ground.Raycast(ray, out var groundDistance) && groundDistance < MaxGroundDistance)
                return ray.GetPoint(groundDistance);

            return ray.GetPoint(InFrontOfCameraDistance);
        }

        // Открыто несколько сцен — заметка ложится в сцену того, на что навели; под курсором пусто — в активную.
        private static Scene SceneUnderMouse(Vector2 mousePosition)
        {
            var pickedObject = HandleUtility.PickGameObject(mousePosition, false);
            return pickedObject != null && pickedObject.scene.isLoaded ? pickedObject.scene : SceneManager.GetActiveScene();
        }


        // ── Ответы поля текста ───────────────────────────

        private static void ChangeDraftKind(LevelNoteDraft draft, LevelNoteKind kind)
        {
            draft.Kind = kind;
            SceneView.RepaintAll();
        }


        private static void ConfirmDraft(LevelNoteDraft draft, string text, LevelNoteKind kind)
        {
            lastUsedKind = kind;

            if (draft.Scene.IsValid() && draft.Scene.isLoaded)
                CreateNoteInScene(draft.Scene, draft.Point, text, kind, PersonName);

            ReleaseDraft(draft);
        }


        private static void ReleaseDraft(LevelNoteDraft draft)
        {
            if (shownDraft != draft)
                return;

            shownDraft = null;
            SceneView.RepaintAll();
        }
    }


    /// <summary>Заметка, которая ждёт текста: где встанет, в какой сцене и какого типа — пока открыто поле у курсора.</summary>
    internal class LevelNoteDraft
    {
        public LevelNoteDraft(Vector3 point, Scene scene, LevelNoteKind kind)
        {
            Point = point;
            Scene = scene;
            Kind = kind;
        }


        public Vector3 Point { get; }
        public Scene Scene { get; }
        public LevelNoteKind Kind { get; set; }
    }
}
