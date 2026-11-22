
namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Куда встают стены комнаты от нарисованного (<see cref="LiveBuilderRoomWallSide"/>): на сколько её внутренний размер отступает от прямоугольника.</summary>
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
    }
}
