namespace FeatherKit.Coroutines
{
    /// <summary>
    /// Ключ запущенной корутины. Отдаётся вместо голой ссылки на Coroutine по той же причине,
    /// что и PoolHandle вместо ссылки на объект из пула: ссылка ничего не знает про то, жива
    /// ли ещё корутина, а по ключу это можно спросить.
    ///
    /// Пустой токен (default) безопасен: IsRunning == false, Stop() ничего не делает.
    /// </summary>
    public readonly struct CoroutineToken
    {
        private readonly int id;
        private readonly CoroutineRunner owner;


        public bool IsRunning => owner != null && owner.IsRunning(id);


        internal CoroutineToken(int id, CoroutineRunner owner)
        {
            this.id = id;
            this.owner = owner;
        }


        // Останавливать уже завершённую корутину безопасно — просто ничего не произойдёт.
        public void Stop() => owner?.Stop(id);
    }
}
