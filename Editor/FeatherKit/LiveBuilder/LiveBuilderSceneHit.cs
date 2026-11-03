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
            Component pathOwner, Component holeCutter, float holeCutterDistance)
        {
            HasBoxSurface = hasBoxSurface;
            BoxSurface = boxSurface;
            HasVisibleHit = hasVisibleHit;
            VisibleHit = visibleHit;
            PathOwner = pathOwner;
            HoleCutter = holeCutter;
            HoleCutterDistance = holeCutterDistance;
        }


        public bool HasBoxSurface { get; }
        public BoxModelSurfaceHit BoxSurface { get; }

        // Ближайший видимый коллайдер под лучом — любой: стена, здание, пропс.
        public bool HasVisibleHit { get; }
        public RaycastHit VisibleHit { get; }

        // Стена по линии или здание, если ближайший коллайдер — их. Иначе — null.
        public Component PathOwner { get; }

        // Резчик дыры под лучом и расстояние до входа в его коробку. Нет — null.
        public Component HoleCutter { get; }
        public float HoleCutterDistance { get; }

        // Стена по линии или здание ближе грани объекта из коробок.
        public bool IsPathOwnerCloser => PathOwner != null && (!HasBoxSurface || VisibleHit.distance < BoxSurface.Distance - Tolerance);

        // Резчик ближе всего остального: его дыру и видно — клик по ней — он.
        public bool IsHoleCutterInFront
        {
            get
            {
                if (HoleCutter == null)
                    return false;

                var nearest = Mathf.Min(HasBoxSurface ? BoxSurface.Distance : float.MaxValue, HasVisibleHit ? VisibleHit.distance : float.MaxValue);
                return HoleCutterDistance < nearest + Tolerance;
            }
        }


        // Разобрать луч мыши.
        public static LiveBuilderSceneHit Find(Ray worldRay)
        {
            var hasBoxSurface = BoxModelPicker.TryPickSurface(worldRay, out var boxSurface);
            var hasVisibleHit = BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var visibleHit);
            var pathOwner = hasVisibleHit ? LiveBuilderPathOwners.OwnerOf(visibleHit.collider) : null;
            var hasHoleCutter = HoleCutterPicker.TryPick(worldRay, out var holeCutter, out var holeCutterDistance);

            return new LiveBuilderSceneHit(hasBoxSurface, boxSurface, hasVisibleHit, visibleHit, pathOwner,
                hasHoleCutter ? holeCutter : null, hasHoleCutter ? holeCutterDistance : float.MaxValue);
        }
    }
}
