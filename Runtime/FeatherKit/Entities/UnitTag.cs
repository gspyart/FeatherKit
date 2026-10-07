using UnityEngine;

namespace FeatherKit.Entities
{
    /// <summary>
    /// Тег-ассет для FEntityBase (например "Enemy", "Boss", "Flammable").
    /// Сравнение идёт по ссылке на ассет, не по строке — новый тег добавляется без правки кода.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Configs/Unit Tag", fileName = "NewUnitTag")]
    public class UnitTag : ScriptableObject
    {
    }
}
