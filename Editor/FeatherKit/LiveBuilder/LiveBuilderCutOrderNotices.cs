using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сообщения в консоль о вырезающих коробках, которые стоят выше стены или здания в иерархии и потому больше их не режут
    /// (<see cref="LiveBuilderHierarchyOrder"/>). Нужны старым сценам: раньше коробка резала всё в объекте, где бы ни стояла.
    /// Одно сообщение на пару «коробка — стена» за сессию редактора, со ссылкой на стену: клик по сообщению её выделит.
    /// </summary>
    public static class LiveBuilderCutOrderNotices
    {
        private static readonly HashSet<(EntityId Box, EntityId Target)> ReportedPairs = new();


        // Коробка прорезала бы цель, но стоит выше неё — сказать об этом один раз.
        public static void ReportBoxAboveTarget(Component box, Component target)
        {
            if (box == null || target == null || !ReportedPairs.Add((box.GetEntityId(), target.GetEntityId())))
                return;

            Debug.Log("Live Builder: вырезающая коробка «" + box.name + "» стоит выше «" + target.name +
                      "» в иерархии и больше её не режет. Нужно резать — перетащи коробку ниже.", target);
        }
    }
}
