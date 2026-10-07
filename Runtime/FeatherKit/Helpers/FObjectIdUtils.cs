using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Номер объекта Unity в памяти, одинаковый по типу на любой версии движка.
    ///
    /// До Unity 6.2 это был int из GetInstanceID, с 6.2 — структура EntityId из GetEntityId,
    /// а с 6.6 старый метод вообще не компилируется. Здесь развилка по версии спрятана
    /// в одном месте: все словари и ключи библиотеки берут номер отсюда и о ней не знают.
    /// </summary>
    public static class FObjectIdUtils
    {
        /// <summary>Номер объекта в памяти, годится как ключ словаря. Уникален, пока объект жив.</summary>
        public static ulong GetStableId(this Object target)
        {
#if UNITY_6000_2_OR_NEWER
            return EntityId.ToULong(target.GetEntityId());
#else
            return (ulong)(long)target.GetInstanceID();
#endif
        }
    }
}
