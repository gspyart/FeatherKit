namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Одна часть префаба в итоге переделки: объект или компонент, где он лежит и что с ним
    /// стало. Только для показа человеку — сама переделка на него не опирается.
    /// </summary>
    public sealed class PrefabPartMatch
    {
        public PrefabPartMatch(string path, bool isGameObject, PrefabPartMatchKind kind)
        {
            Path = path;
            IsGameObject = isGameObject;
            Kind = kind;
        }


        /// <summary>Где лежит, по-человечески: «Model › Box Collider».</summary>
        public string Path { get; }

        public bool IsGameObject { get; }

        public PrefabPartMatchKind Kind { get; }
    }
}
