using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция карточки выделенного в панели Live Builder: Construction, Cut, Material, Chamfer, Array, Move и прочие — одна на наследника.
    /// - сама решает, есть ли она у выделенного (<see cref="ShowsFor"/>) и пуста ли — тогда вместо неё строка «+ Add»
    /// - рисует поля для всех выделенных одного вида сразу: разные значения — «—»
    /// Порядок и выбор секций — <see cref="LiveBuilderSelectionCardSet"/>. Панель закрывается — отпустить готовые инспекторы (<see cref="Dispose"/>).
    /// </summary>
    public abstract class LiveBuilderSelectionCard : System.IDisposable
    {
        // Порядок секций у всех видов один: главные числа → что режет → материал → фаска → повтор → перенос.
        protected const int ConstructionOrder = 10;
        protected const int LevelStoreysOrder = 15;
        protected const int PointsOrder = 20;
        protected const int InsideOrder = 30;
        protected const int CutOrder = 40;
        protected const int MaterialOrder = 50;
        protected const int ChamferOrder = 60;
        protected const int ArrayOrder = 70;
        protected const int MoveOrder = 80;
        protected const int GroupOrder = 90;


        // ── Описание ─────────────────────────────────────────────────────

        // Меньше — выше.
        public abstract int Order { get; }

        public abstract string Title { get; }

        // Есть и тогда, когда выделены разные виды: Array и Move.
        public virtual bool IsSharedByAllKinds => false;

        public virtual bool IsOpenByDefault => true;

        // Что делает «+ Add» у пустой секции — во всплывающей подсказке.
        public virtual string AddTooltip => null;


        // ── Что показывать ───────────────────────────────────────────────

        // selected — всё выделенное, не пустое; одного вида, если секция не общая.
        public abstract bool ShowsFor(IReadOnlyList<GameObject> selected);


        // Пустая — одной строкой «+ Add» вместо секции.
        public virtual bool IsEmptyFor(IReadOnlyList<GameObject> selected)
        {
            return false;
        }


        // Серым в заголовке секции: что внутри, пока она свёрнута.
        public virtual string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            return null;
        }


        // ── Рисование ────────────────────────────────────────────────────

        public abstract void Draw(IReadOnlyList<GameObject> selected);


        // «+ Add» у пустой секции; buttonRect — где кнопка, для меню под ней.
        public virtual void Add(IReadOnlyList<GameObject> selected, Rect buttonRect)
        {
        }


        // IDisposable: панель закрывается — отпустить готовые инспекторы.
        public virtual void Dispose()
        {
        }


        // ── Общее для секций ─────────────────────────────────────────────

        // Все выделенные — одного из этих видов.
        protected static bool AreAllOfKind(IReadOnlyList<GameObject> selected, params LiveBuilderSelectionKind[] kinds)
        {
            foreach (var each in selected)
                if (System.Array.IndexOf(kinds, LiveBuilderSelectionKinds.KindOf(each)) < 0)
                    return false;

            return selected.Count > 0;
        }


        // Постройка, чей инспектор показывать: вырез, комната, лестница, колонна, стена, здание, модуль, объект из коробок — первое, что есть на объекте.
        protected static Component OwnComponentOf(GameObject selected)
        {
            if (selected == null)
                return null;

            var candidates = new Component[]
            {
                selected.GetComponent<GeometryCutter>(),
                selected.GetComponent<BoxModelRoom>(),
                selected.GetComponent<BoxModelStairs>(),
                selected.GetComponent<LiveBuilderColumn>(),
                selected.GetComponent<SplineWall>(),
                selected.GetComponent<SplineBuilding>(),
                selected.GetComponent<LiveBuilderModule>(),
                selected.GetComponent<BoxModel>()
            };

            return System.Array.Find(candidates, candidate => candidate != null);
        }


        // Свои постройки всех выделенных одного вида — цели одного инспектора.
        protected static Object[] OwnComponentsOf(IReadOnlyList<GameObject> selected)
        {
            var components = new List<Object>();

            foreach (var each in selected)
            {
                var component = OwnComponentOf(each);

                if (component != null)
                    components.Add(component);
            }

            return components.ToArray();
        }


        // Готовый инспектор для этих целей. Undo и пересборка здания пересоздают объект с тем же номером —
        // готовый инспектор тогда держит мёртвый объект и сыплет ошибками: такой собираем заново.
        protected static Editor CachedInspectorOf(Object[] targets, ref Editor editor)
        {
            if (editor != null && !HasSameTargets(editor, targets))
                DestroyInspector(ref editor);

            if (editor == null)
                editor = Editor.CreateEditor(targets);

            return editor;
        }

        private static bool HasSameTargets(Editor editor, Object[] targets)
        {
            if (editor.targets.Length != targets.Length)
                return false;

            for (var index = 0; index < targets.Length; index++)
                if (editor.targets[index] == null || editor.targets[index] != targets[index])
                    return false;

            return true;
        }


        protected static void DestroyInspector(ref Editor editor)
        {
            if (editor != null)
                Object.DestroyImmediate(editor);

            editor = null;
        }
    }
}
