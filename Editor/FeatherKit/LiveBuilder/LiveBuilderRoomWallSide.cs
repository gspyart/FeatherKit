namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Куда встают стены комнаты от нарисованного: наружу — нарисовал саму комнату, внутрь — её габарит вместе со стенами, по центру — ось стен.</summary>
    public enum LiveBuilderRoomWallSide
    {
        Outward,
        Inward,
        Centered
    }
}
