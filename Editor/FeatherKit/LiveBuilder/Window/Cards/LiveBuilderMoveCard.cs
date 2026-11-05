using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Перенос и копии» вкладки «Выделено» — точный сдвиг числами и ряд копий (<see cref="LiveBuilderExactMove"/>):
    /// то же, что стрелки и Shift + стрелки в окне сцены, но ровно на набранные метры и сразу N штук. Есть у всего, что двигают стрелки.
    /// </summary>
    public sealed class LiveBuilderMoveCard : LiveBuilderSelectionCard
    {
        public override int Order => 15;


        public override bool Draw(GameObject selected)
        {
            if (selected == null || LiveBuilderMoveHandle.MovableOf(selected) == null)
                return false;

            using (LiveBuilderWindowControls.Card("Перенос и копии"))
            {
                LiveBuilderExactMove.Offset = EditorGUILayout.Vector3Field(
                    new GUIContent("Сдвиг, м", "На сколько сдвинуть или где встанет первая копия. Оси — как у ручек переноса: у коробки, комнаты и лестницы — оси их объекта"),
                    LiveBuilderExactMove.Offset);

                LiveBuilderExactMove.CopyCount = EditorGUILayout.IntSlider(
                    new GUIContent("Копий", "Сколько копий сделать: каждая следующая — ещё на тот же сдвиг дальше. Больше " + LiveBuilderExactMove.MaxCopyCount +
                                            " — это уже повтор: карточка «Повтор»"),
                    LiveBuilderExactMove.CopyCount, 1, LiveBuilderExactMove.MaxCopyCount);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Сдвинуть", "Выделенное уедет на сдвиг. Ctrl+Z — обратно")))
                    {
                        LiveBuilderExactMove.Move(LiveBuilderExactMove.MovableSelection(), LiveBuilderExactMove.Offset);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(new GUIContent("Копии ×" + LiveBuilderExactMove.CopyCount, "Копии выделенного рядом, через сдвиг; выделены станут копии. Один Ctrl+Z — все обратно")))
                    {
                        LiveBuilderExactMove.Copy(LiveBuilderExactMove.MovableSelection(), LiveBuilderExactMove.Offset, LiveBuilderExactMove.CopyCount);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            return true;
        }
    }
}
