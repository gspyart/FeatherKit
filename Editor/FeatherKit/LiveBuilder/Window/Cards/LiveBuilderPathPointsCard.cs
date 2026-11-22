using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Points у стены по линии и здания: сколько точек, вид точек — выбранных в сцене, а если не выбрано ни одной, всех;
    /// скругление выбранных углов, убрать выбранные, замкнуть или разомкнуть стену (здание всегда замкнуто). Свёрнута: точки правят в сцене.
    /// </summary>
    public sealed class LiveBuilderPathPointsCard : LiveBuilderSelectionCard
    {
        public override int Order => PointsOrder;

        public override string Title => "Points";

        public override bool IsOpenByDefault => false;


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return selected.Count == 1 && AreAllOfKind(selected, LiveBuilderSelectionKind.Wall, LiveBuilderSelectionKind.Building);
        }


        // «4 corners · none selected».
        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            var container = selected[0].GetComponent<SplineContainer>();

            if (container == null)
                return null;

            var selectedCount = SelectedCountIn(container);
            return TotalOf(container) + " points · " + (selectedCount > 0 ? selectedCount + " selected" : "none selected");
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            var container = selected[0].GetComponent<SplineContainer>();

            if (container != null)
                DrawPathActions(container, selected[0].GetComponent<SplineWall>() != null);
        }

        private static void DrawPathActions(SplineContainer container, bool canOpen)
        {
            var selected = SelectedCountIn(container);
            var current = LiveBuilderContourSelection.CommonShapeOf(container);
            var chosen = LiveBuilderWindowControls.Switch(
                new GUIContent(selected > 0 ? "Selected" : "All points", "Corner — прямые отрезки. Smooth — кривая сама проходит через точку. " +
                                                                        "Bezier — у точки два уса, ими гнёшь кривую в сцене. Выбрано в сцене — меняются выбранные, иначе все"),
                current, ("Corner", LiveBuilderOptionIcon.Corner), ("Smooth", LiveBuilderOptionIcon.Smooth), ("Bezier", LiveBuilderOptionIcon.Bezier));

            if (chosen != current && chosen >= 0)
                LiveBuilderContourSelection.ApplyShape(container, (LiveBuilderKnotShape)chosen);

            if (selected > 0)
                DrawCornerRadius(container);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected == 0))
                    if (GUILayout.Button(new GUIContent("Remove Selected", "Как Delete в сцене. Меньше трёх точек у замкнутого контура и двух у линии не останется")))
                        LiveBuilderContourKeys.RemoveSelectedKnots(container);

                using (new EditorGUI.DisabledScope(!container.Spline.Closed && container.Spline.Count < 3))
                    if (canOpen && GUILayout.Button(new GUIContent(container.Spline.Closed ? "Open" : "Close",
                            "Замкнуть — последняя точка соединится с первой, разомкнуть — замыкающий отрезок уберётся. В сцене — клавиша C")))
                        LiveBuilderContourKeys.ToggleClosed(container);
            }
        }


        private static int TotalOf(SplineContainer container)
        {
            var total = 0;

            foreach (var spline in container.Splines)
                total += spline.Count;

            return total;
        }

        private static int SelectedCountIn(SplineContainer container)
        {
            var isOwnSelection = LiveBuilderContourSelection.Owner != null && LiveBuilderContourSelection.Owner.GetComponent<SplineContainer>() == container;
            return isOwnSelection ? LiveBuilderContourSelection.Knots.Count : 0;
        }

        // Скругление выбранных углов одним числом: разные — поле пустое, введёшь — у всех станет это.
        private static void DrawCornerRadius(SplineContainer container)
        {
            var corners = new List<(Spline Spline, int Knot)>();

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
                if (LiveBuilderCornerRadiusHandle.CanRound(container.Splines[splineIndex], knotIndex))
                    corners.Add((container.Splines[splineIndex], knotIndex));

            if (corners.Count == 0)
                return;

            var first = LiveBuilderCornerRadii.RadiusOf(corners[0].Spline, corners[0].Knot);
            var isMixed = corners.Exists(corner => !Mathf.Approximately(LiveBuilderCornerRadii.RadiusOf(corner.Spline, corner.Knot), first));

            EditorGUI.showMixedValue = isMixed;
            EditorGUI.BeginChangeCheck();
            var radius = EditorGUILayout.FloatField(new GUIContent("Corner radius, m", "Радиус выбранных углов. 0 — острый угол. Из скольких кусков дуга — " +
                                                                                    "в Fine Tuning у стены или здания, 1 кусок — срез"), first);
            EditorGUI.showMixedValue = false;

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(container, "Round Corners");

            foreach (var (spline, knot) in corners)
                LiveBuilderCornerRadii.SetRadius(spline, knot, Mathf.Max(0f, radius));
        }
    }
}
