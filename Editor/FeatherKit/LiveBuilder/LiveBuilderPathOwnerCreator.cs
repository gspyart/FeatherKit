using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Создаёт объект, по точкам которого рисует линия (<see cref="LiveBuilderPathGesture"/>): стену, перегородку, здание или комнату —
    /// по настройкам инструментов и тому, где первый клик: в здании, в доме из коробок, в выделенном объекте или в пустоте.
    /// Точек не ставит — объект создаётся пустым, с Undo (<see cref="BoxModelFactory"/>).
    /// </summary>
    public static class LiveBuilderPathOwnerCreator
    {
        // Здание — в выделенном объекте из коробок, а без него — в новом (createdModel). Стоит низом ровно на том, где кликнул:
        // точка опоры здания — низ плиты пола, пол первого этажа — на толщину плиты выше.
        public static SplineBuilding CreateBuilding(Vector3 worldPoint, out BoxModel createdModel)
        {
            var parentModel = SelectedOrNewModel(worldPoint, "Building", out createdModel);
            return BoxModelFactory.CreateBuilding(parentModel, worldPoint, parentModel.transform.rotation);
        }


        // Стена внутри здания — перегородка: в его объекте, до потолка этажа, толщиной перегородки, на этом или всех этажах выше.
        // Иначе — стена по настройкам, в выделенном объекте из коробок, если он есть.
        public static SplineWall CreateWall(Vector3 worldPoint, LiveBuilderBuildingContext context)
        {
            if (context != null)
            {
                var partition = BoxModelFactory.CreatePartition(context, worldPoint, "Partition");
                partition.Alignment = LiveBuilderWallSettings.Alignment;
                return partition;
            }

            var parentModel = BoxModelActions.SelectedModel();
            var wall = BoxModelFactory.CreateWall(parentModel, worldPoint, parentModel != null ? parentModel.transform.rotation : Quaternion.identity);
            wall.Alignment = LiveBuilderWallSettings.Alignment;
            wall.Material = LiveBuilderWallSettings.Material;
            wall.Thickness = LiveBuilderWallSettings.Thickness;
            wall.WallType = LiveBuilderWallSettings.WallType;
            wall.Height = LiveBuilderWallSettings.Height;
            return wall;
        }


        // В здании — замкнутая перегородка-комната: стороны вдоль его стен не строятся. В доме из коробок (house) — стены перегородки до его потолка.
        // В пустоте — комната со стенами, полом и потолком, как у здания: стоит низом на том, где кликнул; объект из коробок под неё — createdModel.
        public static Component CreateRoom(Vector3 worldPoint, LiveBuilderBuildingContext context, BoxModel house, float houseCeiling, out BoxModel createdModel)
        {
            createdModel = null;

            if (context != null)
            {
                var room = BoxModelFactory.CreatePartition(context, worldPoint, "Room");
                room.SkipsSidesAlongBuildingWalls = true;
                return room;
            }

            if (house != null)
            {
                var walls = BoxModelFactory.CreateWall(house, worldPoint, house.transform.rotation);
                walls.name = "Room";
                walls.Thickness = LiveBuilderWallSettings.PartitionThickness;
                walls.Material = LiveBuilderWallSettings.Material;
                walls.WallType = LiveBuilderWallSettings.PartitionWallType;
                walls.Height = houseCeiling;
                return walls;
            }

            var parentModel = SelectedOrNewModel(worldPoint, "Room", out createdModel);
            return BoxModelFactory.CreateContourRoom(parentModel, worldPoint, parentModel.transform.rotation);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Выделенный объект из коробок, а без него — новый: так в здании или комнате сразу работают лестницы и вырезы.
        private static BoxModel SelectedOrNewModel(Vector3 worldPoint, string name, out BoxModel createdModel)
        {
            var selected = BoxModelActions.SelectedModel();
            createdModel = null;

            if (selected != null)
                return selected;

            createdModel = BoxModelFactory.CreateModel(worldPoint);
            createdModel.name = name;
            return createdModel;
        }
    }
}
