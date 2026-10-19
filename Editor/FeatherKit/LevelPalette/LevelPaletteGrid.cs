using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Сетка квадратных клеток палитры — часть окна, живёт внутри него.
    /// - рисует только видимые ряды и грузит только их ассеты: тысяча клеток прокручивается без задержек
    /// - мышь: клик, двойной клик, ПКМ — отдаёт окну событиями, сама не решает, что они значат
    /// - перетаскивание наружу: в сцену, на грани, в поле инспектора; что именно тащить — спрашивает у окна
    /// - перетаскивание внутрь: ассеты и папки из Project отдаёт окну событием
    /// </summary>
    public class LevelPaletteGrid
    {
        private const string OwnDragKey = "FeatherKit.LevelPalette.Cell";

        private const float CellSpacing = 4f;
        private const float NameHeight = 14f;
        private const float ScrollbarWidth = 14f;
        private const float HighlightThickness = 2f;

        // Столько пикселей мышь должна пройти с зажатой кнопкой, чтобы клик стал перетаскиванием.
        private const float DragStartDistance = 5f;

        private static readonly Color HighlightColor = new Color(0.25f, 0.6f, 1f);
        private static readonly Color DropTargetColor = new Color(0.25f, 0.6f, 1f, 0.15f);

        private readonly Func<Object, Object[]> collectDraggedObjects;
        private readonly Dictionary<string, Object> loadedAssets = new Dictionary<string, Object>();
        private readonly Dictionary<string, string> assetNames = new Dictionary<string, string>();

        private Vector2 scroll;
        private string pendingGuid;
        private Vector2 pendingStartPosition;
        private string scrollTargetGuid;


        /// <param name="collectDraggedObjects">Что тащить наружу за ассет клетки: текстура, например, тащит ещё и свой материал.</param>
        public LevelPaletteGrid(Func<Object, Object[]> collectDraggedObjects)
        {
            this.collectDraggedObjects = collectDraggedObjects;
        }


        public event Action<string> CellClicked;

        public event Action<string> CellDoubleClicked;

        public event Action<string> CellContextClicked;

        /// <summary>В сетку бросили ассеты или папки из Project.</summary>
        public event Action<Object[]> ObjectsDropped;


        private static bool IsOwnDrag => DragAndDrop.GetGenericData(OwnDragKey) != null;


        // ── Отрисовка ────────────────────────────────────────────────────

        /// <summary>Нарисовать сетку. true — превью ещё готовятся, окно надо перерисовать позже.</summary>
        public bool Draw(Rect area, IReadOnlyList<string> guids, string highlightedGuid, float cellSize, bool showNames)
        {
            if (Event.current.type == EventType.Layout)
                return false;

            HandleDropIn(area);

            if (guids.Count == 0)
            {
                DrawEmptyHint(area);
                return false;
            }

            var cellHeight = cellSize + (showNames ? NameHeight : 0f);
            var stepX = cellSize + CellSpacing;
            var stepY = cellHeight + CellSpacing;
            var columns = Mathf.Max(1, Mathf.FloorToInt((area.width - ScrollbarWidth) / stepX));
            var rows = Mathf.CeilToInt(guids.Count / (float)columns);
            var contentRect = new Rect(0f, 0f, area.width - ScrollbarWidth, rows * stepY);

            ApplyScrollTarget(guids, columns, stepY, area.height);

            scroll = GUI.BeginScrollView(area, scroll, contentRect);

            var firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / stepY));
            var lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scroll.y + area.height) / stepY));
            var isWaitingForPreviews = false;

            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;

                    if (index >= guids.Count)
                        break;

                    var cellRect = new Rect(column * stepX + CellSpacing * 0.5f, row * stepY, cellSize, cellHeight);

                    isWaitingForPreviews |= DrawCell(cellRect, guids[index], guids[index] == highlightedGuid, cellSize, showNames);
                }
            }

            GUI.EndScrollView();

            return isWaitingForPreviews;
        }

        private static void DrawEmptyHint(Rect area)
        {
            GUI.Label(area, "Пусто.\nПеретащи сюда ассеты или папку из Project,\nили задай папку в настройках вкладки (⚙)",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
        }

        // Прокрутить так, чтобы клетка оказалась на виду; уже видна — не трогаем.
        private void ApplyScrollTarget(IReadOnlyList<string> guids, int columns, float stepY, float viewHeight)
        {
            if (scrollTargetGuid == null)
                return;

            var index = IndexOf(guids, scrollTargetGuid);
            scrollTargetGuid = null;

            if (index < 0)
                return;

            var rowTop = index / columns * stepY;

            if (rowTop < scroll.y || rowTop + stepY > scroll.y + viewHeight)
                scroll.y = Mathf.Max(0f, rowTop - (viewHeight - stepY) * 0.5f);
        }

        private bool DrawCell(Rect cellRect, string guid, bool isHighlighted, float cellSize, bool showNames)
        {
            var asset = LoadAsset(guid);

            if (asset == null)
                return false;

            var previewRect = new Rect(cellRect.x, cellRect.y, cellSize, cellSize);
            var isWaitingForPreview = false;

            if (Event.current.type == EventType.Repaint)
            {
                isWaitingForPreview = PaletteAssetPreviewDrawer.Draw(previewRect, asset);

                if (isHighlighted)
                    DrawFrame(previewRect);
            }

            var name = NameOf(guid);

            if (showNames)
                GUI.Label(new Rect(cellRect.x, previewRect.yMax, cellSize, NameHeight), name, EditorStyles.centeredGreyMiniLabel);

            GUI.Label(previewRect, new GUIContent(string.Empty, name));
            HandleCellMouse(cellRect, guid, asset);

            return isWaitingForPreview;
        }

        private static void DrawFrame(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, HighlightThickness), HighlightColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - HighlightThickness, rect.width, HighlightThickness), HighlightColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, HighlightThickness, rect.height), HighlightColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - HighlightThickness, rect.y, HighlightThickness, rect.height), HighlightColor);
        }


        // ── Мышь по клетке ───────────────────────────────────────────────

        // Клик срабатывает на отпускании: зажал и повёл — это перетаскивание, а не клик.
        private void HandleCellMouse(Rect cellRect, string guid, Object asset)
        {
            var currentEvent = Event.current;

            if (!cellRect.Contains(currentEvent.mousePosition))
                return;

            switch (currentEvent.type)
            {
                case EventType.MouseDown when currentEvent.button == 0:
                    pendingGuid = guid;
                    pendingStartPosition = currentEvent.mousePosition;

                    if (currentEvent.clickCount == 2)
                    {
                        pendingGuid = null;
                        CellDoubleClicked?.Invoke(guid);
                    }

                    currentEvent.Use();
                    break;

                case EventType.MouseUp when currentEvent.button == 0 && pendingGuid == guid:
                    pendingGuid = null;
                    CellClicked?.Invoke(guid);
                    currentEvent.Use();
                    break;

                case EventType.MouseDrag when pendingGuid == guid
                                              && Vector2.Distance(currentEvent.mousePosition, pendingStartPosition) > DragStartDistance:
                    pendingGuid = null;
                    StartDragOut(asset);
                    currentEvent.Use();
                    break;

                case EventType.ContextClick:
                    CellContextClicked?.Invoke(guid);
                    currentEvent.Use();
                    break;
            }
        }


        // ── Перетаскивание ───────────────────────────────────────────────

        private void StartDragOut(Object asset)
        {
            var draggedObjects = collectDraggedObjects(asset);

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = draggedObjects;
            DragAndDrop.paths = Array.ConvertAll(draggedObjects, AssetDatabase.GetAssetPath);
            DragAndDrop.SetGenericData(OwnDragKey, asset);
            DragAndDrop.StartDrag(asset.name);
        }


        // Своё перетаскивание обратно в сетку не принимаем: клетка и так тут.
        private void HandleDropIn(Rect area)
        {
            var currentEvent = Event.current;

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
            {
                if (currentEvent.type == EventType.Repaint && IsDraggingInto(area))
                    EditorGUI.DrawRect(area, DropTargetColor);

                return;
            }

            if (!IsDraggingInto(area))
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                ObjectsDropped?.Invoke(DragAndDrop.objectReferences);
            }

            currentEvent.Use();
        }

        private static bool IsDraggingInto(Rect area)
        {
            return area.Contains(Event.current.mousePosition)
                   && DragAndDrop.objectReferences.Length > 0
                   && !IsOwnDrag
                   && Array.TrueForAll(DragAndDrop.objectReferences, EditorUtility.IsPersistent);
        }


        // ── Прокрутка к клетке ───────────────────────────────────────────

        /// <summary>На следующей отрисовке прокрутить к этой клетке.</summary>
        public void ScrollTo(string guid)
        {
            scrollTargetGuid = guid;
        }


        // ── Кэш ассетов ──────────────────────────────────────────────────

        /// <summary>Проект поменялся — забыть загруженное: ассеты могли удалить или переименовать.</summary>
        public void ClearCache()
        {
            loadedAssets.Clear();
            assetNames.Clear();
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private Object LoadAsset(string guid)
        {
            if (!loadedAssets.TryGetValue(guid, out var asset) || asset == null)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                loadedAssets[guid] = asset;
            }

            return asset;
        }

        private string NameOf(string guid)
        {
            if (!assetNames.TryGetValue(guid, out var name))
            {
                name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                assetNames[guid] = name;
            }

            return name;
        }

        private static int IndexOf(IReadOnlyList<string> guids, string guid)
        {
            for (var i = 0; i < guids.Count; i++)
            {
                if (guids[i] == guid)
                    return i;
            }

            return -1;
        }
    }
}
