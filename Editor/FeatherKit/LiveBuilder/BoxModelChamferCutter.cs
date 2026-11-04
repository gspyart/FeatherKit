using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Срезает скосы рёбер (<see cref="BoxModelEdgeChamfer"/>) из готового меша объекта из коробок — как фаска (<see cref="BoxModelBevel"/>),
    /// только у одного ребра одной коробки и на свои размеры: из меша вычитается треугольная призма вдоль ребра (<see cref="MeshSubtraction"/>).
    /// Только у добавляющих коробок: скос срезает объём, а вырезающей пришлось бы его добавлять.
    /// Режет готовый меш, поэтому срежет и чужую коробку, если та стоит в том же месте. Новые грани — материала граней рядом, развёртка в метрах.
    /// </summary>
    public static class BoxModelChamferCutter
    {
        // Призма выходит за грани наружу на столько, м: срез вровень с гранью оставил бы плёнку.
        private const float OutsideMargin = 0.01f;


        public static void Apply(Mesh mesh, IReadOnlyList<BoxModelSolverBox> boxes)
        {
            if (mesh.vertexCount == 0 || !HasAnyChamfer(boxes))
                return;

            var polygons = MeshSubtraction.Read(mesh);

            foreach (var box in boxes)
            {
                if (box.Mode != BoxModelPartMode.Add)
                    continue;

                var min = (Vector3)box.Box.Min / MillimeterBox.MillimetersPerMeter;
                var max = (Vector3)box.Box.Max / MillimeterBox.MillimetersPerMeter;

                foreach (var chamfer in box.Chamfers)
                    polygons = Cut(polygons, chamfer, min, max);
            }

            MeshSubtraction.Write(mesh, polygons, mesh.subMeshCount);
        }

        private static bool HasAnyChamfer(IReadOnlyList<BoxModelSolverBox> boxes)
        {
            foreach (var box in boxes)
                if (box.Mode == BoxModelPartMode.Add && box.Chamfers.Count > 0)
                    return true;

            return false;
        }


        // ── Один скос ────────────────────────────────────────────────────

        // Оси сечения: x — поперёк второй стороны (по её оси), y — поперёк первой, z — вдоль ребра. Угол ребра — в нуле.
        private static List<MeshBooleanPolygon> Cut(List<MeshBooleanPolygon> polygons, BoxModelEdgeChamfer chamfer, Vector3 min, Vector3 max)
        {
            var firstAxis = BoxFaces.Axis(chamfer.FirstFace);
            var secondAxis = BoxFaces.Axis(chamfer.SecondFace);

            if (!chamfer.HasCut || firstAxis == secondAxis)
                return polygons;

            var alongAxis = 3 - firstAxis - secondAxis;
            var size = max - min;
            var sizeOnFirst = Mathf.Min(chamfer.SizeOnFirstFace, size[secondAxis]);
            var sizeOnSecond = Mathf.Min(chamfer.SizeOnSecondFace, size[firstAxis]);
            var crossSection = CrossSectionOf(BoxFaces.IsPositive(chamfer.FirstFace) ? 1f : -1f, BoxFaces.IsPositive(chamfer.SecondFace) ? 1f : -1f,
                sizeOnFirst, sizeOnSecond);

            var edgeToMesh = EdgeToMesh(chamfer, firstAxis, secondAxis, alongAxis, min, max);
            var length = size[alongAxis];

            return MeshSubtraction.SubtractNear(polygons, BoundsOf(crossSection, length, edgeToMesh),
                near => MeshSubtraction.PrismPolygons(crossSection, 0f, length, edgeToMesh, MeshSubtraction.MostCommonSubmesh(near)));
        }


        // Сечение призмы: наружный угол с запасом, срез от точки на первой стороне до точки на второй — продлён за них на запас.
        // Первая точка — наружный угол: из неё видно всё сечение, крышка призмы — веер из неё. Обход — против часовой.
        private static List<Vector2> CrossSectionOf(float firstSign, float secondSign, float sizeOnFirst, float sizeOnSecond)
        {
            var onFirst = new Vector2(-secondSign * sizeOnFirst, 0f);
            var onSecond = new Vector2(0f, -firstSign * sizeOnSecond);
            var cutDirection = (onFirst - onSecond).normalized;
            var onFirstExtended = onFirst + cutDirection * OutsideMargin;
            var onSecondExtended = onSecond - cutDirection * OutsideMargin;

            var crossSection = new List<Vector2>
            {
                new(secondSign * OutsideMargin, firstSign * OutsideMargin),
                new(secondSign * OutsideMargin, onSecondExtended.y),
                onSecondExtended,
                onFirstExtended,
                new(onFirstExtended.x, firstSign * OutsideMargin)
            };

            if (PlanPolygons.SignedArea(crossSection) < 0f)
                crossSection.Reverse(1, crossSection.Count - 1);

            return crossSection;
        }


        // Оси сечения в оси объекта: начало — угол ребра у одного из краёв вдоль него.
        // Оси не зеркальные: иначе призма вывернется наизнанку и вычитание съест не то. Зеркальные — вдоль ребра идём от большего края к меньшему.
        private static Matrix4x4 EdgeToMesh(BoxModelEdgeChamfer chamfer, int firstAxis, int secondAxis, int alongAxis, Vector3 min, Vector3 max)
        {
            var origin = min;
            origin[firstAxis] = BoxFaces.IsPositive(chamfer.FirstFace) ? max[firstAxis] : min[firstAxis];
            origin[secondAxis] = BoxFaces.IsPositive(chamfer.SecondFace) ? max[secondAxis] : min[secondAxis];

            var x = AxisVector(secondAxis);
            var y = AxisVector(firstAxis);
            var along = AxisVector(alongAxis);
            var isMirrored = Vector3.Dot(Vector3.Cross(x, y), along) < 0f;

            if (isMirrored)
            {
                along = -along;
                origin[alongAxis] = max[alongAxis];
            }

            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, x);
            matrix.SetColumn(1, y);
            matrix.SetColumn(2, along);
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return matrix;
        }

        private static Vector3 AxisVector(int axis)
        {
            var vector = Vector3.zero;
            vector[axis] = 1f;
            return vector;
        }


        private static Bounds BoundsOf(List<Vector2> crossSection, float length, Matrix4x4 edgeToMesh)
        {
            var bounds = new Bounds(edgeToMesh.MultiplyPoint3x4(crossSection[0]), Vector3.zero);

            foreach (var point in crossSection)
            {
                bounds.Encapsulate(edgeToMesh.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f)));
                bounds.Encapsulate(edgeToMesh.MultiplyPoint3x4(new Vector3(point.x, point.y, length)));
            }

            return bounds;
        }
    }
}
