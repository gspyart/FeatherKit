using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Точка стены по линии (<see cref="SplineWall"/>), которая держится за стену здания: какая это точка и за что держится —
    /// за внутренний угол здания (номер точки контура) или за его стену (номер отрезка контура от точки до следующей).
    /// Двигаешь стену здания — точка в углу встаёт в новый угол, точка на стене — на ту же стену напротив себя. Пишет редактор.
    /// </summary>
    [System.Serializable]
    public struct SplineWallBuildingAnchor
    {
        [SerializeField] private int spline;
        [SerializeField] private int knot;
        [SerializeField] private int buildingSpline;
        [SerializeField] private int buildingKnot;
        [SerializeField] private bool isAtCorner;


        public SplineWallBuildingAnchor(int spline, int knot, int buildingSpline, int buildingKnot, bool isAtCorner)
        {
            this.spline = spline;
            this.knot = knot;
            this.buildingSpline = buildingSpline;
            this.buildingKnot = buildingKnot;
            this.isAtCorner = isAtCorner;
        }


        public int Spline => spline;

        public int Knot => knot;

        public int BuildingSpline => buildingSpline;

        // В углу — номер точки контура здания; на стене — номер отрезка контура, от этой точки до следующей.
        public int BuildingKnot => buildingKnot;

        public bool IsAtCorner => isAtCorner;
    }
}
