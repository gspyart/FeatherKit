using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Куда инструменты кладут поставленное в здание (<see cref="SplineBuilding"/>) — одно место на все инструменты:
    /// - в этаж здания по высоте объекта (или в выделенную группу здания), «И этажи выше» — повтор по Y до верха (<see cref="LiveBuilderArray"/>)
    /// - стоящее на крыше — в «Roof»
    /// </summary>
    public static class SplineBuildingInteriorPlacement
    {
        // Пропуск по Y у лестницы: с верхнего этажа идти некуда.
        public const string SkipTopStorey = "last";


        // ── На этаж ──────────────────────────────────────────────────────

        // Узел — в этаж на его высоте, место в мире то же. В группе, которую уже повторяют по этажам, свой повтор перемножил бы копии.
        public static void PutOnStorey(Transform node, SplineBuilding building, bool isOnAllStoreys, bool skipsTopStorey, string undoName)
        {
            var group = BoxModelFactory.SelectedGroupIn(building);
            var parent = group != null ? group.transform : SplineBuildingStoreys.ContainerAtHeight(building, node.position.y).transform;
            Undo.SetTransformParent(node, parent, true, undoName);

            if (isOnAllStoreys)
                RepeatOnStoreysAbove(node, building, skipsTopStorey);
        }


        // Вырез на месте (в стене здания или в перегородке) — к этажам здания: в само здание не кладётся, а переезжает в свой этаж.
        public static void PutCutterOnStorey(GeometryCutter cutter, SplineBuilding building, bool isOnAllStoreys)
        {
            if (cutter.transform.parent == building.transform)
                Undo.SetTransformParent(cutter.transform, SplineBuildingStoreys.ContainerAtHeight(building, cutter.transform.position.y).transform, true,
                    "Create Cutter");

            if (isOnAllStoreys)
                RepeatOnStoreysAbove(cutter.transform, building, false);
        }


        // ── На крышу ─────────────────────────────────────────────────────

        // Стоящее на крыше (будка выхода, вентиляция) — в «Roof»: едет за крышей, по этажам не повторяется.
        public static void PutOnRoof(Transform node, SplineBuilding building, string undoName)
        {
            Undo.SetTransformParent(node, SplineBuildingStoreys.RoofOf(building).transform, true, undoName);
        }


        // ── Повтор по этажам ─────────────────────────────────────────────

        // Повтор по Y до верха здания, шаг — этаж; у лестницы без верхнего этажа. Уже повторяется по этажам — ничего.
        public static void RepeatOnStoreysAbove(Transform node, SplineBuilding building, bool skipsTopStorey)
        {
            if (IsRepeatedOnStoreys(node, building))
                return;

            var array = node.GetComponent<LiveBuilderArray>();

            if (array == null)
                array = Undo.AddComponent<LiveBuilderArray>(node.gameObject);
            else
                Undo.RecordObject(array, "Repeat On Storeys Above");

            array.CountX = 1;
            array.CountZ = 1;
            array.CountYToBuildingTop = true;
            array.StepYIsStoreyHeight = true;
            array.SkipY = skipsTopStorey ? SkipTopStorey : "";
            array.CountY = Mathf.Max(1, building.StoreyCount - building.StoreyAtHeight(node.position.y + building.SlabThickness));
        }


        // Узел или что-то над ним в здании уже повторяется по Y: повтор над ним даёт копии и ему.
        public static bool IsRepeatedOnStoreys(Transform node, SplineBuilding building)
        {
            for (var current = node; current != null && current != building.transform; current = current.parent)
                if (current.TryGetComponent<LiveBuilderArray>(out var array) && (array.CountY > 1 || array.CountYToBuildingTop))
                    return true;

            return false;
        }
    }
}
