using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общее у срезов рёбер объекта из коробок — фаски (<see cref="BoxModelBevel"/>) и скосов (<see cref="BoxModelChamferCutter"/>, <see cref="BoxModelChamferHandles"/>):
    /// запас призмы наружу, ось как вектор, габарит призмы среза. Сами срезы — у них.
    /// </summary>
    public static class BoxModelEdgeCutting
    {
        // Призма среза выходит за грани наружу на столько, м: срез вровень с гранью оставил бы плёнку.
        public const float OutsideMargin = 0.01f;


        // Единичный вектор вдоль оси объекта: 0 — X, 1 — Y, 2 — Z.
        public static Vector3 AxisVector(int axis)
        {
            var vector = Vector3.zero;
            vector[axis] = 1f;
            return vector;
        }


        // Габарит призмы: сечение, протянутое по Z от 0 до length, в осях меша.
        public static Bounds BoundsOf(List<Vector2> crossSection, float length, Matrix4x4 edgeToMesh)
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
