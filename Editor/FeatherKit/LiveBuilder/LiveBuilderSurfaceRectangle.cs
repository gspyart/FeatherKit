using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прямоугольник на поверхности под любым углом — для выреза и коробки на косой стене: зажал на поверхности — первый угол, тянешь — второй.
    /// Плоскость — точно по нормали поверхности под мышью. «Вверх» — к небу; у пола и потолка — вдоль оси Z мира.
    /// Углы прилипают к краям грани (<see cref="LiveBuilderFaceEdges"/>) или к сетке — к тому, что ближе. Сетка вдоль поверхности — от ближайшего края грани,
    /// по высоте — от пола этажа; размеры — целые клетки от первого угла. Прилипший к краю угол помечается в сцене.
    /// Ещё знает толщину того, на чём нарисован, — чтобы резать насквозь.
    /// </summary>
    public sealed class LiveBuilderSurfaceRectangle
    {
        // Насколько глубже толщины ищется обратная сторона, м: дальше — уже не эта стена.
        private const float MaxThickness = 3f;

        // Толщина, когда обратная сторона не нашлась, м: режет обычную стену, но не всё, что за ней.
        private const float FallbackThickness = 0.5f;

        // Круче этого (доля вертикали в нормали) — пол или потолок, а не бок стены.
        private const float WallSideMaxVertical = 0.5f;

        // Грань считается параллельной этой (стороной стены, обратной стороной), если косинус между нормалями больше этого.
        private const float ParallelFaceMinCosine = 0.9f;

        private const float EdgeMarkerSize = 0.07f;

        private static readonly Color EdgeMarkerColor = new(0.2f, 0.9f, 1f);

        private LiveBuilderFaceEdges faceEdges = LiveBuilderFaceEdges.None;


        public bool IsActive { get; private set; }

        // Нормаль поверхности наружу, «вверх» и «вправо», если смотреть на поверхность снаружи.
        public Vector3 Normal { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right => Vector3.Cross(Normal, Up);

        // Первый угол в мире — начало координат прямоугольника.
        public Vector3 Origin { get; private set; }

        // Второй угол в осях прямоугольника (вправо, вверх), м.
        public Vector2 End { get; private set; }

        // Угол прилип к краю грани, а не к сетке, — хоть по одной оси.
        public bool IsOriginOnEdge { get; private set; }
        public bool IsEndOnEdge { get; private set; }

        public Collider SurfaceCollider { get; private set; }

        public Vector2 Min => Vector2.Min(Vector2.zero, End);
        public Vector2 Size => new(Mathf.Abs(End.x), Mathf.Abs(End.y));
        public bool HasArea => Size.x > 0.001f && Size.y > 0.001f;

        // Поворот того, что встанет на прямоугольник: X — вправо, Y — вверх, Z — внутрь поверхности.
        public Quaternion Rotation => Quaternion.LookRotation(-Normal, Up);


        // ── Поверхность ──────────────────────────────────────────────────

        // Видимая поверхность под лучом: спрятанное разрезом над этажом не ловится.
        public static bool TryFindSurface(Ray worldRay, out RaycastHit hit)
        {
            return BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out hit);
        }


        // Постройка, на которой нарисован прямоугольник: новое ложится в неё и едет вместе с ней. Не постройка Live Builder — null.
        public Transform SurfaceOwner
        {
            get
            {
                if (SurfaceCollider == null)
                    return null;

                var pathOwner = LiveBuilderPathOwners.OwnerOf(SurfaceCollider);

                if (pathOwner != null)
                    return pathOwner.transform;

                var model = SurfaceCollider.GetComponentInParent<BoxModel>();
                return model != null ? model.transform : null;
            }
        }


        // «Вверх» на поверхности: к небу, а у пола и потолка, где неба нет, — вдоль Z мира.
        public static Vector3 UpOn(Vector3 normal)
        {
            var up = Vector3.ProjectOnPlane(Vector3.up, normal);
            return up.sqrMagnitude > 0.01f ? up.normalized : Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
        }


        // ── Жест ─────────────────────────────────────────────────────────

        public bool TryBegin(Ray worldRay)
        {
            if (!TryFindSurface(worldRay, out var hit))
                return false;

            Normal = hit.normal;
            Up = UpOn(Normal);
            SurfaceCollider = hit.collider;
            faceEdges = LiveBuilderFaceEdges.Find(hit.collider, hit.point, Normal, Right, Up);
            Origin = SnapOrigin(hit.point);
            End = Vector2.zero;
            IsEndOnEdge = false;
            IsActive = true;
            return true;
        }

        // К краю грани или к сетке. Сетка вдоль поверхности — от ближайшего края грани (нет краёв — от нуля мира);
        // по высоте — от пола этажа (низ двери встаёт на пол), у пола и потолка — от ближайшего края.
        private Vector3 SnapOrigin(Vector3 point)
        {
            var step = LiveBuilderGrid.CurrentStep;
            var tolerance = LiveBuilderFaceEdges.SnapToleranceAt(point);
            var onFace = OnFace(point);

            var alongGridOrigin = faceEdges.TryFindNearestEdge(0, onFace, tolerance, out var sideEdge) ? sideEdge : 0f;
            var along = faceEdges.Snap(0, onFace, alongGridOrigin, step, tolerance, out var isAlongOnEdge);
            var height = faceEdges.Snap(1, onFace, HeightGridOrigin(point, onFace, tolerance), step, tolerance, out var isHeightOnEdge);

            IsOriginOnEdge = isAlongOnEdge || isHeightOnEdge;
            return point + Right * (along - onFace.x) + Up * (height - onFace.y);
        }

        // Откуда сетка по высоте, в координате «вверх»: на стене — где поверхность пересекает пол этажа, иначе — ближайший край грани.
        private float HeightGridOrigin(Vector3 point, Vector2 onFace, float tolerance)
        {
            if (Mathf.Abs(Up.y) > 0.5f)
                return onFace.y + (LiveBuilderStoreys.CurrentFloorHeight - point.y) / Up.y;

            return faceEdges.TryFindNearestEdge(1, onFace, tolerance, out var edge) ? edge : 0f;
        }


        // Второй угол — где луч мыши пересекает плоскость прямоугольника: к краю грани или к сетке от первого угла (размер — целыми клетками).
        public void Update(Ray worldRay)
        {
            if (!IsActive || !new Plane(Normal, Origin).Raycast(worldRay, out var enter))
                return;

            var mousePoint = worldRay.GetPoint(enter);
            var mouse = OnFace(mousePoint);
            var origin = OnFace(Origin);
            var step = LiveBuilderGrid.CurrentStep;
            var tolerance = LiveBuilderFaceEdges.SnapToleranceAt(mousePoint);

            var along = faceEdges.Snap(0, mouse, origin.x, step, tolerance, out var isAlongOnEdge);
            var height = faceEdges.Snap(1, mouse, origin.y, step, tolerance, out var isHeightOnEdge);
            End = new Vector2(along - origin.x, height - origin.y);
            IsEndOnEdge = isAlongOnEdge || isHeightOnEdge;
        }


        public void Cancel()
        {
            IsActive = false;
            IsOriginOnEdge = false;
            IsEndOnEdge = false;
            SurfaceCollider = null;
            faceEdges = LiveBuilderFaceEdges.None;
        }


        // Точка прямоугольника в мире по его осям.
        public Vector3 PointAt(Vector2 rectanglePoint)
        {
            return Origin + Right * rectanglePoint.x + Up * rectanglePoint.y;
        }


        // Точка мира в координатах грани: проекции на «вправо» и «вверх», м.
        private Vector2 OnFace(Vector3 worldPoint)
        {
            return new Vector2(Vector3.Dot(worldPoint, Right), Vector3.Dot(worldPoint, Up));
        }


        // ── Метка края ───────────────────────────────────────────────────

        // Угол, прилипший к краю грани, — голубой точкой: видно, что встал вровень с краем, а не по сетке.
        public void DrawEdgeMarkers()
        {
            if (!IsActive)
                return;

            Handles.color = EdgeMarkerColor;

            if (IsOriginOnEdge)
                DrawEdgeMarker(Origin);

            if (IsEndOnEdge)
                DrawEdgeMarker(PointAt(End));
        }

        private static void DrawEdgeMarker(Vector3 point)
        {
            Handles.DotHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * EdgeMarkerSize, EventType.Repaint);
        }


        // ── Толщина ──────────────────────────────────────────────────────

        // Толщина того, на чём нарисован прямоугольник, в точке, м. Бок стены по линии или стены здания — толщина из их настроек:
        // лучом её не всегда поймать (в узкой шахте луч задевает соседнюю стену). Прочее — лучами до обратной стороны.
        public float ThicknessAt(Vector3 surfacePoint)
        {
            if (SurfaceCollider == null)
                return FallbackThickness;

            return TryGetWallThickness(surfacePoint, out var wallThickness) ? wallThickness : MeasureThickness(surfacePoint);
        }

        // Стена по линии или стены этажа здания, и под точкой — сторона стены, а не откос проёма или торец: толщина из настроек.
        private bool TryGetWallThickness(Vector3 surfacePoint, out float thickness)
        {
            thickness = 0f;

            if (Mathf.Abs(Normal.y) >= WallSideMaxVertical)
                return false;

            var owner = LiveBuilderPathOwners.OwnerOf(SurfaceCollider);
            float settingsThickness;

            if (owner is SplineWall wall)
                settingsThickness = wall.Thickness;
            else if (owner is SplineBuilding building && SurfaceCollider.GetComponentInParent<SplineBuildingPart>() is { Kind: SplineBuildingPartKind.Walls })
                settingsThickness = building.WallThickness;
            else
                return false;

            var localNormal = owner.transform.InverseTransformDirection(Normal);

            if (!IsWallSide(owner, owner.transform.InverseTransformPoint(surfacePoint), localNormal))
                return false;

            thickness = owner.transform.TransformVector(localNormal * settingsThickness).magnitude;
            return true;
        }

        // Нормаль грани — поперёк ближайшего к точке пути стены, то есть это её сторона.
        private static bool IsWallSide(Component owner, Vector3 localPoint, Vector3 localNormal)
        {
            var nearestGap = float.MaxValue;
            var isSide = false;

            foreach (var path in LiveBuilderPathOwners.PathsOf(owner))
            {
                if (!path.IsUsable)
                    continue;

                var along = path.ClosestDistanceTo(localPoint, out var gap);

                if (gap >= nearestGap)
                    continue;

                nearestGap = gap;
                isSide = Mathf.Abs(Vector3.Dot(localNormal, path.SideNormalAt(along))) > ParallelFaceMinCosine;
            }

            return isSide;
        }


        // До обратной стороны того же коллайдера: изнутри наружу (ловим изнанку), не вышло — снаружи навстречу.
        // Засчитывается только грань, параллельная этой: иначе луч задел что-то сбоку. Не нашлась — FallbackThickness.
        private float MeasureThickness(Vector3 surfacePoint)
        {
            var wasHittingBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var hasBack = SurfaceCollider.Raycast(new Ray(surfacePoint - Normal * 0.001f, -Normal), out var back, MaxThickness);
            Physics.queriesHitBackfaces = wasHittingBackfaces;

            if (hasBack && Mathf.Abs(Vector3.Dot(back.normal, Normal)) > ParallelFaceMinCosine)
                return back.distance + 0.001f;

            var hasFront = SurfaceCollider.Raycast(new Ray(surfacePoint - Normal * MaxThickness, Normal), out var front, MaxThickness);

            return hasFront && Vector3.Dot(front.normal, Normal) < -ParallelFaceMinCosine && front.distance < MaxThickness - 0.001f
                ? MaxThickness - front.distance
                : FallbackThickness;
        }
    }
}
