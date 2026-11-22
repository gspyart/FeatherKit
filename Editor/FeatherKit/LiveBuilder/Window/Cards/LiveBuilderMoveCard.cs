using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Move &amp; Copies — точный сдвиг числами и ряд копий (<see cref="LiveBuilderExactMove"/>): то же, что ручки и Shift + ручки в окне сцены,
    /// но ровно на набранные метры и сразу N штук. Последняя и свёрнута; общая для всех видов — у всего, что двигают ручки.
    /// </summary>
    public sealed class LiveBuilderMoveCard : LiveBuilderSelectionCard
    {
        public override int Order => MoveOrder;

        public override string Title => "Move & Copies";

        public override bool IsSharedByAllKinds => true;

        public override bool IsOpenByDefault => false;


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            foreach (var each in selected)
                if (LiveBuilderMoveHandle.MovableOf(each) == null)
                    return false;

            return true;
        }


        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            return "exact offset, ×N copies";
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            LiveBuilderExactMove.Offset = EditorGUILayout.Vector3Field(
                new GUIContent("Offset, m", "На сколько сдвинуть или где встанет первая копия. Оси — как у ручек переноса: у коробки, комнаты и лестницы — оси их объекта"),
                LiveBuilderExactMove.Offset);

            LiveBuilderExactMove.CopyCount = EditorGUILayout.IntSlider(
                new GUIContent("Copies", "Сколько копий сделать: каждая следующая — ещё на тот же сдвиг дальше. Больше " + LiveBuilderExactMove.MaxCopyCount +
                                         " — это уже повтор: секция Array"),
                LiveBuilderExactMove.CopyCount, 1, LiveBuilderExactMove.MaxCopyCount);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Move", "Выделенное уедет на сдвиг. Ctrl+Z — обратно")))
                {
                    LiveBuilderExactMove.Move(LiveBuilderExactMove.MovableSelection(), LiveBuilderExactMove.Offset);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Copies × " + LiveBuilderExactMove.CopyCount, "Копии выделенного рядом, через сдвиг; выделены станут копии. Один Ctrl+Z — все обратно")))
                {
                    LiveBuilderExactMove.Copy(LiveBuilderExactMove.MovableSelection(), LiveBuilderExactMove.Offset, LiveBuilderExactMove.CopyCount);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
