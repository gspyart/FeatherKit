using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Как выглядит правка контура (<see cref="LiveBuilderContourHandles"/>) в окне сцены: линия контура, точки по виду (угол — квадрат,
    /// плавная — шарик, Безье — ромб), «+» новой точки, усы Безье, длины сторон у точки, за которую тянут. Только рисует — ничего не меняет.
    /// </summary>
    public static class LiveBuilderContourDrawing
    {
        // Размер точки, «+» и конца уса — доля размера ручки Unity: на экране одинаковые при любом зуме.
        public const float KnotSize = 0.06f;
        public const float InsertSize = 0.08f;
        public const float TangentSize = 0.045f;

        public static readonly Color KnotColor = new(1f, 0.85f, 0.2f);
        public static readonly Color HoverColor = new(1f, 0.55f, 0.1f);
        public static readonly Color TangentColor = new(0.45f, 0.85f, 1f);

        // «+» — зелёный на тёмном кружке: «добавить», не путается с жёлтой линией, на которой стоит.
        public static readonly Color InsertColor = new(0.45f, 1f, 0.45f);

        private static readonly Color LineColor = new(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color InsertBackColor = new(0.1f, 0.1f, 0.1f, 0.9f);


        // ── Линия и точки ────────────────────────────────────────────────

        // Весь контур линией — и стороны, которые не строятся.
        public static void DrawContourLines(Component owner)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            using (new Handles.DrawingScope(LineColor, owner.transform.localToWorldMatrix))
                foreach (var path in LiveBuilderPathOwners.PathsOf(owner))
                    Handles.DrawAAPolyLine(2f, new List<Vector3>(path.Points).ToArray());
        }


        // Угол — квадрат, плавная — шарик, Безье — ромб (квадрат, повёрнутый на ребро).
        public static void DrawKnot(int control, Vector3 position, float size, Color color, LiveBuilderKnotShape shape)
        {
            Handles.color = color;

            switch (shape)
            {
                case LiveBuilderKnotShape.Smooth:
                    Handles.SphereHandleCap(control, position, Quaternion.identity, size * 1.6f, EventType.Repaint);
                    break;

                case LiveBuilderKnotShape.Bezier:
                    var camera = Camera.current;
                    var rotation = camera != null ? camera.transform.rotation * Quaternion.Euler(0f, 0f, 45f) : Quaternion.Euler(0f, 0f, 45f);
                    Handles.CubeHandleCap(control, position, rotation, size * 1.3f, EventType.Repaint);
                    break;

                default:
                    Handles.DotHandleCap(control, position, Quaternion.identity, size, EventType.Repaint);
                    break;
            }
        }


        public static void DrawSelectedKnots(Component owner, SplineContainer container)
        {
            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                var spline = container.Splines[splineIndex];
                var position = owner.transform.TransformPoint(spline[knotIndex].Position);
                DrawKnot(0, position, HandleUtility.GetHandleSize(position) * KnotSize, Color.white, LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knotIndex)));
            }
        }


        // «+» лицом к камере: плашмя на полу его не видно, если смотришь сбоку. Тёмный кружок закрывает линию под ним — кнопка отдельно от линии.
        public static void DrawInsert(Vector3 position, float size, Color color)
        {
            var facing = Camera.current != null ? Camera.current.transform.rotation : Quaternion.identity;
            var normal = facing * Vector3.forward;
            var right = facing * Vector3.right * (size * 0.6f);
            var up = facing * Vector3.up * (size * 0.6f);

            Handles.color = InsertBackColor;
            Handles.DrawSolidDisc(position, normal, size);
            Handles.color = color;
            Handles.DrawWireDisc(position, normal, size, 2f);
            Handles.DrawLine(position - right, position + right, 3f);
            Handles.DrawLine(position - up, position + up, 3f);
        }


        // ── Усы Безье ────────────────────────────────────────────────────

        // Пока тянут — усы выбранных точек Безье, без ручек.
        public static void DrawTangentLines(Component owner, SplineContainer container)
        {
            Handles.color = TangentColor;

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                var spline = container.Splines[splineIndex];

                if (LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knotIndex)) != LiveBuilderKnotShape.Bezier)
                    continue;

                var knot = spline[knotIndex];
                var position = owner.transform.TransformPoint(knot.Position);
                Handles.DrawLine(position, TangentTipOf(owner, knot, BezierTangent.In), 2f);
                Handles.DrawLine(position, TangentTipOf(owner, knot, BezierTangent.Out), 2f);
            }
        }


        // Конец уса в мире: ус хранится в осях самой точки — с её поворотом.
        public static Vector3 TangentTipOf(Component owner, BezierKnot knot, BezierTangent tangent)
        {
            var local = (Vector3)(knot.Position + math.rotate(knot.Rotation, tangent == BezierTangent.Out ? knot.TangentOut : knot.TangentIn));
            return owner.transform.TransformPoint(local);
        }


        // ── Подписи ──────────────────────────────────────────────────────

        // Пока тянешь — длины обеих сторон у точки, за которую тянешь.
        public static void DrawSideLengths(Component owner, SplineContainer container, (int Spline, int Knot) grabbedKnot)
        {
            var spline = container.Splines[grabbedKnot.Spline];
            var segmentCount = spline.Closed ? spline.Count : spline.Count - 1;

            foreach (var segment in new[] { grabbedKnot.Knot - 1, grabbedKnot.Knot })
            {
                var wrapped = spline.Closed ? (segment + spline.Count) % spline.Count : segment;

                if (wrapped < 0 || wrapped >= segmentCount)
                    continue;

                var curve = spline.GetCurve(wrapped);
                var middle = owner.transform.TransformPoint((Vector3)CurveUtility.EvaluatePosition(curve, 0.5f));
                BoxModelSceneText.DrawLabel(middle, BoxModelSceneText.FormatMeters(CurveUtility.ApproximateLength(curve)) + " м");
            }
        }
    }
}
