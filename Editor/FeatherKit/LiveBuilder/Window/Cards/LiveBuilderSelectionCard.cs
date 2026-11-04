using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка зоны «Выделено» окна Live Builder: числа и действия одного вида выделенного — коробки, повтора, стены, здания…
    /// Сама решает, есть ли ей что показать. Зона (<see cref="LiveBuilderSelectionZone"/>) находит все карточки сама и рисует их по <see cref="Order"/>.
    /// Новая карточка — новый наследник.
    /// </summary>
    public abstract class LiveBuilderSelectionCard
    {
        // Меньше — выше в зоне.
        public abstract int Order { get; }


        // ── Рисование ────────────────────────────────────────────────────

        // Нарисовать про выделенное (selected — активный объект выделения). true — было что показать.
        public abstract bool Draw(GameObject selected);


        // Окно закрывается — отпустить готовые инспекторы.
        public virtual void Dispose()
        {
        }


        // Инспектор компонента внутри окна. Undo и пересборка здания пересоздают объект с тем же номером —
        // готовый инспектор тогда держит мёртвый объект и сыплет ошибками: такой собираем заново.
        protected static void DrawCachedInspector(Component component, System.Type editorType, ref Editor editor)
        {
            if (editor != null && (editor.target == null || editor.target != component))
            {
                Object.DestroyImmediate(editor);
                editor = null;
            }

            Editor.CreateCachedEditor(component, editorType, ref editor);
            editor.OnInspectorGUI();
        }


        // Убрать готовый инспектор, если он есть.
        protected static void DestroyInspector(ref Editor editor)
        {
            if (editor != null)
                Object.DestroyImmediate(editor);

            editor = null;
        }
    }
}
