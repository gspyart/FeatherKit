using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Цветная зона окна Live Builder: заголовок со значком и пояснением, сворачивается кликом и помнит это (<see cref="LiveBuilderZoneFrame"/>).
    /// Зона знает, что в ней и когда её показывать; рамку, цвет и место рисует окно (<see cref="LiveBuilderWindow"/>).
    /// Новая зона — новый наследник: окно находит его само и ставит по <see cref="Order"/>.
    /// </summary>
    public abstract class LiveBuilderWindowZone : System.IDisposable
    {
        // ── Заголовок ────────────────────────────────────────────────────

        public abstract string Title { get; }

        // Серое пояснение после названия; может зависеть от того, что выделено и выбрано.
        public abstract string Detail { get; }

        // Белый значок для цветного квадрата в заголовке.
        public abstract Texture2D Icon { get; }

        public abstract LiveBuilderZoneColor ZoneColor { get; }


        // ── Место ────────────────────────────────────────────────────────

        // Меньше — выше в окне.
        public abstract int Order { get; }

        // Под этим ключом помнится, свёрнута ли зона. Не менять: иначе у всех она снова развернётся.
        public abstract string PreferenceKey { get; }

        public virtual bool IsExpandedByDefault => true;

        // Прокручивается вместе с остальными; false — прибита сверху, под палитрой.
        public virtual bool IsScrolled => true;

        // Есть ли что показать сейчас; нет — зоны в окне нет совсем.
        public virtual bool IsVisible => true;


        // ── Содержимое ───────────────────────────────────────────────────

        // Содержимое развёрнутой зоны.
        public abstract void Draw();


        // IDisposable: окно закрывается — отпустить своё (готовые инспекторы и прочее).
        public virtual void Dispose()
        {
        }
    }
}
