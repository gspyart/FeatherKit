using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Комната» — одно понятие для человека, а внутри — то, что подходит месту, где её нарисовали: комната из коробок (<see cref="BoxModelRoom"/>),
    /// в доме из коробок без своих стен — пустота в нём; по контуру в здании и в доме — замкнутая стена (<see cref="SplineWall"/>),
    /// в пустоте — здание в один этаж (<see cref="SplineBuilding"/>). Здесь — одно правило, что считать комнатой, и одна строка, во что она
    /// превратилась и почему: для инструмента «Комната» (что он правит) и для карточки выделенного (<see cref="LiveBuilderOwnComponentCard"/>).
    /// </summary>
    public static class LiveBuilderRooms
    {
        // Комната любого вида: из коробок или по контуру.
        public static bool IsRoom(Component target)
        {
            return target is BoxModelRoom || IsContourRoom(target);
        }

        // Комната по контуру: в пустоте — здание в один этаж, в здании и в доме из коробок — замкнутая стена по линии.
        public static bool IsContourRoom(Component target)
        {
            if (target is SplineBuilding building)
                return building.StoreyCount == 1;

            if (target is not SplineWall wall || !wall.TryGetComponent<SplineContainer>(out var container))
                return false;

            foreach (var spline in container.Splines)
                if (spline.Closed)
                    return true;

            return false;
        }


        // Во что комната превратилась здесь — одной строкой для человека. Не комната — null.
        public static string KindOf(Component target)
        {
            switch (target)
            {
                case BoxModelRoom room:
                    return room.HasWalls
                        ? "Из коробок: свои стены, пол и потолок вокруг."
                        : "Пустота в доме из коробок: стены — то, что осталось от дома.";

                case SplineBuilding building when building.StoreyCount == 1:
                    return "По контуру в пустоте: здание в один этаж — свои стены, пол и крыша.";

                case SplineWall wall when IsContourRoom(wall):
                    return wall.GetComponentInParent<SplineBuilding>() != null
                        ? "По контуру в здании: перегородки до потолка этажа, вдоль стен здания своих стен нет."
                        : "По контуру: замкнутая стена вокруг; пол и потолок — по переключателям «Пол и потолок».";

                default:
                    return null;
            }
        }
    }
}
