using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вид модификатора для окна Feather Modifiers (<see cref="FeatherModifiersWindow"/>): кнопка, как найти его на объекте,
    /// поставить, нарисовать настройки, применить и снять. Новый модификатор — наследник этого класса: окно найдёт его само
    /// и поставит кнопку по <see cref="Order"/>. Сам модификатор — компонент на объекте, его настройки — в нём.
    /// </summary>
    public abstract class FeatherModifierKind
    {
        private static List<FeatherModifierKind> allKinds;


        // Все виды модификаторов по порядку кнопок в окне.
        public static IReadOnlyList<FeatherModifierKind> All
        {
            get
            {
                if (allKinds != null)
                    return allKinds;

                allKinds = new List<FeatherModifierKind>();

                foreach (var type in TypeCache.GetTypesDerivedFrom<FeatherModifierKind>())
                    if (!type.IsAbstract)
                        allKinds.Add((FeatherModifierKind)Activator.CreateInstance(type));

                allKinds.Sort((first, second) => first.Order.CompareTo(second.Order));
                return allKinds;
            }
        }

        public abstract string Title { get; }

        public abstract string Tooltip { get; }

        // Меньше — левее кнопка и выше карточка в окне.
        public virtual int Order => 100;


        // Модификатор этого вида на объекте; нет — null.
        public abstract Component FindOn(GameObject target);


        // Можно ли поставить: по умолчанию — если такого ещё нет.
        public virtual bool CanAddTo(GameObject target)
        {
            return FindOn(target) == null;
        }


        public abstract void AddTo(GameObject target);


        public abstract void DrawSettings(Component modifier);


        // Результат остаётся, модификатор снимается.
        public abstract void Apply(Component modifier);


        // Модификатор и всё, что он сделал, — прочь: объект как был.
        public abstract void Remove(Component modifier);
    }
}
