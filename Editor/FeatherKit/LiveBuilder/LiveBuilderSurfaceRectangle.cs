using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прямоугольник на поверхности под любым углом — для выреза и коробки на косой стене: зажал на поверхности — первый угол, тянешь — второй.
    /// Плоскость — точно по нормали поверхности под мышью. «Вверх» — к небу; у пола и потолка — вдоль оси Z мира.
    /// Первый угол прилипает к сетке: по высоте — от пола этажа, вдоль поверхности — по сетке мира; размеры — целые клетки сетки.
    /// Ещё знает толщину того, на чём нарисован, — чтобы резать насквозь.
    /// </summary>
    public sealed class LiveBuilderSurfaceRectangle
    {
        // Насколько глубже толщины ищется обратная сторона, м: дальше — уже не эта стена.
        private const float MaxThickness = 3f;


        public bool IsActive { get; private set; }

        // Нормаль поверхности наружу, «вверх» и «вправо», если смотреть на поверхность снаружи.
        public Vector3 Normal { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right => Vector3.Cross(Normal, Up);

        // Первый угол в мире — начало координат прямоугольника.
        public Vector3 Origin { get; private set; }

        // Второй угол в осях прямоугольника (вправо, вверх), м.
        public Vector2 End { get; private set; }

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
            Origin = SnapOrigin(hit.point);
            End = Vector2.zero;
            IsActive = true;
            return true;
        }

        // Высота — по сетке от пола этажа (низ двери встаёт на пол), вдоль поверхности — по сетке мира.
        private Vector3 SnapOrigin(Vector3 point)
        {
            var step = LiveBuilderGrid.CurrentStep;
            var alongRight = Vector3.Dot(point, Right);
            point += Right * (Mathf.Round(alongRight / step) * step - alongRight);

            if (Mathf.Abs(Up.y) > 0.5f)
            {
                var floor = LiveBuilderStoreys.CurrentFloorHeight;
                var snappedHeight = floor + Mathf.Round((point.y - floor) / step) * step;
                point += Up * ((snappedHeight - point.y) / Up.y);
            }
            else
            {
                var alongUp = Vector3.Dot(point, Up);
                point += Up * (Mathf.Round(alongUp / step) * step - alongUp);
            }

            return point;
        }


        // Второй угол — где луч мыши пересекает плоскость прямоугольника, размер — целыми клетками сетки.
        public void Update(Ray worldRay)
        {
            if (!IsActive || !new Plane(Normal, Origin).Raycast(worldRay, out var enter))
                return;

            var offset = worldRay.GetPoint(enter) - Origin;
            var step = LiveBuilderGrid.CurrentStep;
            End = new Vector2(Mathf.Round(Vector3.Dot(offset, Right) / step) * step, Mathf.Round(Vector3.Dot(offset, Up) / step) * step);
        }


        public void Cancel()
        {
            IsActive = false;
            SurfaceCollider = null;
        }


        // Точка прямоугольника в мире по его осям.
        public Vector3 PointAt(Vector2 rectanglePoint)
        {
            return Origin + Right * rectanglePoint.x + Up * rectanglePoint.y;
        }


        // ── Толщина ──────────────────────────────────────────────────────

        // Толщина того, на чём нарисован прямоугольник, в точке: до обратной стороны того же коллайдера, м. Не нашлась — MaxThickness.
        public float ThicknessAt(Vector3 surfacePoint)
        {
            if (SurfaceCollider == null)
                return MaxThickness;

            // Изнутри стены наружу — обратная сторона видна изнанкой: просим физику ловить и изнанку.
            var wasHittingBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var hasBack = SurfaceCollider.Raycast(new Ray(surfacePoint - Normal * 0.001f, -Normal), out var back, MaxThickness);
            Physics.queriesHitBackfaces = wasHittingBackfaces;

            if (hasBack)
                return back.distance + 0.001f;

            // Изнанку не поймали — заходим с той стороны: первая грань навстречу — обратная сторона стены.
            return SurfaceCollider.Raycast(new Ray(surfacePoint - Normal * MaxThickness, Normal), out var front, MaxThickness)
                ? MaxThickness - front.distance
                : MaxThickness;
        }
    }
}
