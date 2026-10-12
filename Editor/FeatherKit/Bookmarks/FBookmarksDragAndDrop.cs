using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Перетаскивание в окне закладок — часть окна, живёт внутри него.
    /// - принимает ассеты из Project и объекты из Hierarchy
    /// - переставляет закладки и папки внутри окна
    /// - отдаёт закладку наружу: префаб — на сцену, ассет — в поле инспектора
    /// </summary>
    public class FBookmarksDragAndDrop
    {
        private const string BookmarkDragKey = "FeatherKit.Bookmarks.Bookmark";
        private const string FolderDragKey = "FeatherKit.Bookmarks.Folder";

        // Столько пикселей мышь должна пройти с зажатой кнопкой, чтобы клик стал перетаскиванием.
        private const float DragStartDistance = 5f;
        private const float IndicatorThickness = 2f;

        private static readonly Color IndicatorColor = new Color(0.25f, 0.6f, 1f);
        private static readonly Color DropFolderHighlight = new Color(0.25f, 0.6f, 1f, 0.25f);

        private readonly EditorWindow owner;
        private readonly FBookmarksStorage storage;

        private FBookmarkFolder pendingFolder;
        private FBookmark pendingBookmark;
        private Vector2 pendingStartPosition;

        private Rect indicatorRect;
        private bool isIndicatorLine;


        public FBookmarksDragAndDrop(EditorWindow owner, FBookmarksStorage storage)
        {
            this.owner = owner;
            this.storage = storage;
        }


        private static bool IsExternalDrag => DragBookmark == null && DragFolder == null
                                              && DragAndDrop.objectReferences.Length > 0;

        private static FBookmarkDragPayload DragBookmark => DragAndDrop.GetGenericData(BookmarkDragKey) as FBookmarkDragPayload;

        private static FBookmarkFolder DragFolder => DragAndDrop.GetGenericData(FolderDragKey) as FBookmarkFolder;


        // ── Кадр окна ────────────────────────────────────────────────────

        /// <summary>Зовётся в начале OnGUI: указатель места ставят строки заново на каждое движение.</summary>
        public void BeginFrame()
        {
            if (Event.current.type == EventType.DragUpdated)
                indicatorRect = Rect.zero;
        }


        /// <summary>Зовётся в конце списка, внутри прокрутки: рисует указатель и следит за начатым жестом.</summary>
        public void EndFrame()
        {
            var current = Event.current;

            switch (current.type)
            {
                case EventType.MouseDrag:
                    TryStartPendingDrag(current);
                    break;

                case EventType.MouseUp:
                    ClearPendingDrag();
                    break;

                case EventType.DragExited:
                    indicatorRect = Rect.zero;
                    owner.Repaint();
                    break;

                case EventType.Repaint:
                    DrawIndicator();
                    break;
            }
        }

        private void DrawIndicator()
        {
            if (indicatorRect == Rect.zero)
                return;

            EditorGUI.DrawRect(indicatorRect, isIndicatorLine ? IndicatorColor : DropFolderHighlight);
        }


        // ── Начать перетаскивание ────────────────────────────────────────

        /// <summary>Мышь нажата на строке. Перетаскивание начнётся, только если её потянут.</summary>
        public void BeginPendingDrag(FBookmarkFolder folder, FBookmark bookmark)
        {
            pendingFolder = folder;
            pendingBookmark = bookmark;
            pendingStartPosition = Event.current.mousePosition;
        }


        private void TryStartPendingDrag(Event current)
        {
            if (pendingFolder == null || Vector2.Distance(current.mousePosition, pendingStartPosition) < DragStartDistance)
                return;

            DragAndDrop.PrepareStartDrag();

            if (pendingBookmark != null)
                PrepareBookmarkDrag(pendingFolder, pendingBookmark);
            else
                PrepareFolderDrag(pendingFolder);

            ClearPendingDrag();
            current.Use();
        }

        // Объект кладём в перетаскивание целиком: так закладку можно бросить на сцену или в поле инспектора.
        private static void PrepareBookmarkDrag(FBookmarkFolder folder, FBookmark bookmark)
        {
            var target = bookmark.Resolve();

            DragAndDrop.objectReferences = target != null ? new[] { target } : new Object[0];
            DragAndDrop.SetGenericData(BookmarkDragKey, new FBookmarkDragPayload(folder, bookmark));
            DragAndDrop.StartDrag(bookmark.DisplayName);
        }

        private static void PrepareFolderDrag(FBookmarkFolder folder)
        {
            DragAndDrop.objectReferences = new Object[0];
            DragAndDrop.SetGenericData(FolderDragKey, folder);
            DragAndDrop.StartDrag(folder.Name);
        }


        private void ClearPendingDrag()
        {
            pendingFolder = null;
            pendingBookmark = null;
        }


        // ── Бросить на строку папки ──────────────────────────────────────

        public void HandleFolderRow(FBookmarkFolder folder, int folderIndex, Rect rect)
        {
            if (!IsDropEventInside(rect))
                return;

            if (DragFolder != null)
            {
                var dragged = DragFolder;
                var isUpper = IsUpperHalf(rect);
                var insertIndex = isUpper ? folderIndex : folderIndex + 1;
                AcceptDrop(DragAndDropVisualMode.Move, LineAt(rect, isUpper), true,
                    () => storage.MoveFolder(dragged, insertIndex));
                return;
            }

            if (DragBookmark != null)
            {
                var payload = DragBookmark;
                AcceptDrop(DragAndDropVisualMode.Move, rect, false,
                    () => storage.MoveBookmark(payload.Bookmark, payload.Folder, folder, folder.Bookmarks.Count));
                return;
            }

            if (IsExternalDrag)
                AcceptDrop(DragAndDropVisualMode.Link, rect, false,
                    () => AddDropped(folder, -1));
        }


        // ── Бросить на строку закладки ───────────────────────────────────

        public void HandleBookmarkRow(FBookmarkFolder folder, int bookmarkIndex, Rect rect)
        {
            if (!IsDropEventInside(rect) || DragFolder != null)
                return;

            var isUpper = IsUpperHalf(rect);
            var insertIndex = isUpper ? bookmarkIndex : bookmarkIndex + 1;

            if (DragBookmark != null)
            {
                var payload = DragBookmark;
                AcceptDrop(DragAndDropVisualMode.Move, LineAt(rect, isUpper), true,
                    () => storage.MoveBookmark(payload.Bookmark, payload.Folder, folder, insertIndex));
                return;
            }

            if (IsExternalDrag)
                AcceptDrop(DragAndDropVisualMode.Link, LineAt(rect, isUpper), true,
                    () => AddDropped(folder, insertIndex));
        }


        // ── Бросить на пустое место ──────────────────────────────────────

        public void HandleEmptySpace(Rect rect)
        {
            if (!IsDropEventInside(rect))
                return;

            if (DragFolder != null)
            {
                var folder = DragFolder;
                AcceptDrop(DragAndDropVisualMode.Move, Rect.zero, true,
                    () => storage.MoveFolder(folder, storage.Folders.Count));
                return;
            }

            if (IsExternalDrag)
                AcceptDrop(DragAndDropVisualMode.Link, Rect.zero, false,
                    () => AddDropped(storage.ActiveFolder, -1));
        }


        // ── Общее для всех целей ─────────────────────────────────────────

        private static bool IsDropEventInside(Rect rect)
        {
            var current = Event.current;

            return (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
                   && rect.Contains(current.mousePosition);
        }


        private void AcceptDrop(DragAndDropVisualMode mode, Rect indicator, bool isLine, System.Action drop)
        {
            DragAndDrop.visualMode = mode;

            if (Event.current.type != EventType.DragPerform)
            {
                indicatorRect = indicator;
                isIndicatorLine = isLine;
                Event.current.Use();
                owner.Repaint();
                return;
            }

            DragAndDrop.AcceptDrag();
            drop();
            indicatorRect = Rect.zero;
            Event.current.Use();
            owner.Repaint();
            GUIUtility.ExitGUI(); // список уже другой — дорисовывать этот кадр нельзя
        }


        private void AddDropped(FBookmarkFolder folder, int insertIndex)
        {
            var rejected = storage.AddBookmarks(folder, DragAndDrop.objectReferences, insertIndex);

            if (rejected > 0)
                owner.ShowNotification(new GUIContent(FBookmarksWindow.DescribeRejected(rejected)));
        }


        private static bool IsUpperHalf(Rect rect)
        {
            return Event.current.mousePosition.y < rect.center.y;
        }


        private static Rect LineAt(Rect row, bool isTop)
        {
            var y = isTop ? row.yMin : row.yMax - IndicatorThickness;

            return new Rect(row.x, y, row.width, IndicatorThickness);
        }


        // ── Что тащим ────────────────────────────────────────────────────

        // Класс, а не пара значений: DragAndDrop хранит только объекты.
        private class FBookmarkDragPayload
        {
            public readonly FBookmarkFolder Folder;
            public readonly FBookmark Bookmark;


            public FBookmarkDragPayload(FBookmarkFolder folder, FBookmark bookmark)
            {
                Folder = folder;
                Bookmark = bookmark;
            }
        }
    }
}
