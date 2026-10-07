using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.UI
{
    /// <summary>
    /// Ряд одинаковых элементов из одного префаба: сколько попросили — столько и видно.
    /// - заводит недостающие и сразу отдаёт их владельцу — подписаться до первого показа;
    /// - лишние прячет или уничтожает, как выбрал владелец;
    /// - отдаёт видимые по номеру и находит номер элемента.
    /// Часть, а не компонент: держит её полем окно, оно же и наполняет элементы.
    /// </summary>
    public class ViewList<TView> where TView : Component
    {
        private readonly TView prefab;
        private readonly Transform root;
        private readonly ViewListSurplus surplus;
        private readonly Action<TView> created;
        private readonly Action<TView> destroying;

        private readonly List<TView> views = new List<TView>();

        private int count;


        /// <param name="created">Новый элемент: подписаться на него, пока он ничего не показал.</param>
        /// <param name="destroying">Элемент сейчас уничтожат: снять подписку.</param>
        public ViewList(TView prefab, Transform root, ViewListSurplus surplus,
            Action<TView> created = null, Action<TView> destroying = null)
        {
            this.prefab = prefab;
            this.root = root;
            this.surplus = surplus;
            this.created = created;
            this.destroying = destroying;
        }


        /// <summary>Сколько элементов видно сейчас.</summary>
        public int Count => count;

        /// <summary>Видимый элемент по номеру, от нуля до <see cref="Count"/>.</summary>
        public TView this[int index] => views[index];


        // ── Число элементов ───────────────────────────────────────────────

        /// <summary>
        /// Показать ровно столько элементов. Выжившие остаются на местах: пересоздавать всё
        /// на каждую правку — способ получить дёргающийся экран и мусор в памяти.
        /// </summary>
        public void Resize(int wanted)
        {
            wanted = Mathf.Max(0, wanted);

            if (surplus == ViewListSurplus.Destroy)
                DestroySurplus(wanted);

            while (views.Count < wanted && prefab != null && root != null)
                Create();

            count = Mathf.Min(wanted, views.Count);

            if (surplus == ViewListSurplus.Hide)
                ShowFirst(count);
        }

        private void DestroySurplus(int wanted)
        {
            while (views.Count > wanted)
            {
                var last = views.Count - 1;
                var view = views[last];

                views.RemoveAt(last);

                if (view == null)
                    continue;

                destroying?.Invoke(view);

                Object.Destroy(view.gameObject);
            }
        }

        private void Create()
        {
            var view = Object.Instantiate(prefab, root);

            view.name = $"{prefab.name}_{views.Count}";

            created?.Invoke(view);

            views.Add(view);
        }

        private void ShowFirst(int shown)
        {
            for (var i = 0; i < views.Count; i++)
            {
                var visible = i < shown;

                if (views[i] != null && views[i].gameObject.activeSelf != visible)
                    views[i].gameObject.SetActive(visible);
            }
        }


        // ── Поиск ─────────────────────────────────────────────────────────

        /// <summary>Номер видимого элемента. Минус единица — элемент не наш или спрятан.</summary>
        public int IndexOf(TView view)
        {
            var index = views.IndexOf(view);

            return index < count ? index : -1;
        }
    }


    /// <summary>Что делать с элементами сверх нужного числа.</summary>
    public enum ViewListSurplus
    {
        /// <summary>Спрятать до следующего раза: список часто растёт и сжимается.</summary>
        Hide,

        /// <summary>Уничтожить: список меняется редко, а держать лишнее незачем.</summary>
        Destroy
    }
}
