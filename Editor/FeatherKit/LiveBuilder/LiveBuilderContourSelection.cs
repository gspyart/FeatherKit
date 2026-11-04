using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Вид точки контура: угол — прямые отрезки; плавно — кривая проходит через точку сама; Безье — кривую гнут ручками-усами.</summary>
    public enum LiveBuilderKnotShape
    {
        Corner,
        Smooth,
        Bezier
    }


    /// <summary>
    /// Какие точки контура выбраны — общее для ручек точек в окне сцены (<see cref="LiveBuilderContourHandles"/>) и окна Live Builder:
    /// в сцене точки выбирают и тянут, в окне меняют их вид — угол, плавно, Безье. Сменился контур (выделили другое) — выбор снимается.
    /// Тут же — как вид точки ложится на режим ручек сплайна Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderContourSelection
    {
        private static readonly List<(int Spline, int Knot)> SelectedKnots = new();


        // Ctrl+Z мог убрать выбранные точки — номера чистятся сразу, а не при следующей отрисовке сцены.
        static LiveBuilderContourSelection()
        {
            Undo.undoRedoPerformed += PruneAfterUndo;
        }


        // Выбор поменялся — окно перерисовывается.
        public static event System.Action Changed;


        // Чей контур правят; null — ничей.
        public static Component Owner { get; private set; }

        public static IReadOnlyList<(int Spline, int Knot)> Knots => SelectedKnots;


        // ── Выбор ────────────────────────────────────────────────────────

        // Показан контур этого владельца; другой — выбор снимается.
        public static void SetOwner(Component owner)
        {
            if (owner == Owner)
                return;

            Owner = owner;
            SelectedKnots.Clear();
            Changed?.Invoke();
        }


        public static bool Contains(int splineIndex, int knotIndex)
        {
            return SelectedKnots.Contains((splineIndex, knotIndex));
        }


        // Только эта точка.
        public static void SelectOnly(int splineIndex, int knotIndex)
        {
            SelectedKnots.Clear();
            SelectedKnots.Add((splineIndex, knotIndex));
            Changed?.Invoke();
        }


        // Добавить к выбору или убрать из него.
        public static void Toggle(int splineIndex, int knotIndex)
        {
            if (!SelectedKnots.Remove((splineIndex, knotIndex)))
                SelectedKnots.Add((splineIndex, knotIndex));

            Changed?.Invoke();
        }


        public static void Add(int splineIndex, int knotIndex)
        {
            if (SelectedKnots.Contains((splineIndex, knotIndex)))
                return;

            SelectedKnots.Add((splineIndex, knotIndex));
            Changed?.Invoke();
        }


        public static void Clear()
        {
            if (SelectedKnots.Count == 0)
                return;

            SelectedKnots.Clear();
            Changed?.Invoke();
        }


        // Ctrl+Z мог убрать выбранные точки. Возвращает, пришлось ли что-то убрать.
        public static bool Prune(SplineContainer container)
        {
            var removed = SelectedKnots.RemoveAll(entry => !IsKnotInside(container, entry.Spline, entry.Knot));

            if (removed > 0)
                Changed?.Invoke();

            return removed > 0;
        }

        private static void PruneAfterUndo()
        {
            if (Owner != null && Owner.TryGetComponent<SplineContainer>(out var container))
                Prune(container);
        }


        // Есть ли ещё такая точка в контуре: после Ctrl+Z и Delete номера выбранных могут указывать в пустоту.
        public static bool IsKnotInside(SplineContainer container, int splineIndex, int knotIndex)
        {
            return splineIndex >= 0 && splineIndex < container.Splines.Count && knotIndex >= 0 && knotIndex < container.Splines[splineIndex].Count;
        }


        // ── Вид точек ────────────────────────────────────────────────────

        public static LiveBuilderKnotShape ShapeOf(TangentMode mode)
        {
            return mode switch
            {
                TangentMode.Linear => LiveBuilderKnotShape.Corner,
                TangentMode.AutoSmooth => LiveBuilderKnotShape.Smooth,
                _ => LiveBuilderKnotShape.Bezier
            };
        }


        // Следующий вид по кругу: угол → плавно → Безье → угол.
        public static LiveBuilderKnotShape Next(LiveBuilderKnotShape shape)
        {
            return shape switch
            {
                LiveBuilderKnotShape.Corner => LiveBuilderKnotShape.Smooth,
                LiveBuilderKnotShape.Smooth => LiveBuilderKnotShape.Bezier,
                _ => LiveBuilderKnotShape.Corner
            };
        }


        // Общий вид точек, к которым применится переключатель окна (выбранные или все); разный — −1.
        public static int CommonShapeOf(SplineContainer container)
        {
            var shape = -1;

            foreach (var (splineIndex, knotIndex) in TargetKnotsOf(container))
            {
                var knotShape = (int)ShapeOf(container.Splines[splineIndex].GetTangentMode(knotIndex));

                if (shape >= 0 && shape != knotShape)
                    return -1;

                shape = knotShape;
            }

            return shape;
        }


        // Вид выбранным точкам этого контура, а если не выбрано ни одной — всем. С Undo.
        public static void ApplyShape(SplineContainer container, LiveBuilderKnotShape shape)
        {
            Undo.RecordObject(container, "Change Contour Points");

            foreach (var (splineIndex, knotIndex) in TargetKnotsOf(container))
                SetShape(container.Splines[splineIndex], knotIndex, shape);
        }


        // Вид одной точке. Безье из угла — сначала плавно: так у ручек сразу есть длина и направление, а не ноль.
        public static void SetShape(Spline spline, int knotIndex, LiveBuilderKnotShape shape)
        {
            switch (shape)
            {
                case LiveBuilderKnotShape.Corner:
                    spline.SetTangentMode(knotIndex, TangentMode.Linear);
                    break;

                case LiveBuilderKnotShape.Smooth:
                    spline.SetTangentMode(knotIndex, TangentMode.AutoSmooth);
                    break;

                default:
                    if (spline.GetTangentMode(knotIndex) == TangentMode.Linear)
                        spline.SetTangentMode(knotIndex, TangentMode.AutoSmooth);

                    spline.SetTangentMode(knotIndex, TangentMode.Mirrored);
                    break;
            }
        }


        // Выбранные точки этого контура, а если не выбрано ни одной — все его точки.
        public static List<(int Spline, int Knot)> TargetKnotsOf(SplineContainer container)
        {
            var isOwnSelection = Owner != null && Owner.GetComponent<SplineContainer>() == container && SelectedKnots.Count > 0;

            if (isOwnSelection)
                return SelectedKnots.FindAll(entry => IsKnotInside(container, entry.Spline, entry.Knot));

            var all = new List<(int Spline, int Knot)>();

            for (var splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                for (var knotIndex = 0; knotIndex < container.Splines[splineIndex].Count; knotIndex++)
                    all.Add((splineIndex, knotIndex));

            return all;
        }
    }
}
