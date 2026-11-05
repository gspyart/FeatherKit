using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка зоны «Выделено» окна Live Builder: числа и действия одного вида выделенного — коробки, повтора, стены, здания…
    /// Сама решает, есть ли ей что показать. Зона (<see cref="LiveBuilderSelectionZone"/>) находит все карточки сама и рисует их по <see cref="Order"/>.
    /// Новая карточка — новый наследник. Окно закрывается — зона отпускает карточку (<see cref="Dispose"/>).
    /// </summary>
    public abstract class LiveBuilderSelectionCard : System.IDisposable
    {
        // Меньше — выше в зоне.
        public abstract int Order { get; }


        // ── Общее для карточек ───────────────────────────────────────────

        // Постройка, чей инспектор показывать: проём, комната, лестница, стена, здание, объект из коробок — первое, что есть на объекте. Иначе — null.
        protected static Component OwnComponentOf(GameObject selected)
        {
            if (selected == null)
                return null;

            var candidates = new List<Component>
            {
                selected.GetComponent<GeometryCutter>(),
                selected.GetComponent<BoxModelRoom>(),
                selected.GetComponent<BoxModelStairs>(),
                selected.GetComponent<SplineWall>(),
                selected.GetComponent<SplineBuilding>(),
                selected.GetComponent<BoxModel>()
            };

            return candidates.Find(candidate => candidate != null);
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


        // ── Рисование ────────────────────────────────────────────────────

        // Нарисовать про выделенное (selected — активный объект выделения). true — было что показать.
        public abstract bool Draw(GameObject selected);


        // IDisposable: окно закрывается — отпустить готовые инспекторы.
        public virtual void Dispose()
        {
        }
    }
}
