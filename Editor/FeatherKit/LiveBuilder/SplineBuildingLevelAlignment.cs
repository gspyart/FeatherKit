using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Здание по контуру (<see cref="SplineBuilding"/>) и этажи уровня (<see cref="LiveBuilderStoreys"/>): на каком этаже уровня стоит низ здания,
    /// совпадают ли его этажи с этажами окна, и поставить его так, чтобы совпадали. Высоты здания в его осях — тоже здесь.
    /// </summary>
    public static class SplineBuildingLevelAlignment
    {
        // Расхождение меньше этого — совпадает, м.
        private const float AlignmentTolerance = 0.005f;


        // Этаж уровня, на котором стоит низ здания.
        public static int BaseStoreyOf(SplineBuilding building)
        {
            return Mathf.RoundToInt((BottomWorldHeightOf(building) - LiveBuilderStoreys.GroundLevel) / LiveBuilderStoreys.StoreyHeight);
        }


        // Здание стоит низом ровно на этаже уровня и с тем же шагом — его этажи совпадают с этажами окна.
        public static bool IsAlignedToLevel(SplineBuilding building)
        {
            return IsOnLevelFloor(building) && HasLevelStoreyHeight(building);
        }


        // Низ здания ровно на полу этажа уровня.
        public static bool IsOnLevelFloor(SplineBuilding building)
        {
            return Mathf.Abs(OffsetFromLevelFloorOf(building)) < AlignmentTolerance;
        }


        // Высота этажа здания — как у этажей уровня.
        public static bool HasLevelStoreyHeight(SplineBuilding building)
        {
            return Mathf.Abs(building.StoreyHeight - LiveBuilderStoreys.StoreyHeight) < AlignmentTolerance;
        }


        // На сколько низ здания выше ближайшего пола этажа уровня, м; ниже — со знаком минус.
        public static float OffsetFromLevelFloorOf(SplineBuilding building)
        {
            var levelFloor = LiveBuilderStoreys.GroundLevel + BaseStoreyOf(building) * LiveBuilderStoreys.StoreyHeight;
            return BottomWorldHeightOf(building) - levelFloor;
        }


        // Поставить здание низом на ближайший этаж уровня и взять высоту этажа из окна. С Undo.
        public static void AlignToLevel(SplineBuilding building)
        {
            Undo.RecordObjects(new Object[] { building, building.transform }, "Align Building To Storeys");
            building.StoreyHeight = LiveBuilderStoreys.StoreyHeight;
            building.transform.position -= Vector3.up * OffsetFromLevelFloorOf(building);
        }


        // ── Высоты здания ────────────────────────────────────────────────

        // Высота контура в осях здания, м: от неё идут плита пола и этажи.
        public static float BaseHeightOf(SplineBuilding building)
        {
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            return paths.Count > 0 ? paths[0].BaseHeightAt(0f) : 0f;
        }


        // Низ здания в мире, м: низ плиты пола, а без пола — низ стен; на высоте контура.
        private static float BottomWorldHeightOf(SplineBuilding building)
        {
            return building.transform.TransformPoint(new Vector3(0f, BaseHeightOf(building), 0f)).y;
        }
    }
}
