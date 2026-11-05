using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Повтор» вкладки «Выделено»: у выделенной группы повтора — сколько копий и шаг, «шаг — этаж», «убрать повтор». Всё с Undo.
    /// </summary>
    public sealed class LiveBuilderRepeatGroupCard : LiveBuilderSelectionCard
    {
        public override int Order => 30;


        public override bool Draw(GameObject selected)
        {
            var repeat = LiveBuilderRepeatActions.SelectedRepeat();

            if (repeat == null)
                return false;

            using (LiveBuilderWindowControls.Card("Повтор «" + repeat.name + "»"))
                DrawRepeatFields(repeat);

            return true;
        }

        private static void DrawRepeatFields(BoxModelRepeat repeat)
        {
            var count = EditorGUILayout.IntSlider(new GUIContent("Сколько всего", "Вместе с оригиналом. Ручка-стрелка в сцене делает то же"),
                repeat.Count, 1, BoxModelRepeat.MaxCount);
            var step = EditorGUILayout.Vector3Field(new GUIContent("Шаг, м", "Сдвиг каждой следующей копии в осях объекта"), repeat.Step);

            if (count != repeat.Count || step != repeat.Step)
            {
                Undo.RecordObject(repeat, "Edit Repeat");
                repeat.Count = count;
                repeat.Step = step;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Шаг — этаж", "Шаг вверх на высоту этажа")))
                {
                    Undo.RecordObject(repeat, "Edit Repeat");
                    repeat.Step = new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f);
                }

                if (GUILayout.Button(new GUIContent("Убрать повтор", "Копии исчезнут, оригинал останется на месте")))
                    LiveBuilderRepeatActions.Unwrap(repeat);
            }

            LiveBuilderWindowControls.Explanation("Исключение для одной копии — коробка ниже группы в списке объектов: она главнее.");
        }
    }
}
