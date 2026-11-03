using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общее у сверок перед пересборкой — привязки к этажам (<see cref="SplineBuildingLinkSync"/>), места на линии (<see cref="HoleCutterLineSync"/>),
    /// точек перегородок на стенах здания (<see cref="SplineWallBuildingAnchors"/>):
    /// - правка без Undo: сверка повторит её сама после любого Ctrl+Z; у экземпляра префаба — записана как его правка, иначе потеряется
    /// - подпись линии: поменялась хоть одна точка — другое число, так видно, что линию двигали
    /// - место на линии: ближайшее место — номер точки с долей до следующей, и точка с осями линии в этом месте
    /// </summary>
    public static class LiveBuilderSyncEdits
    {
        public static void Apply(Object target, System.Action change)
        {
            change();
            EditorUtility.SetDirty(target);
            RecordPrefabInstance(target);
        }


        public static void RecordPrefabInstance(Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }


        // Числа всех точек линии разом. Ноль не бывает — он значит «ещё не сверяли».
        public static int LineHashOf(SplineContainer container)
        {
            var hash = new System.HashCode();

            foreach (var spline in container.Splines)
            {
                hash.Add(spline.Closed);

                for (var index = 0; index < spline.Count; index++)
                {
                    hash.Add(spline[index].Position);
                    hash.Add(spline[index].TangentIn);
                    hash.Add(spline[index].TangentOut);
                    hash.Add((int)spline.GetTangentMode(index));
                }
            }

            var result = hash.ToHashCode();
            return result != 0 ? result : 1;
        }


        // ── Место на линии ───────────────────────────────────────────────

        // Ближайшее к точке место на линии, в плане: сплайн и номер точки с долей до следующей (2,4 — между второй и третьей).
        public static void NearestPlace(SplineContainer container, Vector3 localPosition, out int splineIndex, out float place)
        {
            var point = new float3(localPosition.x, 0f, localPosition.z);
            var bestDistance = float.MaxValue;
            splineIndex = 0;
            place = 0f;

            for (var index = 0; index < container.Splines.Count; index++)
            {
                var spline = container.Splines[index];

                if (spline.Count == 0)
                    continue;

                var distance = SplineUtility.GetNearestPoint(spline, point, out _, out var normalized);

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                splineIndex = index;
                place = spline.ConvertIndexUnit(normalized, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            }
        }


        // Точка линии в этом месте и её оси: X — вдоль линии в плане, Y — вверх, Z — поперёк. В осях владельца линии.
        public static void FrameAt(SplineContainer container, int splineIndex, float place, out Vector3 point, out Quaternion rotation)
        {
            var spline = container.Splines[splineIndex];
            var normalized = spline.ConvertIndexUnit(place, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            point = (Vector3)spline.EvaluatePosition(normalized);

            var along = (Vector3)spline.EvaluateTangent(normalized);
            along.y = 0f;

            if (along.sqrMagnitude < 1e-8f)
                along = Vector3.right;

            rotation = Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up), Vector3.up);
        }
    }
}
