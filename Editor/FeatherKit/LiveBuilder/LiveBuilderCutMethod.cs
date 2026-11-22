namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Каким способом вырез режет одну постройку — выбирается само по геометрии (<see cref="LiveBuilderCutRules.MethodOf"/>).
    /// - SolverCells — коробки своего объекта, клетками солвера; WallHoles — проём по линии стены; SlabHoles — дыра в плане перекрытия
    /// - MeshSubtraction — вычитание из готового меша: всё, что точными способами не режется
    /// </summary>
    public enum LiveBuilderCutMethod
    {
        None,
        SolverCells,
        WallHoles,
        SlabHoles,
        MeshSubtraction
    }
}
