using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Покраска объекта, который хранит материалы сам, а меш собирает из них заново (постройки Live Builder): красить надо его данные,
    /// а не рендерер — покраска рендерера слетела бы при следующей пересборке. Палитра уровня спрашивает такие покраски первой
    /// (<see cref="ObjectMaterialPainters"/>); никто не взялся — красит рендерер как обычно.
    /// </summary>
    public interface IObjectMaterialPainter
    {
        // Что покрасит клик по материалу на этом объекте, коротко: «верх коробки», «стена». Не мой объект — null.
        string DescribeTarget(GameObject gameObject);

        // Покрасить объект, запись отмены — внутри. Не мой объект — false.
        bool TryPaint(GameObject gameObject, Material material);
    }
}
