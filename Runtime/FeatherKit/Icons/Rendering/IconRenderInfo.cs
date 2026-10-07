using FeatherKit.Helpers;
using FeatherKit.UI;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Что <see cref="IconsRenderer"/> помнит про одну показанную иконку: что показываем,
    /// где держим, какой объект сейчас на экране и на сколько его отодвинули соседи.
    /// </summary>
    internal class IconRenderInfo
    {
        public ScreenPoint Point;

        public RectTransform Prefab;
        public RectTransform EdgePrefab;
        public RectTransform Instance;

        // Ссылкой, а не поиском компонента: сдвиг анимации берут в каждом LateUpdate.
        public IShowHideAnimation Animation;

        public OffscreenMode Offscreen;
        public bool ClampByIconEdge;
        public bool AvoidOverlap;

        // Честное место — где иконка висела бы одна. Расталкивание считается от него,
        // иначе сдвиги копились бы от кадра к кадру.
        public Vector2 Anchored;

        // На сколько отодвинули от соседей. Живёт между кадрами: к новому сдвигу иконка едет.
        public Vector2 Push;
        public bool IsAtEdge;

        // Убрана, но доигрывает уход: за целью она должна ехать по-прежнему.
        public bool IsFading;


        /// <summary>Видна ли на экране прямо сейчас.</summary>
        public bool IsVisible => Instance != null && Instance.gameObject.activeSelf;


        // Сдвиг анимации — последним: место иконки ставим мы, а «откуда она приезжает» — её дело.
        public void ApplyPosition()
        {
            if (Instance == null)
                return;

            var anchored = Anchored + Push;

            Instance.anchoredPosition = Animation != null ? anchored + Animation.PositionOffset : anchored;
        }
    }
}
