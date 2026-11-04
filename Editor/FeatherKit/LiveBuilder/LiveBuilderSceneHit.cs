using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что под мышью — один разбор луча на все инструменты Live Builder: грань объекта из коробок (<see cref="BoxModelPicker"/>),
    /// ближайший видимый коллайдер и, если это стена по линии или здание, они сами (<see cref="LiveBuilderPathOwners"/>),
    /// резчик дыры под лучом — вырез или вырезающая коробка в здании (<see cref="HoleCutterPicker"/>). И кто из них ближе к камере.
    /// Спрятанное разрезом над этажом не ловится.
    /// </summary>
    public readonly struct LiveBuilderSceneHit
    {
        // Ближе на столько — считается ближе, м: грани на одном месте не спорят.
        private const float Tolerance = 0.01f;


        private LiveBuilderSceneHit(bool hasBoxSurface, BoxModelSurfaceHit boxSurface, bool hasVisibleHit, RaycastHit visibleHit,
            Component pathOwner, bool hasHoleCutter, HoleCutterRayHit holeCutterHit)
        {
            HasBoxSurface = hasBoxSurface;
            BoxSurface = boxSurface;
            HasVisibleHit = hasVisibleHit;
            VisibleHit = visibleHit;
            PathOwner = pathOwner;
            HasHoleCutter = hasHoleCutter;
            HoleCutterHit = holeCutterHit;
        }


        public bool HasBoxSurface { get; }
        public BoxModelSurfaceHit BoxSurface { get; }

        // Ближайший видимый коллайдер под лучом — любой: стена, здание, пропс.
        public bool HasVisibleHit { get; }
        public RaycastHit VisibleHit { get; }

        // Стена по линии или здание, если ближайший коллайдер — их. Иначе — null.
        public Component PathOwner { get; }

        // Резчик дыры, в чью коробку входит луч, и где луч его пересекает.
        public bool HasHoleCutter { get; }
        public HoleCutterRayHit HoleCutterHit { get; }

        // Резчик под лучом. Нет — null.
        public Component HoleCutter => HasHoleCutter ? HoleCutterHit.Cutter : null;

        // Стена по линии или здание ближе грани объекта из коробок.
        public bool IsPathOwnerCloser => PathOwner != null && (!HasBoxSurface || VisibleHit.distance < BoxSurface.Distance - Tolerance);

        // Видна дыра резчика: клик по ней или её откосу — он (<see cref="HoleCutterRayHit.IsVisibleBefore"/>).
        // Ближе всего — геометрия самого объекта, на котором стоит вырез (балкон, прорезающий дверь в стене), — она, а не вырез:
        // свой объект вырез не режет, и его грани выделяются как обычно.
        public bool IsHoleCutterInFront
        {
            get
            {
                if (!HasHoleCutter)
                    return false;

                var nearest = Mathf.Min(HasBoxSurface ? BoxSurface.Distance : float.MaxValue, HasVisibleHit ? VisibleHit.distance : float.MaxValue);
                return !IsHoleCutterOwnGeometryAt(nearest) && HoleCutterHit.IsVisibleBefore(nearest);
            }
        }

        private bool IsHoleCutterOwnGeometryAt(float distance)
        {
            var cutterTransform = HoleCutter.transform;
            var isOwnBoxSurface = HasBoxSurface && BoxSurface.Distance < distance + Tolerance && BoxSurface.Model.transform.IsChildOf(cutterTransform);
            var isOwnCollider = HasVisibleHit && VisibleHit.distance < distance + Tolerance && VisibleHit.collider.transform.IsChildOf(cutterTransform);
            return isOwnBoxSurface || isOwnCollider;
        }


        // Разобрать луч мыши.
        public static LiveBuilderSceneHit Find(Ray worldRay)
        {
            var hasBoxSurface = BoxModelPicker.TryPickSurface(worldRay, out var boxSurface);
            var hasVisibleHit = BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var visibleHit);
            var pathOwner = hasVisibleHit ? LiveBuilderPathOwners.OwnerOf(visibleHit.collider) : null;
            var hasHoleCutter = HoleCutterPicker.TryPick(worldRay, out var holeCutterHit);

            return new LiveBuilderSceneHit(hasBoxSurface, boxSurface, hasVisibleHit, visibleHit, pathOwner, hasHoleCutter, holeCutterHit);
        }
    }
}
