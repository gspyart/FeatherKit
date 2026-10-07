using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Проверка тега по коллайдеру — то, что нужно каждому, кто фильтрует цели в физических
    /// событиях (снаряды, контактный урон, зоны эффектов).
    /// </summary>
    public static class FUnitTagUtils
    {
        /// <summary>
        /// Есть ли у сущности, которой принадлежит этот компонент, нужный тег.
        /// Ищем в родителях: коллайдер обычно висит на дочернем объекте модели, а теги —
        /// на корне сущности, где лежит FEntityBase.
        /// </summary>
        public static bool HasTag(this Component component, UnitTag tag)
        {
            if (component == null || tag == null)
                return false;

            var entity = component.GetComponentInParent<FEntityBase>();

            return entity != null && entity.HasTag(tag);
        }
        
        /// <summary>
        /// То же самое, только для GameObject
        /// </summary>
      
        public static bool HasTag(this GameObject gameObject, UnitTag tag)
        {
            if (gameObject == null || tag == null)
                return false;

            var entity = gameObject.GetComponent<FEntityBase>();

            return entity != null && entity.HasTag(tag);
        }
    }
}
