using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Этажи в здании» вкладки «Выделено» — у всего, что стоит в здании: привязано — этаж и повтор (инспектор привязки);
    /// не привязано — кнопка привязать. Привязать можно перегородку, вырез, лестницу, коробку, комнату и целый объект из коробок внутри здания —
    /// не само здание и не его собранные части.
    /// Одна привязка на цепочку копий: коробку, комнату и лестницу в уже привязанном объекте или группе не привязать — этажи им уже повторены;
    /// объект, в котором уже привязаны свои коробки, — тоже (<see cref="BoxModelShapeCollector"/>). Стене и вырезу копии даёт только своя привязка —
    /// им можно всегда.
    /// </summary>
    public sealed class LiveBuilderStoreyLinkCard : LiveBuilderSelectionCard
    {
        private Editor linkEditor;


        public override int Order => 70;


        // ── Рисование ────────────────────────────────────────────────────

        public override bool Draw(GameObject selected)
        {
            if (selected == null)
                return false;

            var link = selected.GetComponent<SplineBuildingStoreyLink>();

            if (link != null && link.IsValid)
            {
                using (LiveBuilderWindowControls.Card("Этажи в здании"))
                    DrawCachedInspector(link, typeof(SplineBuildingStoreyLinkEditor), ref linkEditor);

                return true;
            }

            var building = selected.GetComponentInParent<SplineBuilding>();

            if (building == null || !CanLinkToStoreys(selected))
                return false;

            using (LiveBuilderWindowControls.Card("Этажи в здании"))
            {
                var repeatedReason = StoreyCopiesReasonOf(selected);

                if (repeatedReason != null)
                {
                    LiveBuilderWindowControls.Note(repeatedReason);
                    return true;
                }

                LiveBuilderWindowControls.Note("Стоит в здании, но не следит за его этажами.");

                if (GUILayout.Button(new GUIContent("Привязать к этажам здания «" + building.name + "»",
                        "Встанет на свой этаж здания и будет следить за высотой этажа; повтор по этажам — здесь же")))
                    SplineBuildingLinkSync.Attach(selected, building, false, selected.GetComponent<BoxModelStairs>() != null);
            }

            return true;
        }

        // Вложенный объект из коробок (шахта, лестничная клетка) — тоже, целиком; само здание и его собранные части — нет.
        private static bool CanLinkToStoreys(GameObject selected)
        {
            if (selected.GetComponent<SplineBuilding>() != null || selected.GetComponent<SplineBuildingPart>() != null)
                return false;

            return selected.GetComponent<SplineWall>() != null || selected.GetComponent<GeometryCutter>() != null
                || BoxModelShapeCollector.IsPiece(selected) || selected.GetComponent<BoxModel>() != null;
        }

        // Почему привязать нельзя: копии по этажам уже даёт привязка выше или внутри — своя их перемножила бы. Можно — null.
        private static string StoreyCopiesReasonOf(GameObject selected)
        {
            if (selected.TryGetComponent<BoxModel>(out var model))
                return BoxModelShapeCollector.HasStoreyLinksInside(model.transform)
                    ? "Внутри уже привязаны свои коробки, комнаты или лестницы. Целиком — сначала сними их привязки."
                    : null;

            var ownerModel = BoxModelShapeCollector.IsPiece(selected) && selected.transform.parent != null ? selected.transform.parent.GetComponentInParent<BoxModel>() : null;

            return ownerModel != null && BoxModelShapeCollector.IsInsideStoreyCopies(ownerModel, selected.transform.parent)
                ? "Уже повторяется по этажам вместе с объектом или группой, в которой стоит."
                : null;
        }


        // ── Конец ────────────────────────────────────────────────────────

        public override void Dispose()
        {
            DestroyInspector(ref linkEditor);
        }
    }
}
