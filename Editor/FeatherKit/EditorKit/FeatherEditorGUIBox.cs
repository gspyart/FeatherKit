using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Прямоугольник с заливкой/обводкой/скруглением через Handles, без текстур.</summary>
    public static class FeatherEditorGUIBox
    {
        private const int CornerSegments = 8; // сегментов на одну скруглённую четверть — компромисс гладкость/производительность

        // Begin/End — размер сам подстроится под контент внутри; fullWidth игнорирует отступы инспектора.
        public static Rect BoxBegin(Color fillColor, Color outlineColor, float outlineThickness = 1f, float cornerRadius = 0f, bool fullWidth = false)
        {
            var rect = EditorGUILayout.BeginVertical();

            if (Event.current.type == EventType.Repaint)
            {
                var drawRect = rect;

                if (fullWidth)
                {
                    drawRect.x = 0f;
                    drawRect.width = EditorGUIUtility.currentViewWidth;
                }

                Draw(drawRect, fillColor, outlineColor, outlineThickness, cornerRadius);
            }

            return rect;
        }

        public static void BoxEnd()
        {
            EditorGUILayout.EndVertical();
        }

        // Вариант с явным Rect — для кастомного позиционирования вне обычного layout-потока.
        public static void Draw(Rect rect, Color fillColor, Color outlineColor, float outlineThickness = 1f, float cornerRadius = 0f)
        {
            var points = BuildRoundedRectPoints(rect, cornerRadius);
            var previousColor = Handles.color;

            Handles.BeginGUI();

            if (fillColor.a > 0f)
            {
                Handles.color = fillColor;
                Handles.DrawAAConvexPolygon(points);
            }

            if (outlineThickness > 0f && outlineColor.a > 0f)
            {
                Handles.color = outlineColor;

                var closedLoop = new Vector3[points.Length + 1];
                Array.Copy(points, closedLoop, points.Length);
                closedLoop[points.Length] = points[0];

                Handles.DrawAAPolyLine(outlineThickness, closedLoop);
            }

            Handles.EndGUI();
            Handles.color = previousColor;
        }


        // radius <= 0 — обычный прямоугольник из 4 углов, без дуг.
        private static Vector3[] BuildRoundedRectPoints(Rect rect, float radius)
        {
            radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f));

            if (radius <= 0.01f)
            {
                return new Vector3[]
                {
                    new Vector3(rect.xMin, rect.yMin),
                    new Vector3(rect.xMax, rect.yMin),
                    new Vector3(rect.xMax, rect.yMax),
                    new Vector3(rect.xMin, rect.yMax),
                };
            }

            var points = new List<Vector3>();

            AddCornerArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f); // верх-право
            AddCornerArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f);   // низ-право
            AddCornerArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f);  // низ-лево
            AddCornerArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f); // верх-лево

            return points.ToArray();
        }

        // Одна четверть окружности (90°) от startAngleDeg, по часовой стрелке (GUI-координаты: Y вниз).
        private static void AddCornerArc(List<Vector3> points, Vector2 center, float radius, float startAngleDeg)
        {
            for (var i = 0; i <= CornerSegments; i++)
            {
                var angle = (startAngleDeg + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }
}
