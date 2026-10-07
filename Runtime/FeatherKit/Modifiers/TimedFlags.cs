namespace FeatherKit.Modifiers
{
    /// <summary>
    /// Заявки на одно и то же: итог — «хоть кто-то держит», база — «никто». Придержать
    /// взгляд, забрать тело себе, приостановить прицел.
    ///
    /// Тем же ключом-просящим, что и остальные виды, и ровно поэтому здесь не общий флаг:
    /// замах, катсцена и оглушение приходят с разных сторон и общий флаг отбирали бы друг
    /// у друга.
    /// </summary>
    public class TimedFlags : TimedModifiers<bool>
    {
        /// <summary>Держит ли хоть кто-то. То же, что <see cref="TimedModifiers{T}.Value"/>, только под именем заявки.</summary>
        public bool IsHeld => Value;

        protected override bool Default => false;

        protected override bool Combine(bool accumulated, bool value) => accumulated || value;


        // ── Заявки ────────────────────────────────────────────────────────

        /// <summary>
        /// Подать заявку от себя. Срок в секундах, ноль — «пока не отпущу».
        /// </summary>
        public void Hold(object key, float duration = 0f)
        {
            Set(key, true, duration);
        }

        /// <summary>Отпустить свою заявку. Не держал — ничего и не случится.</summary>
        public void Release(object key)
        {
            Clear(key);
        }
    }
}
