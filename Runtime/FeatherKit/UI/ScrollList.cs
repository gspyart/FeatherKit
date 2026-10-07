using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Прокручиваемый список: окошко с маской, внутри которого едет содержимое.
    /// - отдаёт наружу одно — КУДА класть элементы; что в них, решает наполняющий;
    /// - сбрасывает прокрутку к началу по просьбе наполняющего.
    /// Компонентом, а не разметкой в каждом окне: полос и сеток в игре с десяток.
    /// </summary>
    [FDescription("Прокручиваемый список: окошко с маской, внутри едет содержимое. Отдаёт наружу, куда класть элементы.")]
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollList : MonoBehaviour
    {
        [Tooltip("Куда кладутся элементы. Пусто — берём содержимое самой прокрутки")]
        [SerializeField] private RectTransform items;


        /// <summary>Куда класть элементы.</summary>
        public Transform Items => items != null ? items : ContentOfScroll();

        private ScrollRect Scroll => GetComponent<ScrollRect>();


        // ── Прокрутка ─────────────────────────────────────────────────────

        /// <summary>Вернуть список в начало. Зовёт перенаполнивший: оставшаяся прокрутка показала бы
        /// пустоту там, где раньше был хвост длинного списка.</summary>
        public void ScrollToStart()
        {
            var scroll = Scroll;

            if (scroll == null)
                return;

            // Останавливаем инерцию: без этого список доедет обратно уже после сброса.
            scroll.StopMovement();

            scroll.horizontalNormalizedPosition = 0f;
            scroll.verticalNormalizedPosition = 1f;
        }


        // ── Внутреннее ────────────────────────────────────────────────────

        private Transform ContentOfScroll()
        {
            var scroll = Scroll;
            var content = scroll != null ? scroll.content : null;

            return content != null ? content : transform;
        }
    }
}
