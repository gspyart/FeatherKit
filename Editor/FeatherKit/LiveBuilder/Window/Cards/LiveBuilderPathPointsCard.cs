using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Точки» зоны «Выделено» у стены по линии и здания: сколько точек, вид точек — выбранных в сцене, а если не выбрано ни одной, всех;
    /// скругление выбранных углов, убрать выбранные, замкнуть или разомкнуть стену (здание всегда замкнуто). Как править в сцене — пояснение.
    /// </summary>
    public sealed class LiveBuilderPathPointsCard : LiveBuilderSelectionCard
    {
        public override int Order => 50;


        public override bool Draw(GameObject selected)
        {
            var component = LiveBuilderOwnComponentCard.OwnComponentOf(selected);

            if (component is not (SplineWall or SplineBuilding))
                return false;

            DrawPathActions(component.GetComponent<SplineContainer>(), component is SplineWall);
            return true;
        }

        private static void DrawPathActions(SplineContainer container, bool canOpen)
        {
            using (LiveBuilderWindowControls.Card("Точки"))
            {
                var total = 0;

                foreach (var spline in container.Splines)
                    total += spline.Count;

                var isOwnSelection = LiveBuilderContourSelection.Owner != null && LiveBuilderContourSelection.Owner.GetComponent<SplineContainer>() == container;
                var selected = isOwnSelection ? LiveBuilderContourSelection.Knots.Count : 0;
                LiveBuilderWindowControls.Computed("Точек", total + (selected > 0 ? ", выбрано " + selected : ""));

                var current = LiveBuilderContourSelection.CommonShapeOf(container);
                var chosen = LiveBuilderWindowControls.Switch(
                    new GUIContent(selected > 0 ? "Выбранные" : "Все точки", "Угол — прямые отрезки. Плавно — кривая сама проходит через точку. " +
                                                                           "Безье — у точки два уса, ими гнёшь кривую в сцене. Выбрано в сцене — меняются выбранные, иначе все"),
                    current, ("Угол", LiveBuilderOptionIcon.Corner), ("Плавно", LiveBuilderOptionIcon.Smooth), ("Безье", LiveBuilderOptionIcon.Bezier));

                if (chosen != current && chosen >= 0)
                    LiveBuilderContourSelection.ApplyShape(container, (LiveBuilderKnotShape)chosen);

                if (selected > 0)
                    DrawCornerRadius(container);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(selected == 0))
                        if (GUILayout.Button(new GUIContent("Убрать выбранные", "Как Delete в сцене. Меньше трёх точек у замкнутого контура и двух у линии не останется")))
                            LiveBuilderContourKeys.RemoveSelectedKnots(container);

                    using (new EditorGUI.DisabledScope(!container.Spline.Closed && container.Spline.Count < 3))
                        if (canOpen && GUILayout.Button(new GUIContent(container.Spline.Closed ? "Разомкнуть" : "Замкнуть",
                                "Замкнуть — последняя точка соединится с первой, разомкнуть — замыкающий отрезок уберётся. В сцене — клавиша C")))
                            LiveBuilderContourKeys.ToggleClosed(container);
                }

                var selectHotkey = LiveBuilderToolPalette.HotkeyOf(LiveBuilderToolPalette.Get<LiveBuilderSelectTool>());
                LiveBuilderWindowControls.Explanation("Points", "В сцене, в «Выделении» (" + selectHotkey + ") или в инструменте этой постройки: тяни точку, «+» на отрезке — новая точка, " +
                                               "«+» за концом — продлить, Shift+клик или Shift+рамка — выбрать несколько, двойной клик — угол → плавно → Безье, " +
                                               "у Безье тяни усы, у угла — голубая ручка скругления, стрелки — на шаг сетки, Delete — убрать.");
            }
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
            var radius = EditorGUILayout.FloatField(new GUIContent("Скругление, м", "Радиус выбранных углов. 0 — острый угол. Из скольких кусков дуга — " +
                                                                                    "в «Тонко» у стены или здания, 1 кусок — срез"), first);
            EditorGUI.showMixedValue = false;

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(container, "Round Corners");

            foreach (var (spline, knot) in corners)
                LiveBuilderCornerRadii.SetRadius(spline, knot, Mathf.Max(0f, radius));
        }
    }
}
