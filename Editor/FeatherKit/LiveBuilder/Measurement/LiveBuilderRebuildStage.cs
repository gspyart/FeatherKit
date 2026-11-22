namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Стадия пересборки Live Builder — то, что меряет <see cref="LiveBuilderRebuildProfiler"/>: у каждой свой маркер профайлера и строка в замере.
    /// Сверки перед пересборкой, расчёт коробок, сборка меша, вырезы, коллайдер, запись на диск и развёртка под запекание.
    /// </summary>
    public enum LiveBuilderRebuildStage
    {
        LevelSync,
        StoreySync,
        AnchorSync,
        LineSync,
        ArraySync,
        BoxModelPass,
        BuildingPass,
        WallPass,
        ColumnPass,
        SignatureCheck,
        Solver,
        Surfaces,
        MeshFile,
        MeshFill,
        Bevel,
        Chamfer,
        Cutters,
        Collider,
        BuildingGeometry,
        WallGeometry,
        DiskSave,
        LightmapUV
    }
}
