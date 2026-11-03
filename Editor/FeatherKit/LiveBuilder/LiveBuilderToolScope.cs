using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что правит каждый инструмент Live Builder: «Выделение» — всё, остальные — только своё: «Коробка» — коробки, «Комната» — комнаты
    /// (и прямоугольные, и по контуру), «Лестница» — лестницы, «Стена» — стены по линии, «Здание» — здания, «Проём» — вырезы и вырезающие коробки,
    /// «Линейка» — ничего. По этому клик решает, что выделить, а ручки — у чего показываться; чужое выделенное видно каркасом, без ручек.
    /// </summary>
    public static class LiveBuilderToolScope
    {
        // Может ли инструмент выделить и править эту постройку или её часть.
        public static bool CanEdit(LiveBuilderTool tool, Component target)
        {
            if (target == null)
                return false;

            if (target is SplineBuildingPart part)
                target = part.GetComponentInParent<SplineBuilding>();

            return tool switch
            {
                LiveBuilderTool.Select => target != null,
                LiveBuilderTool.Box => target is BoxModelPart,
                LiveBuilderTool.Room => target is BoxModelRoom || IsContourRoom(target),
                LiveBuilderTool.Stairs => target is BoxModelStairs,
                LiveBuilderTool.Wall => target is SplineWall,
                LiveBuilderTool.Building => target is SplineBuilding,
                LiveBuilderTool.Opening => target is GeometryCutter || target is BoxModelPart { Mode: BoxModelPartMode.Cut },
                _ => false
            };
        }


        // То же для объекта: смотрит постройку или её часть на нём. Объект из коробок целиком и группу повтора правит только «Выделение».
        public static bool CanEdit(LiveBuilderTool tool, GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (gameObject.TryGetComponent<BoxModelPart>(out var part))
                return CanEdit(tool, part);

            if (gameObject.TryGetComponent<BoxModelRoom>(out var room))
                return CanEdit(tool, room);

            if (gameObject.TryGetComponent<BoxModelStairs>(out var stairs))
                return CanEdit(tool, stairs);

            if (gameObject.TryGetComponent<GeometryCutter>(out var cutter))
                return CanEdit(tool, cutter);

            if (gameObject.TryGetComponent<SplineWall>(out var wall))
                return CanEdit(tool, wall);

            if (gameObject.TryGetComponent<SplineBuilding>(out var building))
                return CanEdit(tool, building);

            if (gameObject.TryGetComponent<SplineBuildingPart>(out var buildingPart))
                return CanEdit(tool, buildingPart);

            return tool == LiveBuilderTool.Select;
        }


        // Режим «Грани» и стрелки выделенных граней — в «Выделении» и «Коробке».
        public static bool CanEditFaces(LiveBuilderTool tool)
        {
            return tool is LiveBuilderTool.Select or LiveBuilderTool.Box;
        }


        // Ручка группы повтора — тоже.
        public static bool CanEditRepeats(LiveBuilderTool tool)
        {
            return tool is LiveBuilderTool.Select or LiveBuilderTool.Box;
        }


        // Комната по контуру: в пустоте — здание в один этаж, в здании и в доме из коробок — замкнутая стена по линии.
        private static bool IsContourRoom(Component target)
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
    }
}
