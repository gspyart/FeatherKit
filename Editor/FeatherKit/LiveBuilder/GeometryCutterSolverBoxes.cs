using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вырез-коробку (<see cref="GeometryCutter"/>) по осям объекта переводит в коробки солвера этого объекта — так она режет его точно, как вырезающая коробка.
    /// - чей это кусок: ближайший объект из коробок над вырезом, если путь к нему не идёт через стену по линии
    /// - годится коробка без скруглений, чьи оси лежат вдоль осей объекта (поворот на 90° можно), объект — на новом правиле вырезов
    /// - угол — в осях выреза (сдвиг − половина), потом в осях объекта; материалы сторон выреза — на стороны коробки в осях объекта
    /// </summary>
    public static class GeometryCutterSolverBoxes
    {
        // Насколько ось выреза может отклониться от оси объекта — доля её длины.
        private const float AxisTolerance = 1e-4f;

        // Масштаб ближе этого к 1 — ровно 1: иначе размер коробки уходил бы на миллиметр от размера выреза.
        private const float UnitScaleTolerance = 1e-5f;

        // Радиус угла меньше этого — угол прямой: так же его рисует контур выреза.
        private const float SquareCornerRadius = 0.001f;


        // ── Чей кусок ────────────────────────────────────────────────────

        // Объект из коробок, чьим куском вырез собирается; в стене по линии или вне объектов — null.
        public static BoxModel CollectingModelOf(GeometryCutter cutter)
        {
            for (var node = cutter.transform.parent; node != null; node = node.parent)
            {
                if (node.TryGetComponent<BoxModel>(out var model))
                    return model;

                if (node.GetComponent<SplineWall>() != null)
                    return null;
            }

            return null;
        }


        // Режет ли вырез свой объект клетками солвера; model — объект, который о нём спрашивает.
        public static bool IsSolverPieceOf(GeometryCutter cutter, BoxModel model)
        {
            return model != null && cutter.isActiveAndEnabled && CollectingModelOf(cutter) == model && IsCollectedBy(model, cutter);
        }


        public static bool IsSolverPiece(GeometryCutter cutter)
        {
            return IsSolverPieceOf(cutter, CollectingModelOf(cutter));
        }


        // Для сборщика, который уже дошёл до выреза по своим кускам: на старом правиле вырезы режут только вычитанием.
        public static bool IsCollectedBy(BoxModel model, GeometryCutter cutter)
        {
            return cutter.enabled && !LiveBuilderCutRules.IsLegacy(model) && IsBoxAlongAxesOf(cutter, model);
        }


        // Коробка без скруглений, чьи оси лежат вдоль осей объекта, — её можно превратить в коробку объекта и обратно.
        public static bool IsBoxAlongAxesOf(GeometryCutter cutter, BoxModel model)
        {
            return model != null && IsBoxAlongAxesOf(cutter, model.transform);
        }


        // То же в осях любой постройки — стены по линии, в которой стоит дверь.
        public static bool IsBoxAlongAxesOf(GeometryCutter cutter, Transform space)
        {
            return IsBoxShape(cutter) && TryGetAxes(cutter, space, out _);
        }

        private static bool IsBoxShape(GeometryCutter cutter)
        {
            return cutter.Shape == GeometryCutterShape.Box && cutter.TopCornerRadius <= SquareCornerRadius && cutter.BottomCornerRadius <= SquareCornerRadius;
        }


        // ── Коробки ──────────────────────────────────────────────────────

        // Коробка солвера в осях объекта (не по осям — пусто); offset — сдвиг копии повтора над ним, м в осях объекта.
        public static List<MillimeterBox> BoxesOf(GeometryCutter cutter, BoxModel model, Vector3 offset)
        {
            return BoxesOf(cutter, model.transform, offset);
        }


        // То же в осях любой постройки (space): стена по линии режет дверь-ребёнка проёмом по линии.
        public static List<MillimeterBox> BoxesOf(GeometryCutter cutter, Transform space, Vector3 offset)
        {
            var boxes = new List<MillimeterBox>(1);

            if (!TryGetAxes(cutter, space, out var axes))
                return boxes;

            var size = new Vector3(cutter.Width, cutter.Height, cutter.Depth);
            var cornerInCutter = cutter.ShapeOffset - new Vector3(cutter.Width / 2f, 0f, cutter.Depth / 2f);
            var modelSize = Vector3.zero;

            for (var axis = 0; axis < 3; axis++)
                modelSize[axes[axis].ModelAxis] = size[axis] * axes[axis].Scale;

            var min = space.InverseTransformPoint(cutter.transform.TransformPoint(cornerInCutter));

            // Угол выреза лежит на дальней стороне той оси объекта, вдоль которой его ось смотрит назад.
            for (var axis = 0; axis < 3; axis++)
                if (axes[axis].IsReversed)
                    min[axes[axis].ModelAxis] -= modelSize[axes[axis].ModelAxis];

            boxes.Add(MillimeterBox.FromMeters(min + offset, modelSize));
            return boxes;
        }


        // ── Материалы ────────────────────────────────────────────────────

        // Материалы и подгонки текстуры сторон выреза — на стороны коробки в осях объекта. Общие у выреза — общие у коробки.
        public static BoxModelSideMaterials SideMaterialsIn(GeometryCutter cutter, BoxModel model)
        {
            var byModelSide = new Material[BoxFaces.Count];
            var fitsByModelSide = new LiveBuilderTextureFit[BoxFaces.Count];
            var fits = cutter.TextureFits;
            var hasAxes = TryGetAxes(cutter, model.transform, out var axes);

            for (var cutterSide = 0; cutterSide < BoxFaces.Count; cutterSide++)
            {
                var modelSide = hasAxes ? ModelSideOf(axes, cutterSide) : cutterSide;
                fitsByModelSide[modelSide] = fits.IsEmpty ? null : OwnFitOf(fits, cutterSide);

                if (hasAxes)
                    byModelSide[modelSide] = OwnMaterialOf(cutter, cutterSide);
            }

            var sideFits = fits.IsEmpty
                ? default
                : new BoxModelSideFits(fits.Get(BoxModelSideSlot.All), fitsByModelSide[3], fitsByModelSide[2], null,
                    fitsByModelSide[0], fitsByModelSide[1], fitsByModelSide[4], fitsByModelSide[5]);
            return new BoxModelSideMaterials(cutter.RevealMaterial, byModelSide[3], byModelSide[2], null,
                byModelSide[0], byModelSide[1], byModelSide[4], byModelSide[5]).WithFits(sideFits);
        }


        // Своя подгонка стороны выреза без общей: у стен — своя или всех стен. Общая — у всей коробки.
        public static LiveBuilderTextureFit OwnFitOf(LiveBuilderTextureFitList<BoxModelSideSlot> fits, int cutterSide)
        {
            var own = fits.Get(BoxModelSideFits.SlotOfSide(cutterSide));
            return own ?? (BoxFaces.Axis(cutterSide) == 1 ? null : fits.Get(BoxModelSideSlot.Sides));
        }


        // Сторона выреза, которая в объекте лежит стороной modelSide (номера — BoxFaces). Вырез не по осям объекта — та же.
        public static int CutterSideOf(GeometryCutter cutter, BoxModel model, int modelSide)
        {
            if (!TryGetAxes(cutter, model.transform, out var axes))
                return modelSide;

            for (var cutterSide = 0; cutterSide < BoxFaces.Count; cutterSide++)
                if (ModelSideOf(axes, cutterSide) == modelSide)
                    return cutterSide;

            return modelSide;
        }


        // Свой материал стороны выреза: у пола и потолка — свой, у стен — свой или всех стен. Пусто — общий материал выреза.
        public static Material OwnMaterialOf(GeometryCutter cutter, int cutterSide)
        {
            var sides = cutter.SideMaterials;

            return cutterSide switch
            {
                0 => sides.Right != null ? sides.Right : sides.Sides,
                1 => sides.Left != null ? sides.Left : sides.Sides,
                2 => sides.Top,
                3 => sides.Bottom,
                4 => sides.Front != null ? sides.Front : sides.Sides,
                _ => sides.Back != null ? sides.Back : sides.Sides
            };
        }


        // Есть ли у выреза свой материал стенок — общий или хоть одной стороны.
        public static bool HasOwnMaterials(GeometryCutter cutter)
        {
            return cutter.RevealMaterial != null || cutter.SideMaterials.HasAny;
        }


        // ── Оси ──────────────────────────────────────────────────────────

        // Одна ось выреза в осях объекта: вдоль какой оси объекта, в какую сторону и с каким масштабом.
        private readonly struct CutterAxisInModel
        {
            public CutterAxisInModel(int modelAxis, bool isReversed, float scale)
            {
                ModelAxis = modelAxis;
                IsReversed = isReversed;
                Scale = scale;
            }


            public int ModelAxis { get; }
            public bool IsReversed { get; }
            public float Scale { get; }
        }

        // Каждая ось выреза — вдоль своей оси объекта; иначе вырез повёрнут и точно резать его нельзя.
        private static bool TryGetAxes(GeometryCutter cutter, Transform space, out CutterAxisInModel[] axes)
        {
            axes = new CutterAxisInModel[3];
            var cutterToModel = space.worldToLocalMatrix * cutter.transform.localToWorldMatrix;
            var usedModelAxes = 0;

            for (var axis = 0; axis < 3; axis++)
            {
                var column = (Vector3)cutterToModel.GetColumn(axis);
                var length = column.magnitude;

                if (length < 1e-6f)
                    return false;

                var modelAxis = LargestAxisOf(column);

                for (var other = 0; other < 3; other++)
                    if (other != modelAxis && Mathf.Abs(column[other]) > length * AxisTolerance)
                        return false;

                if ((usedModelAxes & (1 << modelAxis)) != 0)
                    return false;

                usedModelAxes |= 1 << modelAxis;
                var scale = Mathf.Abs(length - 1f) < UnitScaleTolerance ? 1f : length;
                axes[axis] = new CutterAxisInModel(modelAxis, column[modelAxis] < 0f, scale);
            }

            return true;
        }

        private static int LargestAxisOf(Vector3 vector)
        {
            var absolute = new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            return absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2;
        }

        private static int ModelSideOf(CutterAxisInModel[] axes, int cutterSide)
        {
            var axis = axes[BoxFaces.Axis(cutterSide)];
            return BoxFaces.Index(axis.ModelAxis, BoxFaces.IsPositive(cutterSide) != axis.IsReversed);
        }
    }
}
