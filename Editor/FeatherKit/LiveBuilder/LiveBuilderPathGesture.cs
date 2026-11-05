using System.Collections.Generic;
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
    /// Рисование линии точками для инструментов «Стена», «Здание» и «Комната» по контуру: кликами точки по сетке — первая на верх того, что под мышью
    /// (коробки, крыши, перекрытия), а под мышью пусто или стена сбоку — на пол текущего этажа; дальше линия идёт на высоте первой точки,
    /// и по ним сразу встаёт стена (<see cref="SplineWall"/>), здание (<see cref="SplineBuilding"/>) или комната.
    /// Точка — угол или плавный изгиб: по переключателю в окне, Shift на клике — наоборот.
    /// Клик по первой точке — замкнуть; Enter — готово, Esc — бросить отрезок за мышью (здание и комната замыкаются сами); Ctrl+Z, пока рисуешь, — убрать последнюю точку.
    /// Отрезок, с которым контур здания или комнаты перекрестит сам себя, — красный, с подписью.
    /// Точная длина, как в SketchUp (<see cref="LiveBuilderTypedLength"/>): набрал цифры — Enter ставит следующую точку на этом расстоянии в сторону мыши.
    /// У мыши подпись, к чему прилипло: «к стене» здания, «замкнуть» у первой точки.
    /// Готовая линия — один шаг отмены: Ctrl+Z после неё убирает всю стену, здание или комнату, без пустых объектов.
    /// Стена внутри здания (<see cref="LiveBuilderBuildingContext"/>) — перегородка: на полу его этажа, до потолка, толщиной перегородки,
    /// точки прилипают к стенам здания, на этом этаже или на всех выше.
    /// Комната: в здании — замкнутая перегородка, стороны вдоль его стен не строятся; в доме из коробок — стены до его потолка;
    /// в пустоте — здание в один этаж. Куда встают её стены от линии — наружу, внутрь или по центру — по настройке «Линия по стенам» (<see cref="LiveBuilderRoomWalls"/>).
    /// Выделен объект из коробок — линия встаёт в него и его вырезающие коробки режут и её. Здание и комната в пустоте — в новом объекте из коробок:
    /// так в них сразу работают лестницы и вырезы (<see cref="LiveBuilderPathOwnerCreator"/>). Двигать точки потом — <see cref="LiveBuilderContourHandles"/>.
    /// </summary>
    public sealed class LiveBuilderPathGesture
    {
        // Клик ближе этого к первой точке на экране — замкнуть, пикс. На экране, а не в метрах: при любом зуме одинаково.
        private const float CloseDistancePixels = 12f;

        // То же без окна сцены (вызов из кода), м.
        private const float CloseDistanceWithoutView = 0.3f;

        // Ближе этого к последней точке — та же точка, м.
        private const float SameKnotDistance = 0.005f;

        // Здание и комната — замкнутый контур хотя бы из трёх точек, стена — хотя бы из двух.
        private const int MinClosedPoints = 3;
        private const int MinWallPoints = 2;

        // Отрезок к мыши, с которым контур здания или комнаты перекрестит сам себя.
        private static readonly Color CrossingColor = new(1f, 0.3f, 0.25f);

        private readonly LiveBuilderPathKind kind;

        private Component pathOwner;
        private BoxModel createdModel;
        private Vector3? hoveredWorldPoint;

        // Набранная с клавиатуры длина следующего отрезка; пусто — точка по мыши.
        private readonly LiveBuilderTypedLength typedLength = new();

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

        // Группа «Interior», которую создала эта перегородка: отменили её — пустая группа уходит вместе с ней.
        private Transform interiorCreatedByGesture;


        public LiveBuilderPathGesture(LiveBuilderPathKind kind)
        {
            this.kind = kind;
        }


        public bool IsActive => pathOwner != null;

        public string Hint => pathOwner == null
            ? "Клик — первая точка " + ShapeName + " на полу этажа" + RoomPlaceHint + ". Shift — точка наоборот: " +
              (LiveBuilderWallSettings.IsSmooth ? "углом" : "плавно")
            : typedLength.IsTyping
                ? "Длина " + typedLength.Text + " м: Enter — точка на этом расстоянии в сторону мыши, Backspace — стереть цифру, Esc — отмена длины"
                : IsClosedShape
                    ? "Клик — следующая точка контура, цифры + Enter — точная длина. По первой точке или Enter — замкнуть и готово. Esc — тоже готово, Ctrl+Z — убрать последнюю"
                    : "Клик — следующая точка, цифры + Enter — точная длина, по первой — замкнуть. Enter — готово; Esc — бросить отрезок за мышью, точки остаются; Ctrl+Z — убрать последнюю";

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

            // Второй клик двойного щелчка или клик в ту же клетку — не вторая точка поверх последней.
            if (IsOnLastKnot(worldPoint))
                return;

            AddKnot(worldPoint, isShiftHeld);
        }


        // Пока рисуешь: цифры — длина следующего отрезка, Enter — точка на этой длине или готово, Esc — сбросить длину или готово.
        // Возвращает, забрал ли клавишу.
        public bool HandleKey(KeyCode key)
        {
            if (pathOwner == null)
                return false;

            if (typedLength.TryType(key))
                return true;

            if (key is KeyCode.Return or KeyCode.KeypadEnter && typedLength.IsTyping)
            {
                AddKnotAtTypedLength();
                return true;
            }

            if (key == KeyCode.Escape && typedLength.IsTyping)
            {
                typedLength.Clear();
                return true;
            }

            if (key is not (KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape))
                return false;

            Finish(IsClosedShape);
            return true;
        }

        // Следующая точка на набранной длине — от последней точки в сторону мыши, по горизонтали.
        private void AddKnotAtTypedLength()
        {
            if (!typedLength.TryTake(out var length) || hoveredWorldPoint is not { } hovered)
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

                if (interiorCreatedByGesture != null && interiorCreatedByGesture.childCount == 0)
                    Undo.DestroyObjectImmediate(interiorCreatedByGesture.gameObject);
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
            interiorCreatedByGesture = null;
            buildingContext = null;
            typedLength.Clear();
        }


        // ── Линия и точки ────────────────────────────────────────────────

        // Новое здание, стена или комната — пока без точек.
        private void Begin(Vector3 worldPoint, LiveBuilderBuildingContext context)
        {
            Undo.IncrementCurrentGroup();
            undoGroupAtBegin = Undo.GetCurrentGroup();
            buildingContext = context;
            var hadInterior = context != null && BoxModelFactory.HasInterior(context.Building);

            switch (kind)
            {
                case LiveBuilderPathKind.Building:
                    Undo.SetCurrentGroupName("Draw Building");
                    pathOwner = LiveBuilderPathOwnerCreator.CreateBuilding(worldPoint, out createdModel);
                    break;

                case LiveBuilderPathKind.Room:
                    Undo.SetCurrentGroupName("Draw Room");
                    pathOwner = LiveBuilderPathOwnerCreator.CreateRoom(worldPoint, context, hoveredHouse, hoveredHouseCeiling, out createdModel);
                    break;

                default:
                    Undo.SetCurrentGroupName("Draw Wall");
                    pathOwner = LiveBuilderPathOwnerCreator.CreateWall(worldPoint, context);
                    break;
            }

            // Перегородка встаёт в группу «Interior» здания; не было её — создала эта перегородка.
            var group = pathOwner.transform.parent;
            interiorCreatedByGesture = context != null && !hadInterior && group != null && group.parent == context.Building.transform ? group : null;
        }


        private void AddKnot(Vector3 worldPoint, bool isShiftHeld)
        {
            var isSmooth = LiveBuilderWallSettings.IsSmooth != isShiftHeld;
            var localPoint = pathOwner.transform.InverseTransformPoint(worldPoint);

            // Контур здания и комнаты в пустоте — ровно на точке опоры, на высоте первой точки.
            if (pathOwner is SplineBuilding)
                localPoint.y = 0f;

            Undo.RecordObject(Container, "Add Path Point");
            Container.Spline.Add(new BezierKnot(localPoint), isSmooth ? TangentMode.AutoSmooth : TangentMode.Linear);
            ApplyRoomWallSide();
        }

        // Комната из стены по линии — сторона стены от линии по настройке «Линия по стенам» и по тому, в какую сторону обходишь комнату. Пересчёт на каждой точке:
        // пока рисуешь, видно, куда встанут стены.
        private void ApplyRoomWallSide()
        {
            if (kind != LiveBuilderPathKind.Room || pathOwner is not SplineWall walls)
                return;

            var alignment = LiveBuilderRoomWalls.AlignmentFor(Container.Spline, LiveBuilderRoomSettings.WallSide);

            if (walls.Alignment == alignment)
                return;

            Undo.RecordObject(walls, "Add Path Point");
            walls.Alignment = alignment;
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

        // Точка встала бы ровно на последнюю: точки по сетке, так что совпадение — та же клетка.
        private bool IsOnLastKnot(Vector3 worldPoint)
        {
            var spline = Container.Spline;

            if (spline.Count == 0)
                return false;

            var lastKnot = pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            return Vector3.Distance(lastKnot, worldPoint) < SameKnotDistance;
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
                worldPoint = floor.FrameToWorld.MultiplyPoint3x4(wallPoint);
                snapLabel = (wallPoint - gridPoint).sqrMagnitude > 0.000001f ? "к стене" : LiveBuilderPointSnap.LabelAt(worldPoint);
                return true;
            }

            if (pathOwner == null && TryGetSurfaceTopPoint(worldRay, out worldPoint))
                return true;

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
            snapLabel = LiveBuilderPointSnap.LabelAt(worldPoint);
            return true;
        }

        // Первая точка — на верх того, что под мышью (коробки, крыши, перекрытия, любой геометрии), если он смотрит вверх: по сетке в плане,
        // а по высоте — ровно на поверхности, без округления к сетке. Дальше линия идёт на этой высоте. Под мышью стена сбоку или пусто — false: тогда пол этажа.
        private static bool TryGetSurfaceTopPoint(Ray worldRay, out Vector3 worldPoint)
        {
            worldPoint = default;

            if (!BoxModelDrawPlaneFinder.TryFind(worldRay, false, out var plane, out var localPoint, true) || plane.Axis != 1 || plane.OutwardSign < 0f)
                return false;

            worldPoint = plane.FrameToWorld.MultiplyPoint3x4(localPoint);
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
                LiveBuilderSceneText.DrawLabel(hovered + Vector3.up * HandleUtility.GetHandleSize(hovered) * 0.25f, cursorLabel);

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
            var isCrossing = IsClosedShape && !isClosing && IsCrossingOwnSides(hovered);

            if (isCrossing)
                Handles.color = CrossingColor;

            Handles.DrawLine(last, hovered, 2f);

            var lengthLabel = isCrossing
                ? "стороны перекрестятся — пол и крыша не построятся"
                : typedLength.IsTyping
                    ? typedLength.Text + " м ↵"
                    : LiveBuilderSceneText.FormatMeters(Vector3.Distance(last, hovered)) + " м · цифры — точно";
            LiveBuilderSceneText.DrawLabel((last + hovered) / 2f, lengthLabel);

            if (IsClosedShape && spline.Count >= 2)
                Handles.DrawDottedLine(hovered, first, 4f);
        }

        // Новая точка там, где мышь, — и контур перекрестит сам себя: отрезок к ней или замыкающий к первой точке крест-накрест со сторонами.
        private bool IsCrossingOwnSides(Vector3 hovered)
        {
            var spline = Container.Spline;
            var points = new List<Vector2>();

            for (var index = 0; index < spline.Count; index++)
                points.Add(Flat(pathOwner.transform.TransformPoint((Vector3)spline[index].Position)));

            points.Add(Flat(hovered));
            return points.Count >= 4 && PlanPolygons.IsSelfCrossing(points);
        }

        private static Vector2 Flat(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }
    }
}
