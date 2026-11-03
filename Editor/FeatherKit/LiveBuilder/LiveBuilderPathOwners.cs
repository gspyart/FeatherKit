using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Стена по линии (<see cref="SplineWall"/>) и здание по контуру (<see cref="SplineBuilding"/>) для инструментов — одно и то же:
    /// сплайн, вдоль которого стоит стена. Всё, где их приходится различать, — здесь: кому принадлежит коллайдер, в чьей стене лежит вырез,
    /// пути и шаг на изгибе, где стороны стены относительно линии, к этажам какого здания привязано поставленное в стену.
    /// </summary>
    public static class LiveBuilderPathOwners
    {
        // ── Чьё ──────────────────────────────────────────────────────────

        public static bool IsPathOwner(Component component)
        {
            return component != null && (component.GetComponent<SplineWall>() != null || component.GetComponent<SplineBuilding>() != null);
        }


        // Стена по линии или здание, чей это коллайдер: у стены он на ней самой, у здания — на его частях.
        // Ближе к коллайдеру объект из коробок (например, в интерьере здания) — это не стена, null.
        public static Component OwnerOf(Collider collider)
        {
            for (var current = collider.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent<SplineWall>(out var wall))
                    return wall;

                if (current.TryGetComponent<SplineBuilding>(out var building))
                    return building;

                if (current.TryGetComponent<BoxModel>(out _))
                    return null;
            }

            return null;
        }


        // Стена по линии или здание, в котором лежит вырез: ближайшее над ним.
        public static Component OwnerAbove(Component hole)
        {
            for (var current = hole.transform.parent; current != null; current = current.parent)
            {
                if (current.TryGetComponent<SplineWall>(out var wall))
                    return wall;

                if (current.TryGetComponent<SplineBuilding>(out var building))
                    return building;
            }

            return null;
        }


        // Здание, к этажам которого привяжется поставленное в стену: само здание или то, к которому привязана перегородка. Нет — null.
        public static SplineBuilding LinkedBuildingOf(Component pathOwner)
        {
            if (pathOwner == null)
                return null;

            if (pathOwner.TryGetComponent<SplineBuilding>(out var building))
                return building;

            return pathOwner.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid ? link.Building : null;
        }


        // ── Линия ────────────────────────────────────────────────────────

        // Длина прямого кусочка на изгибе, м.
        public static float CurveStepOf(Component owner)
        {
            return owner is SplineBuilding building ? building.CurveStep : ((SplineWall)owner).CurveStep;
        }


        // Пути всех сплайнов стены или здания — по ним строятся стены.
        public static List<SplineWallPath> PathsOf(Component owner)
        {
            return SplineWallPath.FromContainer(owner.GetComponent<SplineContainer>(), CurveStepOf(owner));
        }


        // Где стороны стены относительно этого пути: смещения влево и вправо по ходу линии.
        // У стены — по её «Где стена», у здания — внутрь или наружу от контура.
        public static void GetWallSides(Component owner, SplineWallPath path, out float leftOffset, out float rightOffset)
        {
            if (owner is SplineBuilding building)
                SplineBuildingMeshBuilder.GetWallOffsets(building, path, out leftOffset, out rightOffset);
            else
                SplineWallMeshBuilder.GetSideOffsets((SplineWall)owner, out leftOffset, out rightOffset);
        }
    }
}
