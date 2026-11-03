using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Список покрасок для объектов со своими данными о материалах (<see cref="IObjectMaterialPainter"/>): инструменты записываются сюда при загрузке,
    /// палитра уровня спрашивает их, прежде чем красить рендерер. Так палитра ничего не знает про сами инструменты.
    /// </summary>
    public static class ObjectMaterialPainters
    {
        private static readonly List<IObjectMaterialPainter> Painters = new();


        public static void Register(IObjectMaterialPainter painter)
        {
            if (!Painters.Contains(painter))
                Painters.Add(painter);
        }


        // Чья покраска берётся за этот объект; ничья — null.
        public static IObjectMaterialPainter Find(GameObject gameObject)
        {
            foreach (var painter in Painters)
                if (painter.DescribeTarget(gameObject) != null)
                    return painter;

            return null;
        }
    }
}
