using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прицел под мышью, как в ProBuilder: точка, уже прилипшая к сетке, и клочок сетки вокруг на плоскости рисования.
    /// До нажатия видно, куда встанет угол коробки и в какой плоскости она начнётся.
    /// </summary>
    public static class BoxModelSnapCursor
    {
        // Сколько клеток сетки видно в каждую сторону от точки.
        private const int VisibleCellsEachSide = 3;
        private const float PointSize = 0.06f;

        private static readonly Color PointColor = new(1f, 0.85f, 0.2f);
        private static readonly Color GridColor = new(1f, 0.85f, 0.2f, 0.5f);


        public static void Draw(BoxModelDrawPlane plane, Vector3 snappedLocalPoint)
        {
            using (new Handles.DrawingScope(plane.FrameToWorld))
            {
                DrawGridPatch(plane, snappedLocalPoint);

                Handles.color = PointColor;
                var size = HandleUtility.GetHandleSize(snappedLocalPoint) * PointSize;
                Handles.DotHandleCap(0, snappedLocalPoint, Quaternion.identity, size, EventType.Repaint);
            }
        }

        // Линии сетки по двум осям плоскости; к краям гаснут.
        private static void DrawGridPatch(BoxModelDrawPlane plane, Vector3 center)
        {
            var axisU = (plane.Axis + 1) % 3;
            var axisV = (plane.Axis + 2) % 3;
            DrawLinesAcross(center, axisU, axisV);
            DrawLinesAcross(center, axisV, axisU);
        }

        // Линии вдоль оси lineAxis, разнесённые по оси spreadAxis.
        private static void DrawLinesAcross(Vector3 center, int lineAxis, int spreadAxis)
        {
            var lineStep = BoxModelGrid.StepOn(lineAxis);
            var spreadStep = BoxModelGrid.StepOn(spreadAxis);

            for (var index = -VisibleCellsEachSide; index <= VisibleCellsEachSide; index++)
            {
                var fade = 1f - Mathf.Abs(index) / (VisibleCellsEachSide + 1f);
                Handles.color = new Color(GridColor.r, GridColor.g, GridColor.b, GridColor.a * fade);

                var start = center;
                start[spreadAxis] += index * spreadStep;
                start[lineAxis] -= VisibleCellsEachSide * lineStep;

                var end = start;
                end[lineAxis] += 2 * VisibleCellsEachSide * lineStep;

                Handles.DrawLine(start, end);
            }
        }
    }
}
