using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Режим «поставить на поверхность»: пока он включён, под курсором в сцене видна метка —
    /// куда встанет объект, — а клик ставит туда выделенное и выключает режим.
    /// Esc или повторное нажатие кнопки — выход без перестановки.
    ///
    /// Поверхность ищется лучом по коллайдерам: меш без коллайдера луч не видит.
    /// Коллайдеры самих выделенных объектов луч пропускает, иначе объект упирался бы в себя.
    /// </summary>
    public static class SurfacePlacementMode
    {
        private const int MaxHitsPerRay = 64;
        private const float MarkerRadius = 0.25f;
        private static readonly Color MarkerColor = new(0.3f, 0.9f, 1f);

        private static readonly RaycastHit[] HitsBuffer = new RaycastHit[MaxHitsPerRay];

        // ── Включение и выключение ───────────────────────

        public static event Action ActiveChanged;

        public static bool IsActive { get; private set; }

        public static void Start()
        {
            if (IsActive)
                return;

            IsActive = true;
            SceneView.duringSceneGui += HandleSceneInput;
            Selection.selectionChanged += StopWhenNothingSelected;
            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }


        public static void Stop()
        {
            if (!IsActive)
                return;

            IsActive = false;
            SceneView.duringSceneGui -= HandleSceneInput;
            Selection.selectionChanged -= StopWhenNothingSelected;
            ActiveChanged?.Invoke();
            SceneView.RepaintAll();
        }


        private static void StopWhenNothingSelected()
        {
            if (!SelectionPlacer.HasTargets)
                Stop();
        }


        // ── Ввод в сцене ─────────────────────────────────

        private static void HandleSceneInput(SceneView sceneView)
        {
            var current = Event.current;

            // Свой контрол по умолчанию — чтобы клик ставил объект, а не менял выделение.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseMove:
                    sceneView.Repaint();
                    break;

                case EventType.Repaint:
                    if (TryFindSurface(current.mousePosition, out var surface))
                        DrawMarker(surface);
                    break;

                // С Alt левая кнопка вращает камеру — её не перехватываем.
                case EventType.MouseDown when current.button == 0 && !current.alt:
                    PlaceAtCursor(current.mousePosition);
                    current.Use();
                    break;

                case EventType.KeyDown when current.keyCode == KeyCode.Escape:
                    current.Use();
                    Stop();
                    break;
            }
        }

        // Мимо поверхности — режим остаётся, можно кликнуть ещё раз.
        private static void PlaceAtCursor(Vector2 mousePosition)
        {
            if (!TryFindSurface(mousePosition, out var surface))
                return;

            SelectionPlacer.PlaceOnSurface(surface.point, surface.normal);
            Stop();
        }

        private static void DrawMarker(RaycastHit surface)
        {
            float size = HandleUtility.GetHandleSize(surface.point);

            using (new Handles.DrawingScope(MarkerColor))
            {
                Handles.DrawWireDisc(surface.point, surface.normal, MarkerRadius * size, 2f);
                Handles.DrawLine(surface.point, surface.point + surface.normal * size * 0.5f, 2f);
            }
        }


        // ── Поиск поверхности ────────────────────────────

        private static bool TryFindSurface(Vector2 mousePosition, out RaycastHit surface)
        {
            // В редакторе коллайдеры не догоняют сдвинутые объекты сами — подтягиваем их перед лучом.
            Physics.SyncTransforms();

            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, HitsBuffer, Mathf.Infinity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            surface = default;
            bool found = false;

            for (int index = 0; index < hitCount; index++)
            {
                var hit = HitsBuffer[index];
                if (found && hit.distance >= surface.distance)
                    continue;

                if (IsPartOfSelection(hit.transform))
                    continue;

                surface = hit;
                found = true;
            }

            return found;
        }

        private static bool IsPartOfSelection(Transform hitTransform)
        {
            foreach (var selected in Selection.transforms)
            {
                if (hitTransform.IsChildOf(selected))
                    return true;
            }

            return false;
        }
    }
}
