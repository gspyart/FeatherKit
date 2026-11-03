using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Куда встают стены комнаты от нарисованного (<see cref="LiveBuilderRoomWallSide"/>) — в понятиях каждой постройки:
    /// у прямоугольной комнаты — на сколько её внутренний размер отступает от нарисованного, у замкнутой стены по линии — с какой стороны линии стена,
    /// у комнаты-здания — чем считается её контур.
    /// </summary>
    public static class LiveBuilderRoomWalls
    {
        // Насколько внутренний размер комнаты отступает от нарисованного с каждой стороны, м: наружу — ничуть, внутрь — на толщину, по центру — на половину.
        public static float InsetOf(LiveBuilderRoomWallSide side, float wallThickness)
        {
            return side switch
            {
                LiveBuilderRoomWallSide.Inward => wallThickness,
                LiveBuilderRoomWallSide.Centered => wallThickness / 2f,
                _ => 0f
            };
        }


        // С какой стороны замкнутой линии встаёт стена. Обход против часовой стрелки сверху — комната слева от линии, по часовой — справа.
        public static SplineWallAlignment AlignmentFor(Spline spline, LiveBuilderRoomWallSide side)
        {
            if (side == LiveBuilderRoomWallSide.Centered || spline.Count < 3)
                return SplineWallAlignment.Center;

            var plan = new List<Vector2>(spline.Count);

            foreach (var knot in spline.Knots)
                plan.Add(new Vector2(knot.Position.x, knot.Position.z));

            var roomSide = PlanPolygons.SignedArea(plan) > 0f ? SplineWallAlignment.Left : SplineWallAlignment.Right;
            var otherSide = roomSide == SplineWallAlignment.Left ? SplineWallAlignment.Right : SplineWallAlignment.Left;
            return side == LiveBuilderRoomWallSide.Inward ? roomSide : otherSide;
        }


        // Чем считается контур комнаты-здания: стены наружу — контур это комната изнутри, внутрь — габарит, по центру — ось стен.
        public static SplineBuildingContourSide ContourSideFor(LiveBuilderRoomWallSide side)
        {
            return side switch
            {
                LiveBuilderRoomWallSide.Inward => SplineBuildingContourSide.Outside,
                LiveBuilderRoomWallSide.Centered => SplineBuildingContourSide.Center,
                _ => SplineBuildingContourSide.Inside
            };
        }
    }
}
