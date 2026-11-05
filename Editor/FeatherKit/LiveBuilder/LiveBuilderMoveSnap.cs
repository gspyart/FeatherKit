using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Перенос «от точки к точке», как в SketchUp: пока тянешь ручки переноса (<see cref="LiveBuilderMoveHandle"/>), углы переносимого прилипают
    /// к углам, серединам и точкам других построек (<see cref="LiveBuilderPointSnap"/>) — но только вдоль осей, которые тянешь: тянешь по X —
    /// угол встаёт вровень с точкой по X, остальное не трогается. Углы — рамки активного выделенного (<see cref="LiveBuilderSelectionInfo"/>).
    /// Своё не ловит: точки внутри того, что едет, пропускаются. Без окна сцены (проверки) не прилипает — остаётся сдвиг по сетке.
    /// </summary>
    public static class LiveBuilderMoveSnap
    {
        // Насколько тянут по оси, чтобы она считалась тянущейся, м.
        private const float DraggedAxisThreshold = 0.0001f;

        // Углы рамки переносимого в начале жеста, в мире, и их общий габарит.
        private static readonly List<Vector3> StartCorners = new();
        private static Bounds startBounds;


        // ── Жест ─────────────────────────────────────────────────────────

        // Запомнить углы рамки того, что едет, в начале жеста. Размера нет — прилипать нечем.
        public static void Begin(Transform active)
        {
            StartCorners.Clear();
            LiveBuilderPointSnap.Hold();

            if (active == null || !LiveBuilderSelectionInfo.TryDescribe(active.gameObject, out var info))
                return;

            for (var corner = 0; corner < 8; corner++)
            {
                var signs = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                var point = info.Frame.MultiplyPoint3x4(info.LocalBounds.center + Vector3.Scale(info.LocalBounds.extents, signs));

                if (StartCorners.Count == 0)
                    startBounds = new Bounds(point, Vector3.zero);
                else
                    startBounds.Encapsulate(point);

                StartCorners.Add(point);
            }
        }


        public static void End()
        {
            StartCorners.Clear();
            LiveBuilderPointSnap.Release();
        }


        // ── Привязка ─────────────────────────────────────────────────────

        // Сдвиг в осях ручек: rawOffset — куда тянут, gridOffset — он же по сетке. Какой-то угол у точки постройки — сдвиг, что ставит его на неё
        // по тянущимся осям, и подпись «к углу»; иначе — по сетке и null.
        public static Vector3 Snap(Quaternion axes, Vector3 rawOffset, Vector3 gridOffset, out string label)
        {
            label = null;

            if (StartCorners.Count == 0)
                return gridOffset;

            var rawWorld = axes * rawOffset;
            var ignored = new Bounds(startBounds.center + rawWorld, startBounds.size + Vector3.one * 0.02f);
            var toAxes = Quaternion.Inverse(axes);
            var bestPixels = float.MaxValue;
            var result = gridOffset;

            foreach (var corner in StartCorners)
            {
                if (!LiveBuilderPointSnap.TryFindPointNear(corner + rawWorld, ignored, out var point, out var pixels) || pixels >= bestPixels)
                    continue;

                var toPoint = toAxes * (point - corner);
                var snapped = gridOffset;

                for (var axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(rawOffset[axis]) > DraggedAxisThreshold)
                        snapped[axis] = toPoint[axis];

                bestPixels = pixels;
                result = snapped;
                label = "угол к точке";
            }

            return result;
        }
    }
}
