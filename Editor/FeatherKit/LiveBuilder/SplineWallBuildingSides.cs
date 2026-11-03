using System.Collections.Generic;
using System.Text;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Стена-комната в здании (<see cref="SplineWall.SkipsSidesAlongBuildingWalls"/>): какие её стороны легли на стены здания и не строятся.
    /// Сторона на стене — если её начало, середина и конец ближе допуска к внутренней стороне стен здания.
    /// Линия стены при этом не меняется: сдвинул точку комнаты или стену здания — стороны пересчитаются при следующей пересборке.
    /// </summary>
    public static class SplineWallBuildingSides
    {
        // Сторона ближе этого к стене здания — лежит на стене, м.
        public const float OnWallTolerance = 0.05f;


        // Пути стены, которые надо построить: у комнаты в здании — без сторон на его стенах.
        public static List<SplineWallPath> PathsToBuild(SplineWall wall)
        {
            var paths = SplineWallPath.FromWall(wall);

            if (!TryGetBuilding(wall, out var building))
                return paths;

            var contours = SplineBuildingMeshBuilder.InnerContoursOf(building);
            var wallToBuilding = building.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
            var result = new List<SplineWallPath>();

            foreach (var path in paths)
                result.AddRange(path.SplitWithout(segment => IsAlongContours(wallToBuilding.MultiplyPoint3x4(path.Points[segment]),
                    wallToBuilding.MultiplyPoint3x4(path.Points[segment + 1]), contours)));

            return result;
        }


        // Сторона от start до end (в осях здания) лежит на стенах здания: начало, середина и конец — у внутренней стороны стен.
        public static bool IsAlongContours(Vector3 start, Vector3 end, List<List<Vector2>> contours)
        {
            foreach (var fraction in new[] { 0f, 0.5f, 1f })
            {
                var point = Vector3.Lerp(start, end, fraction);

                if (PlanPolygons.DistanceToContours(new Vector2(point.x, point.z), contours) >= OnWallTolerance)
                    return false;
            }

            return true;
        }


        // Для подписи стены: от чего у здания зависят пропущенные стороны — его контур, стены и где стоит стена относительно него.
        public static string BuildingKeyOf(SplineWall wall)
        {
            if (!TryGetBuilding(wall, out var building))
                return "";

            var key = new StringBuilder();
            key.Append(building.WallThickness).Append(',').Append((int)building.ContourSide).Append(',').Append(building.CurveStep).Append(',').Append(building.CornerSegments)
                .Append(building.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix);

            foreach (var spline in building.GetComponent<SplineContainer>().Splines)
                for (var index = 0; index < spline.Count; index++)
                    key.Append(';').Append(spline[index].Position).Append(spline[index].TangentIn).Append(spline[index].TangentOut)
                        .Append((int)spline.GetTangentMode(index)).Append('r').Append(LiveBuilderCornerRadii.RadiusOf(spline, index));

            return key.ToString();
        }


        // Здание, вдоль чьих стен стороны пропускаются: стена-комната в привязке к нему.
        private static bool TryGetBuilding(SplineWall wall, out SplineBuilding building)
        {
            building = null;

            if (!wall.SkipsSidesAlongBuildingWalls || !wall.TryGetComponent<SplineBuildingStoreyLink>(out var link) || !link.IsValid)
                return false;

            building = link.Building;
            return true;
        }
    }
}
