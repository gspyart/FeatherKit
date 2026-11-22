using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Комната коробками расчёта для сборщика (<see cref="BoxModelShapeCollector"/>): пустота и оболочка — один кусок, своя пустота оболочку не режет.
    /// - оболочка — до шести коробок: стены, пол, потолок по переключателям комнаты; в здании стены на его стенах не строятся
    /// - изнутри — материалы пола, стен и потолка комнаты, снаружи — материал объекта
    /// - старые данные (версия 0) — прежняя одна коробка вокруг, её режет пустота
    /// </summary>
    public static class BoxModelRoomShell
    {
        private const int FloorSide = 3;
        private const int CeilingSide = 2;

        // Стены по сторонам коробки (BoxFaces): +X, −X, +Z, −Z.
        private static readonly int[] WallSides = { 0, 1, 4, 5 };

        // Кто кому отдаёт угол: пол и потолок идут под стенами, стены по X — через углы стен по Z. Номер — ось.
        private static readonly int[] CornerPriorityByAxis = { 1, 2, 0 };


        // ── Сборщик ──────────────────────────────────────────────────────

        // min — угол пола комнаты в осях объекта, с учётом копии. Новый кусок начат: пустота и оболочка — в нём.
        internal static void Add(BoxModelRoom room, BoxModel model, Vector3 min, BoxModelCollectedBoxes boxes)
        {
            if (LiveBuilderCutRules.IsLegacy(model))
            {
                AddLegacy(room, min, boxes);
                return;
            }

            var inside = InsideMaterialsOf(room);
            var interior = MillimeterBox.FromMeters(min, room.Size);

            foreach (var (box, materials) in ShellOf(interior, room.WallThickness, room.SlabThickness, SidesOf(room, model, min), inside))
                boxes.Add(box, BoxModelPartMode.Add, room, materials);

            boxes.Add(interior, BoxModelPartMode.Cut, room, inside);
        }

        // Какие стороны строить: стены, пол и потолок по переключателям; стена, что легла на стену здания, — нет.
        private static bool[] SidesOf(BoxModelRoom room, BoxModel model, Vector3 min)
        {
            var sides = SidesFor(room.HasWalls, room.HasFloor, room.HasCeiling);
            var building = room.HasWalls ? LiveBuilderBuildingContext.BuildingOfPiece(room) : null;

            if (building == null)
                return sides;

            var contours = SplineBuildingMeshBuilder.InnerContoursOf(building);
            var modelToBuilding = building.transform.worldToLocalMatrix * model.transform.localToWorldMatrix;
            var max = min + room.Size;

            foreach (var side in WallSides)
            {
                GetWallLine(side, min, max, out var start, out var end);
                sides[side] = !SplineWallBuildingSides.IsAlongContours(modelToBuilding.MultiplyPoint3x4(start), modelToBuilding.MultiplyPoint3x4(end), contours);
            }

            return sides;
        }

        // Внутренняя сторона стены по полу, от угла до угла.
        private static void GetWallLine(int side, Vector3 min, Vector3 max, out Vector3 start, out Vector3 end)
        {
            var axis = BoxFaces.Axis(side);
            var across = axis == 0 ? 2 : 0;
            var plane = BoxFaces.IsPositive(side) ? max[axis] : min[axis];
            start = min;
            end = min;
            start[axis] = plane;
            end[axis] = plane;
            end[across] = max[across];
        }

        // Прежняя оболочка — одна коробка на толщину стен и перекрытий; без стен нет ни пола, ни потолка. Пустота её режет.
        private static void AddLegacy(BoxModelRoom room, Vector3 min, BoxModelCollectedBoxes boxes)
        {
            if (room.HasWalls)
            {
                var below = room.HasFloor ? room.SlabThickness : 0f;
                var above = room.HasCeiling ? room.SlabThickness : 0f;
                var shellMin = min - new Vector3(room.WallThickness, below, room.WallThickness);
                var shellSize = room.Size + new Vector3(room.WallThickness * 2f, below + above, room.WallThickness * 2f);
                boxes.Add(MillimeterBox.FromMeters(shellMin, shellSize), BoxModelPartMode.Add, room, default);
            }

            boxes.Add(MillimeterBox.FromMeters(min, room.Size), BoxModelPartMode.Cut, room, InsideMaterialsOf(room));
        }


        // ── Предпросмотр рисования ───────────────────────────────────────

        // Те же коробки, что даст комната, — без комнаты на сцене: пустота и оболочка, в осях рамки.
        public static List<BoxModelSolverBox> PreviewBoxesOf(Vector3 min, Vector3 size, float wallThickness, float slabThickness, bool[] sides,
            BoxModelSideMaterials inside)
        {
            var interior = MillimeterBox.FromMeters(min, size);
            var boxes = new List<BoxModelSolverBox>();

            foreach (var (box, materials) in ShellOf(interior, wallThickness, slabThickness, sides, inside))
                boxes.Add(new BoxModelSolverBox(box, BoxModelPartMode.Add, null, materials));

            boxes.Add(new BoxModelSolverBox(interior, BoxModelPartMode.Cut, null, inside));
            return boxes;
        }


        // ── Оболочка ─────────────────────────────────────────────────────

        // Стороны по номерам BoxFaces: стены — все четыре или ни одной, пол — низ, потолок — верх.
        public static bool[] SidesFor(bool hasWalls, bool hasFloor, bool hasCeiling)
        {
            var sides = new bool[BoxFaces.Count];

            foreach (var side in WallSides)
                sides[side] = hasWalls;

            sides[FloorSide] = hasFloor;
            sides[CeilingSide] = hasCeiling;
            return sides;
        }


        // Коробка на каждую построенную сторону, вплотную к комнате, — в миллиметрах от её коробки: стыки без зазоров.
        private static List<(MillimeterBox Box, BoxModelSideMaterials Materials)> ShellOf(MillimeterBox interior, float wallThickness, float slabThickness,
            bool[] sides, BoxModelSideMaterials inside)
        {
            var wall = Mathf.RoundToInt(wallThickness * MillimeterBox.MillimetersPerMeter);
            var slab = Mathf.RoundToInt(slabThickness * MillimeterBox.MillimetersPerMeter);
            var shell = new List<(MillimeterBox, BoxModelSideMaterials)>();

            for (var side = 0; side < BoxFaces.Count; side++)
            {
                if (!sides[side])
                    continue;

                var axis = BoxFaces.Axis(side);
                var min = interior.Min;
                var max = interior.Max;

                if (BoxFaces.IsPositive(side))
                {
                    min[axis] = interior.Max[axis];
                    max[axis] = interior.Max[axis] + (axis == 1 ? slab : wall);
                }
                else
                {
                    min[axis] = interior.Min[axis] - (axis == 1 ? slab : wall);
                    max[axis] = interior.Min[axis];
                }

                for (var otherAxis = 0; otherAxis < 3; otherAxis++)
                {
                    if (otherAxis == axis || CornerPriorityByAxis[otherAxis] > CornerPriorityByAxis[axis])
                        continue;

                    if (sides[BoxFaces.Index(otherAxis, false)])
                        min[otherAxis] -= wall;

                    if (sides[BoxFaces.Index(otherAxis, true)])
                        max[otherAxis] += wall;
                }

                var innerSide = BoxFaces.Opposite(side);
                var materials = MaterialOnSide(innerSide, inside.ForSide(side)).WithFits(BoxModelSideFits.OnSide(innerSide, inside.Fits.ForSide(side)));
                shell.Add((new MillimeterBox(min, max), materials));
            }

            return shell;
        }

        // Материал только на одной стороне коробки, остальные — материал объекта.
        private static BoxModelSideMaterials MaterialOnSide(int side, Material material)
        {
            return side switch
            {
                0 => new BoxModelSideMaterials(null, right: material),
                1 => new BoxModelSideMaterials(null, left: material),
                2 => new BoxModelSideMaterials(null, top: material),
                3 => new BoxModelSideMaterials(null, bottom: material),
                4 => new BoxModelSideMaterials(null, front: material),
                _ => new BoxModelSideMaterials(null, back: material)
            };
        }


        // ── Стороны изнутри ──────────────────────────────────────────────

        // Материалы изнутри — у пустоты по её сторонам: низ — пол, верх — потолок, бока — стены.
        public static BoxModelSideMaterials InsideMaterialsOf(BoxModelRoom room)
        {
            var fits = room.TextureFits;
            var sideFits = fits.IsEmpty
                ? default
                : new BoxModelSideFits(null, fits.Get(BoxModelRoomTextureSurface.Floor), fits.Get(BoxModelRoomTextureSurface.Ceiling),
                    fits.Get(BoxModelRoomTextureSurface.Walls));
            return new BoxModelSideMaterials(null, room.FloorMaterial, room.CeilingMaterial, room.WallMaterial).WithFits(sideFits);
        }


        // Сторона коробки оболочки смотрит в комнату: лежит на пустоте своего куска с той же стороны. Иначе это наружная сторона.
        public static bool IsInsideSide(BoxModel model, BoxModelSolverBox shellBox, int side)
        {
            if (shellBox.Mode != BoxModelPartMode.Add || shellBox.Owner is not BoxModelRoom)
                return false;

            var axis = BoxFaces.Axis(side);
            var isPositive = BoxFaces.IsPositive(side);
            var plane = isPositive ? shellBox.Box.Max[axis] : shellBox.Box.Min[axis];

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Mode == BoxModelPartMode.Cut && box.Owner == shellBox.Owner && box.PieceKey == shellBox.PieceKey
                    && plane == (isPositive ? box.Box.Min[axis] : box.Box.Max[axis]))
                    return true;

            return false;
        }
    }
}
