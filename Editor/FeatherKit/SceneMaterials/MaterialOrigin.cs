namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Откуда материал в слоте — от этого зависит, что с ним можно делать. Сценовый живёт
    /// только в сцене, проектный — общий файл на всех, кто его носит, встроенный правится
    /// только копией. Узнаётся через <see cref="SceneMaterialTools.GetOrigin"/>.
    /// </summary>
    public enum MaterialOrigin
    {
        Empty,
        Scene,
        Project,
        BuiltIn
    }
}
