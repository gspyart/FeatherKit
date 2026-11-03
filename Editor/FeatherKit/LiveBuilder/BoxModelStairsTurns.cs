using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Направление подъёма готовой лестницы — внутри её коробки, как в ProBuilder: коробка стоит на месте, ступени перестраиваются под неё.
    /// Повернул на четверть — длиной марша стала бывшая ширина коробки, шириной — бывшая длина; развернул — низ и верх поменялись местами.
    /// Ступеней не меньше прежнего: если они не влезают по глубине (мельче 0,1 м), марш выйдет за коробку. С Undo.
    /// </summary>
    public static class BoxModelStairsTurns
    {
        // Следующее направление по часовой, если смотреть сверху: +Z → +X → −Z → −X.
        public static BoxModelStairsDirection Clockwise(BoxModelStairsDirection direction)
        {
            return direction switch
            {
                BoxModelStairsDirection.PositiveZ => BoxModelStairsDirection.PositiveX,
                BoxModelStairsDirection.PositiveX => BoxModelStairsDirection.NegativeZ,
                BoxModelStairsDirection.NegativeZ => BoxModelStairsDirection.NegativeX,
                _ => BoxModelStairsDirection.PositiveZ
            };
        }


        // Подъём в обратную сторону: низ и верх меняются местами.
        public static BoxModelStairsDirection Opposite(BoxModelStairsDirection direction)
        {
            return Clockwise(Clockwise(direction));
        }


        // Новое направление в той же коробке: угол опоры и габарит не меняются, ширина и глубина ступени — под коробку.
        public static void Turn(BoxModelStairs stairs, BoxModelStairsDirection direction)
        {
            var box = stairs.Size;

            Undo.RecordObject(stairs, "Turn Stairs");
            stairs.Direction = direction;

            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;
            stairs.Width = box[crossAxis];
            stairs.StepDepth = box[stairs.RunAxis] / stairs.StepCount;
        }
    }
}
