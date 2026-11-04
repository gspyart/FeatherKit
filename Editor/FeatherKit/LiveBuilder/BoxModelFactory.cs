using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Создаёт объекты Live Builder с Undo, по правилам набора — материал и группа из настроек проекта, статика:
    /// объекты из коробок, коробки, комнаты и лестницы в них, стены по линии, здания по контуру и проёмы в них.
    /// </summary>
    public static class BoxModelFactory
    {
        private const string InteriorGroupName = "Interior";


        private static LiveBuilderProjectSettings Settings => LiveBuilderProjectSettings.instance;


        // Новый объект в точке мира — там же, куда Unity кладёт новые объекты: в Default Parent, в открытый префаб.
        // Ни того, ни другого — в группу новых объектов из настроек проекта, если она есть на сцене.
        public static BoxModel CreateModel(Vector3 worldPosition)
        {
            var modelObject = new GameObject("Box Model");
            ObjectFactory.PlaceGameObject(modelObject, null);

            if (modelObject.transform.parent == null)
                PutIntoLevelGeometry(modelObject);

            modelObject.transform.position = worldPosition;
            GameObjectUtility.SetStaticEditorFlags(modelObject, (StaticEditorFlags)~0);

            var model = modelObject.AddComponent<BoxModel>();
            model.DefaultMaterial = LoadDefaultMaterial();

            // Иначе Ctrl+Z убирает коробку или здание, а пустой объект остаётся.
            Undo.RegisterCreatedObjectUndo(modelObject, "Create Box Model");
            return model;
        }

        // Группа ищется только в сцене самого объекта: при нескольких открытых сценах объект не уезжает в чужую.
        private static void PutIntoLevelGeometry(GameObject modelObject)
        {
            if (Settings.NewObjectsGroup.Length == 0)
                return;

            var groupNames = Settings.NewObjectsGroup.Split('/');

            foreach (var root in modelObject.scene.GetRootGameObjects())
            {
                if (root.name != groupNames[0])
                    continue;

                var geometryGroup = root.transform.Find(string.Join("/", groupNames, 1, groupNames.Length - 1));

                if (geometryGroup != null)
                {
                    modelObject.transform.SetParent(geometryGroup, false);
                    return;
                }
            }
        }


        // Материал новых объектов из настроек проекта; не задан — серый материал FeatherKit.
        public static Material LoadDefaultMaterial()
        {
            return Settings.DefaultMaterial;
        }


        // Коробка в конце списка объекта — главнее всех прежних. Углы — в осях объекта, м.
        public static BoxModelPart CreatePart(BoxModel model, Vector3 localMin, Vector3 localMax, BoxModelPartMode mode)
        {
            var partObject = new GameObject(mode == BoxModelPartMode.Add ? BoxModelPieceTidying.AddPartName : BoxModelPieceTidying.CutPartName) { tag = BoxModelPieceTidying.EditorOnlyTag };
            Undo.RegisterCreatedObjectUndo(partObject, "Create Box");

            var partTransform = partObject.transform;
            partTransform.SetParent(model.transform, false);
            partTransform.SetAsLastSibling();
            partTransform.localPosition = Vector3.Min(localMin, localMax);

            var part = partObject.AddComponent<BoxModelPart>();
            part.Mode = mode;
            part.Size = Abs(localMax - localMin);
            return part;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }


        // Комната в конце списка объекта: угол пола и внутренний размер — в осях объекта, м. Толщины и материалы — из настроек инструмента.
        public static BoxModelRoom CreateRoom(BoxModel model, Vector3 localMin, Vector3 size, bool hasWalls)
        {
            var roomObject = new GameObject("Room") { tag = BoxModelPieceTidying.EditorOnlyTag };
            Undo.RegisterCreatedObjectUndo(roomObject, "Create Room");

            var roomTransform = roomObject.transform;
            roomTransform.SetParent(model.transform, false);
            roomTransform.SetAsLastSibling();
            roomTransform.localPosition = localMin;

            var room = roomObject.AddComponent<BoxModelRoom>();
            room.Size = size;
            room.HasWalls = hasWalls;
            room.WallThickness = LiveBuilderRoomSettings.WallThickness;
            room.SlabThickness = LiveBuilderRoomSettings.SlabThickness;
            room.FloorMaterial = LiveBuilderRoomSettings.FloorMaterial;
            room.WallMaterial = LiveBuilderRoomSettings.WallMaterial;
            room.CeilingMaterial = LiveBuilderRoomSettings.CeilingMaterial;
            return room;
        }


        // Лестница в конце списка объекта. Угол — в осях объекта, м. Вид и толщина ступеней — из настроек инструмента.
        public static BoxModelStairs CreateStairs(BoxModel model, Vector3 localMin, BoxModelStairsDirection direction, float width, float height, float stepDepth,
            bool cutsAbove)
        {
            var stairsObject = new GameObject("Stairs") { tag = BoxModelPieceTidying.EditorOnlyTag };
            Undo.RegisterCreatedObjectUndo(stairsObject, "Create Stairs");

            var stairsTransform = stairsObject.transform;
            stairsTransform.SetParent(model.transform, false);
            stairsTransform.SetAsLastSibling();
            stairsTransform.localPosition = localMin;

            var stairs = stairsObject.AddComponent<BoxModelStairs>();
            stairs.Direction = direction;
            stairs.Width = width;
            stairs.Height = height;
            stairs.StepDepth = stepDepth;
            stairs.CutsAbove = cutsAbove;
            stairs.Style = LiveBuilderStairsSettings.Style;
            stairs.StepThickness = LiveBuilderStairsSettings.StepThickness;
            return stairs;
        }


        // ── Стены по линии ───────────────────────────────────────────────

        // Новая стена без точек. В объекте из коробок — его вырезающие коробки режут и её; иначе — как новый объект, в группе уровня.
        public static SplineWall CreateWall(BoxModel parentModel, Vector3 worldPosition, Quaternion worldRotation)
        {
            var wallObject = new GameObject("Spline Wall");
            Undo.RegisterCreatedObjectUndo(wallObject, "Create Wall");

            if (parentModel != null)
            {
                wallObject.transform.SetParent(parentModel.transform, false);
                wallObject.transform.SetAsLastSibling();
            }
            else
            {
                ObjectFactory.PlaceGameObject(wallObject, null);

                if (wallObject.transform.parent == null)
                    PutIntoLevelGeometry(wallObject);
            }

            wallObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
            GameObjectUtility.SetStaticEditorFlags(wallObject, (StaticEditorFlags)~0);

            var container = wallObject.AddComponent<SplineContainer>();
            container.Spline.Clear();

            return wallObject.AddComponent<SplineWall>();
        }


        // Перегородка в здании без точек: в его объекте, в группе интерьера, толщиной и типом перегородки, привязана к его этажам
        // («только этот» или «и все выше» — как выбрано в окне). Высоту и копии потом берёт от здания сверка привязки.
        public static SplineWall CreatePartition(LiveBuilderBuildingContext context, Vector3 worldPosition, string name)
        {
            var building = context.Building;
            var wall = CreateWall(context.Model, worldPosition, building.transform.rotation);
            wall.name = name;
            wall.Thickness = LiveBuilderWallSettings.PartitionThickness;
            wall.Material = LiveBuilderWallSettings.Material;
            wall.WallType = LiveBuilderWallSettings.PartitionWallType;
            Undo.SetTransformParent(wall.transform, InteriorOf(building), true, "Create Partition");
            SplineBuildingLinkSync.Attach(wall.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys);
            return wall;
        }


        // Новое здание без точек — по настройкам инструмента; высота этажа — общая, из этажей окна.
        public static SplineBuilding CreateBuilding(BoxModel parentModel, Vector3 worldPosition, Quaternion worldRotation)
        {
            var building = CreateBuildingObject(parentModel, worldPosition, worldRotation, "Spline Building");
            building.StoreyCount = LiveBuilderBuildingSettings.StoreyCount;
            building.StoreyHeight = LiveBuilderStoreys.StoreyHeight;
            building.WallThickness = LiveBuilderBuildingSettings.WallThickness;
            building.WallType = LiveBuilderBuildingSettings.WallType;
            building.ContourSide = LiveBuilderBuildingSettings.ContourSide;
            building.SlabThickness = LiveBuilderBuildingSettings.SlabThickness;
            building.HasFloor = LiveBuilderBuildingSettings.HasFloor;
            building.HasRoof = LiveBuilderBuildingSettings.HasRoof;
            building.OuterWallMaterial = LiveBuilderBuildingSettings.OuterWallMaterial;
            building.InnerWallMaterial = LiveBuilderBuildingSettings.InnerWallMaterial;
            building.FloorMaterial = LiveBuilderBuildingSettings.FloorMaterial;
            building.CeilingMaterial = LiveBuilderBuildingSettings.CeilingMaterial;
            building.RoofMaterial = LiveBuilderBuildingSettings.RoofMaterial;
            return building;
        }


        // Комната по контуру в пустоте — здание в один этаж: чем считается контур — по настройке «Стены», пол и потолок по форме.
        // Толщины и материалы — из настроек «Комнаты», как у прямоугольной: снаружи — серый набора, изнутри — стены, пол, потолок комнаты.
        public static SplineBuilding CreateContourRoom(BoxModel parentModel, Vector3 worldPosition, Quaternion worldRotation)
        {
            var room = CreateBuildingObject(parentModel, worldPosition, worldRotation, "Contour Room");
            room.StoreyCount = 1;
            room.StoreyHeight = LiveBuilderStoreys.StoreyHeight;
            room.WallThickness = LiveBuilderRoomSettings.WallThickness;
            room.ContourSide = LiveBuilderRoomWalls.ContourSideFor(LiveBuilderRoomSettings.WallSide);
            room.SlabThickness = LiveBuilderRoomSettings.SlabThickness;
            room.HasFloor = true;
            room.HasRoof = true;
            room.InnerWallMaterial = LiveBuilderRoomSettings.WallMaterial;
            room.FloorMaterial = LiveBuilderRoomSettings.FloorMaterial;
            room.CeilingMaterial = LiveBuilderRoomSettings.CeilingMaterial;
            return room;
        }

        // Объект здания со сплайном без точек: в объекте из коробок, а без него — в группе уровня.
        private static SplineBuilding CreateBuildingObject(BoxModel parentModel, Vector3 worldPosition, Quaternion worldRotation, string name)
        {
            var buildingObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(buildingObject, "Create Building");

            if (parentModel != null)
            {
                buildingObject.transform.SetParent(parentModel.transform, false);
                buildingObject.transform.SetAsLastSibling();
            }
            else
            {
                ObjectFactory.PlaceGameObject(buildingObject, null);

                if (buildingObject.transform.parent == null)
                    PutIntoLevelGeometry(buildingObject);
            }

            buildingObject.transform.SetPositionAndRotation(worldPosition, worldRotation);
            GameObjectUtility.SetStaticEditorFlags(buildingObject, (StaticEditorFlags)~0);

            var container = buildingObject.AddComponent<SplineContainer>();
            container.Spline.Clear();

            return buildingObject.AddComponent<SplineBuilding>();
        }


        // Группа «Interior» здания — перегородки и лестницы, поставленные в нём: едут и удаляются вместе со зданием.
        public static Transform InteriorOf(SplineBuilding building)
        {
            var interior = building.transform.Find(InteriorGroupName);

            if (interior != null)
                return interior;

            var interiorObject = new GameObject(InteriorGroupName);
            Undo.RegisterCreatedObjectUndo(interiorObject, "Create Building Interior");
            interiorObject.transform.SetParent(building.transform, false);
            GameObjectUtility.SetStaticEditorFlags(interiorObject, GameObjectUtility.GetStaticEditorFlags(building.gameObject));
            return interiorObject.transform;
        }


        // Есть ли у здания группа «Interior» — до того, как InteriorOf её создаст.
        public static bool HasInterior(SplineBuilding building)
        {
            return building.transform.Find(InteriorGroupName) != null;
        }


        // Вырез: опора — низ посередине, в мире; в постройке, которую режет, — едет вместе с ней. Только для редактора: в игру идёт прорезанный меш.
        public static GeometryCutter CreateCutter(Transform owner, Vector3 worldBottomCenter, Quaternion worldRotation, Vector2 size, float depth)
        {
            var cutterObject = new GameObject("Cutter") { tag = BoxModelPieceTidying.EditorOnlyTag };

            if (owner != null)
                cutterObject.transform.SetParent(owner, false);

            cutterObject.transform.SetPositionAndRotation(worldBottomCenter, worldRotation);

            var cutter = cutterObject.AddComponent<GeometryCutter>();
            cutter.Width = size.x;
            cutter.Height = size.y;
            cutter.Depth = depth;

            Undo.RegisterCreatedObjectUndo(cutterObject, "Create Cutter");
            return cutter;
        }
    }
}
