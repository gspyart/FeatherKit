using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вырез (<see cref="GeometryCutter"/>) в окне сцены: оранжевая рамка его формы спереди и сзади и полупрозрачная коробка — у каждой копии вверх.
    /// Выделенный — ярко; выделена стена или здание, в котором он лежит, — бледно, чтобы найти и кликнуть; иначе не рисуется — видна только дыра.
    /// Выделить кликом по дыре помогает <see cref="GeometryCutterPicker"/>.
    /// </summary>
    public static class GeometryCutterGizmos
    {
        public static readonly Color CutterColor = new(1f, 0.55f, 0.1f);


        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        private static void DrawCutter(GeometryCutter cutter, GizmoType gizmoType)
        {
            var isSelected = Selection.Contains(cutter.gameObject);
            var outline = GeometryCutterOutline.OutlineOf(cutter);

            for (var copy = 0; copy < cutter.CopiesUp; copy++)
                DrawCopy(cutter, outline, GeometryCutterOutline.CopyToWorld(cutter, copy), isSelected);
        }

        private static void DrawCopy(GeometryCutter cutter, List<Vector2> outline, Matrix4x4 copyToWorld, bool isSelected)
        {
            var frontZ = cutter.Depth / 2f;
            var backZ = -frontZ;

            Gizmos.matrix = copyToWorld;
            Gizmos.color = new Color(CutterColor.r, CutterColor.g, CutterColor.b, isSelected ? 1f : 0.45f);

            // Рёбра вдоль глубины — не у каждой точки круга, а через одну-две: так рамка не сливается в пятно.
            var edgeStep = Mathf.Max(1, outline.Count / 8);

            for (var index = 0; index < outline.Count; index++)
            {
                var start = outline[index];
                var end = outline[(index + 1) % outline.Count];
                Gizmos.DrawLine(new Vector3(start.x, start.y, frontZ), new Vector3(end.x, end.y, frontZ));
                Gizmos.DrawLine(new Vector3(start.x, start.y, backZ), new Vector3(end.x, end.y, backZ));

                if (index % edgeStep == 0)
                    Gizmos.DrawLine(new Vector3(start.x, start.y, frontZ), new Vector3(start.x, start.y, backZ));
            }

            Gizmos.color = new Color(CutterColor.r, CutterColor.g, CutterColor.b, isSelected ? 0.08f : 0.03f);
            Gizmos.DrawCube(new Vector3(0f, cutter.Height / 2f, 0f), new Vector3(cutter.Width, cutter.Height, cutter.Depth));
        }
    }
}
