using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рабочая точка инструмента «Своя точка»: временная опора, вокруг которой вращается
    /// и растягивается выделенное, и выбранный режим ручек. В объекты не записывается,
    /// живёт до перезагрузки скриптов. Общая для инструмента и его панели.
    /// </summary>
    public static class WorkingPoint
    {
        // ── Состояние ────────────────────────────────────

        public static event Action Changed;

        public static Vector3 Position { get; private set; }

        public static WorkingPointMode Mode { get; private set; } = WorkingPointMode.RotateSelection;

        // Ставили ли точку с запуска редактора: до этого её место — пивот выделенного.
        public static bool IsPlaced { get; private set; }


        public static void MoveTo(Vector3 position)
        {
            Position = position;
            IsPlaced = true;
            NotifyChanged();
        }


        public static void SwitchMode(WorkingPointMode mode)
        {
            Mode = mode;
            NotifyChanged();
        }


        // ── Быстрые места ────────────────────────────────

        public static void MoveToActivePivot()
        {
            if (Selection.activeTransform != null)
                MoveTo(Selection.activeTransform.position);
        }


        // Середина габаритов выделенного по мешам; мешей нет — середина пивотов.
        public static void MoveToSelectionCenter()
        {
            var targets = Selection.transforms;

            if (targets.Length == 0)
                return;

            var corners = targets.SelectMany(MeshBoundsCorners.CollectInWorld).ToList();

            if (corners.Count == 0)
                corners = targets.Select(target => target.position).ToList();

            var bounds = new Bounds(corners[0], Vector3.zero);

            foreach (var corner in corners)
                bounds.Encapsulate(corner);

            MoveTo(bounds.center);
        }


        // ── Общие помощники ──────────────────────────────

        private static void NotifyChanged()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }
    }
}
