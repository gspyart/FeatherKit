using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что встаёт по линии точек: стена, здание или комната по контуру.</summary>
    public enum LiveBuilderPathKind
    {
        Wall,
        Building,
        Room
    }


    /// <summary>
    /// Рисование линии точками для инструментов «Стена», «Здание» и «Комната» по контуру: кликами точки на полу текущего этажа, по сетке,
    /// и по ним сразу встаёт стена (<see cref="SplineWall"/>), здание (<see cref="SplineBuilding"/>) или комната.
    /// Точка — угол или плавный изгиб: по переключателю в окне, Shift на клике — наоборот.
    /// Клик по первой точке — замкнуть; Enter или Esc — готово (здание и комната замыкаются сами); Ctrl+Z, пока рисуешь, — убрать последнюю точку.
    /// Точная длина, как в SketchUp: набрал цифры — Enter ставит следующую точку на этом расстоянии в сторону мыши.
    /// У мыши подпись, к чему прилипло: «к стене» здания, «замкнуть» у первой точки.
    /// Готовая линия — один шаг отмены: Ctrl+Z после неё убирает всю стену, здание или комнату, без пустых объектов.
    /// Стена внутри здания (<see cref="LiveBuilderBuildingContext"/>) — перегородка: на полу его этажа, до потолка, толщиной перегородки,
    /// точки прилипают к стенам здания, на этом этаже или на всех выше.
    /// Комната: в здании — замкнутая перегородка, стороны вдоль его стен не строятся; в доме из коробок — стены до его потолка;
    /// в пустоте — здание в один этаж, где контур — внутренняя сторона стен.
    /// Выделен объект из коробок — линия встаёт в него и его вырезающие коробки режут и её. Здание и комната в пустоте — в новом объекте из коробок:
    /// так в них сразу работают лестницы и вырезы. Двигать точки потом — <see cref="LiveBuilderContourHandles"/>.
    /// </summary>
    public sealed class LiveBuilderPathGesture
    {
        // Клик ближе этого к первой точке на экране — замкнуть, пикс. На экране, а не в метрах: при любом зуме одинаково.
        private const float CloseDistancePixels = 12f;

        // То же без окна сцены (вызов из кода), м.
        private const float CloseDistanceWithoutView = 0.3f;

        // Здание и комната — замкнутый контур хотя бы из трёх точек, стена — хотя бы из двух.
        private const int MinClosedPoints = 3;
        private const int MinWallPoints = 2;

        private readonly LiveBuilderPathKind kind;

        private Component pathOwner;
        private BoxModel createdModel;
        private Vector3? hoveredWorldPoint;

        // Набранная с клавиатуры длина следующего отрезка, м; пусто — точка по мыши.
        private string typedLength = "";

        // К чему прилипла точка под мышью: подпись у прицела; пусто — просто по сетке.
        private string snapLabel;

        // Здание, внутри которого рисуется стена: её пол, высота, толщина и копии по этажам — от него.
        private LiveBuilderBuildingContext buildingContext;
        private LiveBuilderBuildingContext hoveredBuildingContext;

        // Пол дома из коробок под мышью и высота до его потолка, м: комната по контуру встанет там стенами до потолка.
        private BoxModel hoveredHouse;
        private float hoveredHouseCeiling;

        // Шаг отмены, с которого началось рисование: готовая линия схлопывается в один шаг — один Ctrl+Z убирает её целиком.
        private int undoGroupAtBegin;


        public LiveBuilderPathGesture(LiveBuilderPathKind kind)
        {
            this.kind = kind;
        }


        public bool IsActive => pathOwner != null;

        public string Hint => pathOwner == null
            ? "Клик — первая точка " + ShapeName + " на полу этажа" + RoomPlaceHint + ". Shift — точка наоборот: " +
              (LiveBuilderWallSettings.IsSmooth ? "углом" : "плавно")
            : typedLength.Length > 0
                ? "Длина " + typedLength + " м: Enter — точка на этом расстоянии в сторону мыши, Backspace — стереть цифру, Esc — отмена длины"
                : IsClosedShape
                    ? "Клик — следующая точка контура, цифры + Enter — точная длина. По первой точке или Enter — замкнуть и готово. Esc — тоже готово, Ctrl+Z — убрать последнюю"
                    : "Клик — следующая точка, цифры + Enter — точная длина, по первой — замкнуть. Enter или Esc — готово, Ctrl+Z — убрать последнюю";

        private string ShapeName => kind switch
        {
            LiveBuilderPathKind.Building => "контура здания",
            LiveBuilderPathKind.Room => "контура комнаты",
            _ => "стены"
        };

        // Что встанет по контуру комнаты там, где мышь.
        private string RoomPlaceHint => kind != LiveBuilderPathKind.Room ? ""
            : CurrentContext != null ? ": в здании — перегородки, вдоль его стен не строятся"
            : hoveredHouse != null ? ": в доме — стены до его потолка"
            : ": стены, пол и потолок вокруг";

        // Здание и комната всегда замыкаются.
        private bool IsClosedShape => kind != LiveBuilderPathKind.Wall;

        // Здание, в котором рисуется (или будет рисоваться) стена или комната; у здания-инструмента — никогда. Здание удалили — уже не в нём.
        private LiveBuilderBuildingContext CurrentContext
        {
            get
            {
                var context = kind == LiveBuilderPathKind.Building ? null : pathOwner != null ? buildingContext : hoveredBuildingContext;
                return context is { IsBuildingAlive: true } ? context : null;
            }
        }

        private SplineContainer Container => pathOwner.GetComponent<SplineContainer>();


        // ── Мышь и клавиши ───────────────────────────────────────────────

        public void UpdateHover(Ray worldRay)
        {
            var isStarting = pathOwner == null;
            hoveredBuildingContext = isStarting && kind != LiveBuilderPathKind.Building && LiveBuilderBuildingContext.TryFind(worldRay, out var context) ? context : null;
            hoveredHouse = null;

            if (isStarting && kind == LiveBuilderPathKind.Room && CurrentContext == null && LiveBuilderFloorRectangle.TryFindHouseFloor(worldRay, out var house, out _, out var ceiling))
            {
                hoveredHouse = house;
                hoveredHouseCeiling = ceiling;
            }

            hoveredWorldPoint = TryGetFloorPoint(worldRay, out var point) ? point : null;
        }


        // Клик: новая линия с первой точкой или следующая точка; по первой точке — замкнуть.
        public void Click(Ray worldRay, bool isShiftHeld)
        {
            if (pathOwner == null)
                UpdateHover(worldRay);

            if (!TryGetFloorPoint(worldRay, out var worldPoint))
                return;

            if (pathOwner == null)
            {
                Begin(worldPoint, CurrentContext);
                AddKnot(worldPoint, isShiftHeld);
                return;
            }

            if (IsNearFirstKnot(worldPoint))
            {
                Finish(true);
                return;
            }

            AddKnot(worldPoint, isShiftHeld);
        }


        // Пока рисуешь: цифры — длина следующего отрезка, Enter — точка на этой длине или готово, Esc — сбросить длину или готово.
        // Возвращает, забрал ли клавишу.
        public bool HandleKey(KeyCode key)
        {
            if (pathOwner == null)
                return false;

            if (TryTypeLength(key))
                return true;

            if (key is KeyCode.Return or KeyCode.KeypadEnter && typedLength.Length > 0)
            {
                AddKnotAtTypedLength();
                return true;
            }

            if (key == KeyCode.Escape && typedLength.Length > 0)
            {
                typedLength = "";
                return true;
            }

            if (key is not (KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape))
                return false;

            Finish(IsClosedShape);
            return true;
        }

        // Цифра, точка или запятая — в длину; Backspace — стереть последнюю.
        private bool TryTypeLength(KeyCode key)
        {
            var character = key switch
            {
                >= KeyCode.Alpha0 and <= KeyCode.Alpha9 => (char)('0' + (key - KeyCode.Alpha0)),
                >= KeyCode.Keypad0 and <= KeyCode.Keypad9 => (char)('0' + (key - KeyCode.Keypad0)),
                KeyCode.Period or KeyCode.Comma or KeyCode.KeypadPeriod => ',',
                _ => '\0'
            };

            if (character == ',' && typedLength.Contains(","))
                return true;

            if (character != '\0')
            {
                typedLength += character;
                return true;
            }

            if (key != KeyCode.Backspace || typedLength.Length == 0)
                return false;

            typedLength = typedLength.Substring(0, typedLength.Length - 1);
            return true;
        }

        // Следующая точка на набранной длине — от последней точки в сторону мыши, по горизонтали.
        private void AddKnotAtTypedLength()
        {
            var isNumber = float.TryParse(typedLength.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var length);
            typedLength = "";

            if (!isNumber || length <= 0.001f || hoveredWorldPoint is not { } hovered)
                return;

            var spline = Container.Spline;
            var last = pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            var direction = Vector3.ProjectOnPlane(hovered - last, Vector3.up);

            if (direction.sqrMagnitude < 0.0001f)
                return;

            AddKnot(last + direction.normalized * length, false);
        }


        // Готово: мало точек — линии нет; иначе выделяется. Здание и комната замыкаются всегда.
        public void Finish()
        {
            Finish(IsClosedShape);
        }

        private void Finish(bool isClosing)
        {
            if (pathOwner == null)
                return;

            var minPoints = IsClosedShape ? MinClosedPoints : MinWallPoints;

            if (Container.Spline.Count < minPoints)
            {
                Undo.DestroyObjectImmediate(createdModel != null ? createdModel.gameObject : pathOwner.gameObject);
            }
            else
            {
                if (isClosing && !Container.Spline.Closed)
                {
                    Undo.RecordObject(Container, "Close Path");
                    Container.Spline.Closed = true;
                }

                Selection.activeGameObject = pathOwner.gameObject;
            }

            Undo.CollapseUndoOperations(undoGroupAtBegin);
            pathOwner = null;
            createdModel = null;
            buildingContext = null;
            typedLength = "";
        }


        // ── Линия и точки ────────────────────────────────────────────────

        // Новое здание, стена или комната — пока без точек.
        private void Begin(Vector3 worldPoint, LiveBuilderBuildingContext context)
        {
            Undo.IncrementCurrentGroup();
            undoGroupAtBegin = Undo.GetCurrentGroup();
            buildingContext = context;

            switch (kind)
            {
                case LiveBuilderPathKind.Building:
                    Undo.SetCurrentGroupName("Draw Building");
                    pathOwner = BeginBuilding(worldPoint);
                    break;

                case LiveBuilderPathKind.Room:
                    Undo.SetCurrentGroupName("Draw Room");
                    pathOwner = BeginRoom(worldPoint, context);
                    break;

                default:
                    Undo.SetCurrentGroupName("Draw Wall");
                    pathOwner = BeginWall(worldPoint, context);
                    break;
            }
        }

        // Здание — в выделенном объекте из коробок, а без него — в новом. Пол первого этажа — ровно на полу этажа уровня:
        // точка опоры здания — низ плиты пола, на её толщину ниже.
        private SplineBuilding BeginBuilding(Vector3 worldPoint)
        {
            var parentModel = SelectedOrNewModel(worldPoint, "Building");
            var floorThickness = LiveBuilderBuildingSettings.HasFloor ? LiveBuilderBuildingSettings.SlabThickness : 0f;
            return BoxModelFactory.CreateBuilding(parentModel, worldPoint + Vector3.down * floorThickness, parentModel.transform.rotation);
        }

        // Стена внутри здания — перегородка: в его объекте, до потолка этажа, толщиной перегородки, на этом или всех этажах выше.
        // Иначе — стена по настройкам, в выделенном объекте из коробок, если он есть.
        private static SplineWall BeginWall(Vector3 worldPoint, LiveBuilderBuildingContext context)
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

        // В здании — замкнутая перегородка-комната: стороны вдоль его стен не строятся. В доме из коробок — стены перегородки до его потолка.
        // В пустоте — комната со стенами, полом и потолком, как у здания: пол — на полу этажа.
        private Component BeginRoom(Vector3 worldPoint, LiveBuilderBuildingContext context)
        {
            if (context != null)
            {
                var room = BoxModelFactory.CreatePartition(context, worldPoint, "Room");
                room.SkipsSidesAlongBuildingWalls = true;
                return room;
            }

            if (hoveredHouse != null)
            {
                var walls = BoxModelFactory.CreateWall(hoveredHouse, worldPoint, hoveredHouse.transform.rotation);
                walls.name = "Room";
                walls.Thickness = LiveBuilderWallSettings.PartitionThickness;
                walls.Material = LiveBuilderWallSettings.Material;
                walls.WallType = LiveBuilderWallSettings.PartitionWallType;
                walls.Height = hoveredHouseCeiling;
                return walls;
            }

            var parentModel = SelectedOrNewModel(worldPoint, "Room");
            return BoxModelFactory.CreateContourRoom(parentModel, worldPoint + Vector3.down * LiveBuilderRoomSettings.SlabThickness, parentModel.transform.rotation);
        }

        // Выделенный объект из коробок, а без него — новый: так в здании или комнате сразу работают лестницы и вырезы.
        private BoxModel SelectedOrNewModel(Vector3 worldPoint, string name)
        {
            var selected = BoxModelActions.SelectedModel();

            if (selected != null)
                return selected;

            createdModel = BoxModelFactory.CreateModel(worldPoint);
            createdModel.name = name;
            return createdModel;
        }


        private void AddKnot(Vector3 worldPoint, bool isShiftHeld)
        {
            var isSmooth = LiveBuilderWallSettings.IsSmooth != isShiftHeld;
            var localPoint = pathOwner.transform.InverseTransformPoint(worldPoint);

            // Контур здания и комнаты в пустоте — на точке опоры, а кликаешь по полу этажа выше неё на толщину плиты.
            if (pathOwner is SplineBuilding)
                localPoint.y = 0f;

            Undo.RecordObject(Container, "Add Path Point");
            Container.Spline.Add(new BezierKnot(localPoint), isSmooth ? TangentMode.AutoSmooth : TangentMode.Linear);
        }

        private bool IsNearFirstKnot(Vector3 worldPoint)
        {
            var spline = Container.Spline;

            if (spline.Count < MinClosedPoints)
                return false;

            var firstKnot = pathOwner.transform.TransformPoint((Vector3)spline[0].Position);

            if (Event.current == null || Camera.current == null)
                return Vector3.Distance(firstKnot, worldPoint) < CloseDistanceWithoutView;

            var screenDistance = (HandleUtility.WorldToGUIPoint(firstKnot) - HandleUtility.WorldToGUIPoint(worldPoint)).magnitude;
            return screenDistance < CloseDistancePixels;
        }


        // Точка на полу по сетке: в здании — на полу его этажа, в его осях, и прилипает к его стенам; первая точка комнаты в доме из коробок — на его полу;
        // иначе — на полу текущего этажа, в осях объекта, в который встанет линия, а без него — мира.
        private bool TryGetFloorPoint(Ray worldRay, out Vector3 worldPoint)
        {
            var context = CurrentContext;
            snapLabel = null;

            if (pathOwner == null && hoveredHouse != null && LiveBuilderFloorRectangle.TryFindHouseFloor(worldRay, out _, out worldPoint, out _))
                return true;

            if (context != null)
            {
                var floor = context.FloorPlane;

                if (!floor.TryIntersect(worldRay, out var floorPoint))
                {
                    worldPoint = default;
                    return false;
                }

                var gridPoint = floor.SnapOnPlane(floorPoint);
                var wallPoint = context.SnapToWalls(gridPoint);
                snapLabel = (wallPoint - gridPoint).sqrMagnitude > 0.000001f ? "к стене" : null;
                worldPoint = floor.FrameToWorld.MultiplyPoint3x4(wallPoint);
                return true;
            }

            var model = pathOwner != null ? SplineWallRebuilder.OuterModelOf(pathOwner) : BoxModelActions.SelectedModel();
            var plane = BoxModelDrawPlaneFinder.StoreyFloorPlane(model);

            if (pathOwner != null)
                plane = new BoxModelDrawPlane(model, plane.FrameToWorld, 1, plane.FrameToWorld.inverse.MultiplyPoint3x4(pathOwner.transform.position).y, 1f);

            if (!plane.TryIntersect(worldRay, out var localPoint))
            {
                worldPoint = default;
                return false;
            }

            worldPoint = plane.FrameToWorld.MultiplyPoint3x4(plane.SnapOnPlane(localPoint));
            return true;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Точки, отрезок до мыши с длиной, у здания и комнаты — ещё и замыкающий отрезок к первой точке; прицел.
        public void DrawPreview()
        {
            if (hoveredWorldPoint is not { } hovered)
                return;

            Handles.color = new Color(1f, 0.85f, 0.2f);
            Handles.DrawWireDisc(hovered, Vector3.up, HandleUtility.GetHandleSize(hovered) * 0.08f);

            var isClosing = pathOwner != null && IsNearFirstKnot(hovered);
            var cursorLabel = isClosing ? "замкнуть" : snapLabel;

            if (cursorLabel != null)
                BoxModelSceneText.DrawLabel(hovered + Vector3.up * HandleUtility.GetHandleSize(hovered) * 0.25f, cursorLabel);

            if (pathOwner == null || Container.Spline.Count == 0)
                return;

            var spline = Container.Spline;

            for (var index = 0; index < spline.Count; index++)
            {
                var knot = pathOwner.transform.TransformPoint((Vector3)spline[index].Position);
                Handles.DrawSolidDisc(knot, Vector3.up, HandleUtility.GetHandleSize(knot) * (index == 0 ? 0.07f : 0.05f));
            }

            var first = pathOwner.transform.TransformPoint((Vector3)spline[0].Position);
            var last = pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            Handles.DrawLine(last, hovered, 2f);

            var lengthLabel = typedLength.Length > 0
                ? typedLength + " м ↵"
                : BoxModelSceneText.FormatMeters(Vector3.Distance(last, hovered)) + " м · цифры — точно";
            BoxModelSceneText.DrawLabel((last + hovered) / 2f, lengthLabel);

            if (IsClosedShape && spline.Count >= 2)
                Handles.DrawDottedLine(hovered, first, 4f);
        }
    }
}
