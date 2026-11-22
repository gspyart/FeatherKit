using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Chamfer у коробки — скосы рёбер и пандус (<see cref="BoxModelChamferPanel"/>). Скосов нет — строка «+ Add»: новый скос тянут точкой на ребре в сцене.
    /// Выделено несколько коробок — сколько из них со скосами; числа — у одной.
    /// </summary>
    public sealed class LiveBuilderChamferCard : LiveBuilderSelectionCard
    {
        public override int Order => ChamferOrder;

        public override string Title => "Chamfer";

        public override string AddTooltip => "Новый скос — потяни точку в середине ребра коробки в окне сцены внутрь коробки";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return AreAllOfKind(selected, LiveBuilderSelectionKind.Box);
        }


        public override bool IsEmptyFor(IReadOnlyList<GameObject> selected)
        {
            return CountWithChamfers(selected) == 0;
        }


        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            return selected.Count > 1 ? CountWithChamfers(selected) + " of " + selected.Count : null;
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            if (selected.Count > 1)
            {
                LiveBuilderWindowControls.Note("Select one box to edit its chamfers.");
                return;
            }

            BoxModelChamferPanel.Draw(selected[0].GetComponent<BoxModelPart>());
        }


        // Скос рисуют в сцене: подсказываем, где.
        public override void Add(IReadOnlyList<GameObject> selected, Rect buttonRect)
        {
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Drag the dot in the middle of a box edge into the box"));
        }


        private static int CountWithChamfers(IReadOnlyList<GameObject> selected)
        {
            var count = 0;

            foreach (var each in selected)
                if (each.TryGetComponent<BoxModelPart>(out var part) && BoxModelChamferPanel.HasChamfers(part))
                    count++;

            return count;
        }
    }
}
