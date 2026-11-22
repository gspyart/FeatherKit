using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Inside — что лежит внутри выделенного, без окна Hierarchy; клик — выделить:
    /// - здание — его этажи плашками с числом вещей на каждом
    /// - этаж здания — вещи на нём
    /// - объект из коробок — здания и стены по линии в нём: их числа — у них самих
    /// </summary>
    public sealed class LiveBuilderInnerObjectsCard : LiveBuilderSelectionCard
    {
        private const float ChipWidth = 76f;


        public override int Order => InsideOrder;

        public override string Title => "Inside";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return selected.Count == 1 && InsideOf(selected[0]).Count > 0;
        }


        // «Storey 1 · 4, Storey 2 · 6…».
        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            var inside = InsideOf(selected[0]);
            return string.Join(", ", inside.ConvertAll(LabelOf));
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            var inside = InsideOf(selected[0]);
            var perRow = Mathf.Max(1, Mathf.FloorToInt((LiveBuilderWindowControls.AvailableWidth - 16f) / (ChipWidth + 2f)));

            for (var first = 0; first < inside.Count; first += perRow)
                using (new EditorGUILayout.HorizontalScope())
                    for (var index = first; index < Mathf.Min(first + perRow, inside.Count); index++)
                        if (GUILayout.Button(new GUIContent(LabelOf(inside[index]), "Выделить «" + inside[index].name + "»"), EditorStyles.miniButton,
                                GUILayout.Width(ChipWidth)))
                            Selection.activeGameObject = inside[index].gameObject;
        }


        // ── Что внутри ───────────────────────────────────────────────────

        // У здания — этажи и крыша, у этажа — его вещи без собранных частей, у объекта из коробок — здания и стены.
        public static List<Transform> InsideOf(GameObject selected)
        {
            var inside = new List<Transform>();

            if (selected.TryGetComponent<SplineBuilding>(out var building))
            {
                foreach (var container in SplineBuildingStoreys.ContainersOf(building))
                    inside.Add(container.transform);
            }
            else if (selected.TryGetComponent<SplineBuildingStorey>(out _))
            {
                inside.AddRange(ThingsOn(selected.transform));
            }
            else if (selected.TryGetComponent<BoxModel>(out var model))
            {
                foreach (var innerBuilding in model.GetComponentsInChildren<SplineBuilding>(false))
                    inside.Add(innerBuilding.transform);

                foreach (var wall in model.GetComponentsInChildren<SplineWall>(false))
                    inside.Add(wall.transform);
            }

            return inside;
        }

        // Вещи этажа — его дети, кроме частей, которые собирает здание.
        private static List<Transform> ThingsOn(Transform storey)
        {
            var things = new List<Transform>();

            foreach (Transform child in storey)
                if (!child.TryGetComponent<SplineBuildingPart>(out _))
                    things.Add(child);

            return things;
        }

        // Этаж — «Storey 2 · 6», где 6 — вещей на нём; остальное — имя.
        private static string LabelOf(Transform each)
        {
            return each.TryGetComponent<SplineBuildingStorey>(out _) ? each.name + " · " + ThingsOn(each).Count : each.name;
        }
    }
}
