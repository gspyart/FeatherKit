namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Как собрана постройка Live Builder: начерно — пока тянут ручку, начисто — когда отпустили.
    /// Черновик (<see cref="LiveBuilderDraftBuilds"/>) — без фаски и коллайдера и без записи подписи; чистовая — всё.
    /// </summary>
    public enum LiveBuilderBuildQuality
    {
        Draft,
        Final
    }
}
