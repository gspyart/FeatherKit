using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что встаёт по линии точек: стена или здание.</summary>
    public enum LiveBuilderPathKind
    {
        Wall,
        Building
    }


    /// <summary>
    /// Рисование линии точками для «Стены» и «Здания»: точки по сетке — первая на верх того, что под мышью, иначе на пол этажа; по ним сразу встаёт стена или здание.
    /// - точка — угол или плавно; клик по первой — замкнуть; Enter — готово, Esc — бросить целиком; набранные цифры — точная длина
    /// - в здании — перегородка на его этаже; в выделенном объекте — в нём, а внутри пустоты или выреза — в «Kept From Cuts» (<see cref="LiveBuilderKeptFromCuts"/>)
    /// - готовая линия — один шаг отмены; здание в пустоте — в новом объекте (<see cref="LiveBuilderPathOwnerCreator"/>), точки потом — <see cref="LiveBuilderContourHandles"/>
    /// </summary>
    public sealed class LiveBuilderPathGesture
    {
        // Клик ближе этого к первой точке на экране — замкнуть, пикс. На экране, а не в метрах: при любом зуме одинаково.
        private const float CloseDistancePixels = 12f;

        // То же без окна сцены (вызов из кода), м.
        private const float CloseDistanceWithoutView = 0.3f;

        // Ближе этого к последней точке — та же точка, м.
        private const float SameKnotDistance = 0.005f;

        // Здание — замкнутый контур хотя бы из трёх точек, стена — хотя бы из двух.
        private const int MinClosedPoints = 3;
        private const int MinWallPoints = 2;

        // Отрезок к мыши, с которым контур здания перекрестит сам себя.
        private static readonly Color CrossingColor = new(1f, 0.3f, 0.25f);

        private readonly LiveBuilderPathKind kind;

        private Component pathOwner;
        private BoxModel createdModel;
        private Vector3? hoveredWorldPoint;

        // Набранная с клавиатуры длина следующего отрезка; пусто — точка по мыши.
        private readonly LiveBuilderTypedLength typedLength = new();

        // К чему прилипла точка под мышью: подпись у прицела; пусто — просто по сетке.
        private string snapLabel;

        // Направляющая от последней точки, к которой прилипла точка под мышью (<see cref="LiveBuilderLineGuides"/>); нет — null.
        private LiveBuilderLineGuide? activeGuide;

        // Здание, внутри которого рисуется стена: её пол, высота, толщина и копии по этажам — от него.
        private LiveBuilderBuildingContext buildingContext;
        private LiveBuilderBuildingContext hoveredBuildingContext;

        // Стена, начатая под мышью, встанет в «Kept From Cuts»: точка внутри пустоты или выреза объекта.
        private bool isHoverInsideCut;

        // Шаг отмены, с которого началось рисование: готовая линия схлопывается в один шаг — один Ctrl+Z убирает её целиком.
        private int undoGroupAtBegin;

        // Группа «Kept From Cuts», которую создала эта линия: отменили её — пустая группа уходит вместе с ней.
        private Transform keptGroupCreatedByGesture;


        public LiveBuilderPathGesture(LiveBuilderPathKind kind)
        {
            this.kind = kind;
        }


        public bool IsActive => pathOwner != null;

        public string Hint => pathOwner == null
            ? "Click — the first point of " + ShapeName + KeptFromCutsHint + " " + LiveBuilderHintKeys.Shift + (LiveBuilderWallSettings.IsSmooth ? " corner" : " smooth")
            : typedLength.IsTyping
                ? "Length " + typedLength.Text + " m " + LiveBuilderHintKeys.Enter + " point toward the mouse " + LiveBuilderHintKeys.Key("Backspace") + " erase " +
                  LiveBuilderHintKeys.Esc + " cancel length"
                : IsClosedShape
                    ? "Click — next point, the first one closes " + LiveBuilderHintKeys.Enter + " close and done " + LiveBuilderHintKeys.Esc + " drop " +
                      LiveBuilderHintKeys.Undo + " undo point"
                    : "Click — next point, the first one closes " + LiveBuilderHintKeys.Enter + " done " + LiveBuilderHintKeys.Esc + " drop " +
                      LiveBuilderHintKeys.Undo + " undo point";

        private string ShapeName => kind == LiveBuilderPathKind.Building ? "the building outline" : "the wall";

        // Стена внутри пустоты или выреза — говорим заранее, куда она встанет.
        private string KeptFromCutsHint => isHoverInsideCut ? " — inside a cut: goes into '" + LiveBuilderKeptFromCuts.GroupName + "', stays uncut" : "";

        // Здание всегда замыкается.
        private bool IsClosedShape => kind != LiveBuilderPathKind.Wall;

        // Здание, в котором рисуется (или будет рисоваться) стена; у здания-инструмента — никогда. Здание удалили — уже не в нём.
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
            hoveredWorldPoint = TryGetFloorPoint(worldRay, out var point) ? point : null;
            isHoverInsideCut = isStarting && kind == LiveBuilderPathKind.Wall && hoveredWorldPoint is { } floorPoint
                               && LiveBuilderKeptFromCuts.IsInsideCut(NewWallParent(CurrentContext), floorPoint);
        }

        // Куда встанет новая стена: в здании — в его выделенную группу или к нему (интерьер — его ребёнок), иначе — в выделенный объект из коробок.
        private static Transform NewWallParent(LiveBuilderBuildingContext context)
        {
            if (context != null)
            {
                var group = BoxModelFactory.SelectedGroupIn(context.Building);
                return group != null ? group.transform : context.Building.transform;
            }

            var model = BoxModelActions.SelectedModel();
            return model != null ? model.transform : null;
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


        // Пока рисуешь: цифры — длина следующего отрезка, Enter — точка на этой длине или готово, Esc — сбросить длину или бросить линию.
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

            // Esc — передумал: начатая линия уходит целиком, как недорисованная коробка. Оставить — Enter.
            if (key == KeyCode.Escape)
            {
                Abandon();
                return true;
            }

            if (key is not (KeyCode.Return or KeyCode.KeypadEnter))
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


        // Готово: мало точек — линии нет; иначе выделяется. Здание замыкается всегда.
        public void Finish()
        {
            Finish(IsClosedShape);
        }

        // Бросить начатую линию целиком: всё, что сделано с её начала (сама постройка, её точки, её группы), отменяется одним махом.
        public void Abandon()
        {
            if (pathOwner == null)
                return;

            Undo.RevertAllDownToGroup(undoGroupAtBegin);
            pathOwner = null;
            createdModel = null;
            keptGroupCreatedByGesture = null;
            buildingContext = null;
            typedLength.Clear();
        }


        private void Finish(bool isClosing)
        {
            if (pathOwner == null)
                return;

            var minPoints = IsClosedShape ? MinClosedPoints : MinWallPoints;

            if (Container.Spline.Count < minPoints)
            {
                Undo.DestroyObjectImmediate(createdModel != null ? createdModel.gameObject : pathOwner.gameObject);
                DestroyIfEmpty(keptGroupCreatedByGesture);
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
            keptGroupCreatedByGesture = null;
            buildingContext = null;
            typedLength.Clear();
        }


        // Группа, которую создала эта линия, без неё осталась пустой.
        private static void DestroyIfEmpty(Transform createdGroup)
        {
            if (createdGroup != null && createdGroup.childCount == 0)
                Undo.DestroyObjectImmediate(createdGroup.gameObject);
        }


        // ── Линия и точки ────────────────────────────────────────────────

        // Новое здание или стена — пока без точек.
        private void Begin(Vector3 worldPoint, LiveBuilderBuildingContext context)
        {
            Undo.IncrementCurrentGroup();
            undoGroupAtBegin = Undo.GetCurrentGroup();
            buildingContext = context;

            switch (kind)
            {
                case LiveBuilderPathKind.Building:
                    Undo.SetCurrentGroupName("Draw Building");
                    pathOwner = LiveBuilderPathOwnerCreator.CreateBuilding(worldPoint, out createdModel);
                    break;

                default:
                    Undo.SetCurrentGroupName("Draw Wall");
                    pathOwner = LiveBuilderPathOwnerCreator.CreateWall(worldPoint, context);
                    break;
            }

            if (kind == LiveBuilderPathKind.Wall && LiveBuilderKeptFromCuts.MoveIfInsideCut(pathOwner.transform, worldPoint))
                keptGroupCreatedByGesture = pathOwner.transform.parent;
        }

        private void AddKnot(Vector3 worldPoint, bool isShiftHeld)
        {
            var isSmooth = LiveBuilderWallSettings.IsSmooth != isShiftHeld;
            var localPoint = pathOwner.transform.InverseTransformPoint(worldPoint);

            // Контур здания — ровно на точке опоры, на высоте первой точки.
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

        // Точка встала бы ровно на последнюю: точки по сетке, так что совпадение — та же клетка.
        private bool IsOnLastKnot(Vector3 worldPoint)
        {
            var spline = Container.Spline;

            if (spline.Count == 0)
                return false;

            var lastKnot = pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            return Vector3.Distance(lastKnot, worldPoint) < SameKnotDistance;
        }


        // Точка на полу по сетке: в здании — на полу его этажа, в его осях, и прилипает к его стенам;
        // иначе — на полу текущего этажа, в осях объекта, в который встанет линия, а без него — мира.
        private bool TryGetFloorPoint(Ray worldRay, out Vector3 worldPoint)
        {
            var context = CurrentContext;
            snapLabel = null;
            activeGuide = null;

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
                snapLabel = (wallPoint - gridPoint).sqrMagnitude > 0.000001f ? "to wall" : LiveBuilderPointSnap.LabelAt(worldPoint);
                worldPoint = ApplyGuides(floor.FrameToWorld, floor.Axis, floor.FrameToWorld.MultiplyPoint3x4(floorPoint), worldPoint);
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
            worldPoint = ApplyGuides(plane.FrameToWorld, plane.Axis, plane.FrameToWorld.MultiplyPoint3x4(localPoint), worldPoint);
            return true;
        }

        // Не прилипла к постройке и стене — к направляющей от последней точки: по оси, под 45°, вдоль и поперёк прошлого отрезка (<see cref="LiveBuilderLineGuides"/>).
        // rawWorldPoint — точка мыши на плоскости, ещё без сетки.
        private Vector3 ApplyGuides(Matrix4x4 frameToWorld, int planeAxis, Vector3 rawWorldPoint, Vector3 worldPoint)
        {
            if (pathOwner == null || snapLabel != null || Container.Spline.Count == 0)
                return worldPoint;

            var spline = Container.Spline;
            var last = pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position);
            var previous = spline.Count >= 2 ? (Vector3?)pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 2].Position) : null;

            if (!LiveBuilderLineGuides.TrySnap(frameToWorld, planeAxis, last, previous, rawWorldPoint, out var snapped, out var guide))
                return worldPoint;

            activeGuide = guide;
            snapLabel = guide.Label;
            return snapped;
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


        // ── Отчёт для панели ─────────────────────────────────────────────

        // Какая точка следующая, отрезок к мыши и вся длина для секции Drawing (<see cref="LiveBuilderDrawingReport"/>); точек сколько угодно. Не рисуешь — null.
        public LiveBuilderDrawingReport Report()
        {
            if (pathOwner == null)
                return null;

            var spline = Container.Spline;
            var typedValues = new List<float>();
            var isLengthTyped = typedLength.TryPeekAll(typedValues);
            var last = spline.Count > 0 ? pathOwner.transform.TransformPoint((Vector3)spline[spline.Count - 1].Position) : (Vector3?)null;
            var segment = isLengthTyped ? typedValues[0] : last is { } lastPoint && hoveredWorldPoint is { } hovered ? Vector3.Distance(lastPoint, hovered) : 0f;
            var total = 0f;

            for (var index = 1; index < spline.Count; index++)
                total += Vector3.Distance(pathOwner.transform.TransformPoint((Vector3)spline[index - 1].Position),
                    pathOwner.transform.TransformPoint((Vector3)spline[index].Position));

            var shapeName = kind == LiveBuilderPathKind.Building ? "outline" : "wall";
            return new LiveBuilderDrawingReport(kind == LiveBuilderPathKind.Building ? "Building outline" : "Wall", spline.Count + 1, 0, "Point " + (spline.Count + 1))
                .WithSize("Segment", segment, isLengthTyped)
                .WithSize("Total", total)
                .WithTyped(typedLength.Text)
                .WithKeys(ReportEnterResult(spline.Count, isLengthTyped, segment, shapeName), typedLength.IsTyping ? "clear the length" : "drop the " + shapeName);
        }

        // Enter: набранная длина — точка на ней; иначе — готово, а точек мало — линии не будет.
        private string ReportEnterResult(int pointCount, bool isLengthTyped, float segment, string shapeName)
        {
            if (typedLength.IsTyping)
                return isLengthTyped ? "point at " + LiveBuilderSceneText.FormatMeters(segment) + " m" : "erase typing";

            var minPoints = IsClosedShape ? MinClosedPoints : MinWallPoints;

            if (pointCount < minPoints)
                return "drop — needs " + minPoints + " points";

            return IsClosedShape ? "close and finish" : "finish the " + shapeName;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Точки, отрезок до мыши с длиной, у здания — ещё и замыкающий отрезок к первой точке; прицел.
        public void DrawPreview()
        {
            if (hoveredWorldPoint is not { } hovered)
                return;

            Handles.color = new Color(1f, 0.85f, 0.2f);
            Handles.DrawWireDisc(hovered, Vector3.up, HandleUtility.GetHandleSize(hovered) * 0.08f);

            var isClosing = pathOwner != null && IsNearFirstKnot(hovered);
            var cursorLabel = isClosing ? "close" : snapLabel;

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

            if (activeGuide is { } guide)
                LiveBuilderLineGuides.Draw(guide, hovered);

            var lengthLabel = isCrossing
                ? "sides will cross — floor and roof won't be built"
                : typedLength.IsTyping
                    ? typedLength.Text + " m ↵"
                    : LiveBuilderSceneText.FormatMeters(Vector3.Distance(last, hovered)) + " m · digits — exact";
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
