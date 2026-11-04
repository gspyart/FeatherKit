using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Этажи в здании» зоны «Выделено» — у всего, что стоит в здании: привязано — этаж и повтор (инспектор привязки);
    /// не привязано — кнопка привязать. Привязать можно перегородку, вырез, лестницу, коробку и комнату — не само здание и не его собранные части.
    /// </summary>
    public sealed class LiveBuilderStoreyLinkCard : LiveBuilderSelectionCard
    {
        private Editor linkEditor;


        public override int Order => 70;


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
                LiveBuilderWindowControls.Note("Стоит в здании, но не следит за его этажами.");

                if (GUILayout.Button(new GUIContent("Привязать к этажам здания «" + building.name + "»",
                        "Встанет на свой этаж здания и будет следить за высотой этажа; повтор по этажам — здесь же")))
                    SplineBuildingLinkSync.Attach(selected, building, false, selected.GetComponent<BoxModelStairs>() != null);
            }

            return true;
        }

        private static bool CanLinkToStoreys(GameObject selected)
        {
            return selected.GetComponent<SplineWall>() != null || selected.GetComponent<GeometryCutter>() != null
                || selected.GetComponent<BoxModelStairs>() != null || selected.GetComponent<BoxModelPart>() != null || selected.GetComponent<BoxModelRoom>() != null;
        }


        public override void Dispose()
        {
            DestroyInspector(ref linkEditor);
        }
    }
}
