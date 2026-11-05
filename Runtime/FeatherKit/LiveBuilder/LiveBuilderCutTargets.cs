using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Что режет вырезающая коробка или вырез (<see cref="GeometryCutter"/>) из стен и зданий — как проём в Revit:
    /// шахта пробивает только перекрытия, проём в стене — только стены.
    /// Всё — как раньше: стены по линии, стены зданий и их перекрытия (у выреза — ещё и объекты из коробок).
    /// Только перекрытия — пол, перекрытия и крышу зданий. Только стены — стены по линии и стены зданий.
    /// </summary>
    public enum LiveBuilderCutTargets
    {
        [InspectorName("Всё")] Everything,
        [InspectorName("Только перекрытия")] SlabsOnly,
        [InspectorName("Только стены")] WallsOnly
    }
}
