namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Правило солвера: кто режет добавляющую коробку объекта.
    /// - LaterBoxesCut — каждая коробка ниже в списке, добавляющая или вырезающая: «вырезал, добавил, вырезал»
    /// - CutsAfterAllAdds — сначала все добавления по списку, потом все вырезы: место выреза в списке не важно
    /// </summary>
    public enum BoxModelCutRule
    {
        LaterBoxesCut,
        CutsAfterAllAdds
    }


    /// <summary>
    /// Может ли вырезающая коробка cutIndex резать добавляющую addIndex — номера в списке коробок солвера.
    /// Только для <see cref="BoxModelCutRule.CutsAfterAllAdds"/>: досягаемость выреза и «не режет свой кусок».
    /// </summary>
    public delegate bool BoxModelCutFilter(int cutIndex, int addIndex);
}
