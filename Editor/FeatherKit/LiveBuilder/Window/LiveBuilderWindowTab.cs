using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вкладка окна Live Builder: название, значок и цвет на корешке, пометка на корешке, содержимое и когда открываться самой.
    /// Вкладка знает, что в ней; корешки, «?» и место рисует окно (<see cref="LiveBuilderWindow"/>, вид — <see cref="LiveBuilderTabBar"/>).
    /// Новая вкладка — новый наследник: окно находит его само и ставит по <see cref="Order"/>.
    /// </summary>
    public abstract class LiveBuilderWindowTab : System.IDisposable
    {
        // ── Корешок ──────────────────────────────────────────────────────

        public abstract string Title { get; }

        // Белый значок: окно красит его цветом вкладки, на открытой — белым.
        public abstract Texture2D Icon { get; }

        public abstract LiveBuilderTabColor TabColor { get; }

        // Короткая пометка на корешке, например сколько выделено; null — без неё.
        public virtual string Badge => null;

        // Можно ли открыть сейчас: нельзя — корешок бледный и не нажимается, открыта первая доступная вкладка.
        public virtual bool IsAvailable => true;

        // Почему нельзя открыть — во всплывающей подсказке бледного корешка.
        public virtual string UnavailableReason => null;


        // ── Место ────────────────────────────────────────────────────────

        // Меньше — левее.
        public abstract int Order { get; }

        // Под этим ключом помнится, открыта ли вкладка и включены ли на ней пояснения. Не менять: иначе у всех они сбросятся.
        public abstract string PreferenceKey { get; }


        // ── Открывается сама ─────────────────────────────────────────────

        // Сменилось выделение — открыться ли. Только если включено «Переключать вкладки сами».
        public virtual bool ShouldOpenWhenSelectionChanges => false;

        // Выбран инструмент — открыться ли.
        public virtual bool ShouldOpenWhenToolChanges => false;


        // ── Содержимое ───────────────────────────────────────────────────

        // Содержимое открытой вкладки.
        public abstract void Draw();


        // IDisposable: окно закрывается — отпустить своё (готовые инспекторы и прочее).
        public virtual void Dispose()
        {
        }
    }
}
