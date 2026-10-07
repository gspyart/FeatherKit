using System;
using System.Collections.Generic;

namespace FeatherKit.Registry
{
    /// <summary>
    /// Список живых объектов одного типа: враги на арене, активные снаряды, разрушаемые
    /// предметы. Заводи отдельный экземпляр под каждый нужный список, а не один общий
    /// реестр на все типы сразу.
    ///
    /// Объекты регистрируются сами (обычно в OnEnable) и снимаются в OnDisable —
    /// реестр никого не ищет по сцене.
    ///
    /// По реестру можно спокойно ходить foreach'ем и убивать объекты прямо в теле цикла:
    /// снятые во время обхода вычёркиваются после него, а не сдвигают список под ногами.
    /// Ради этого он и написан вместо голого HashSet — на нём такой цикл падает.
    /// </summary>
    public class TypeRegistry<T> where T : class
    {
        // Два хранилища: список задаёт порядок обхода и терпит дырки, множество отвечает
        // за «уже есть?» и за честный Count.
        private readonly List<T> items = new List<T>();
        private readonly HashSet<T> lookup = new HashSet<T>();

        private int iterationDepth;
        private bool hasHoles;


        public int Count => lookup.Count;

        /// <summary>Для тех, кому нужен именно интерфейс. Для перебора бери сам реестр.</summary>
        public IReadOnlyCollection<T> All => lookup;


        // Дубликаты и null игнорируются.
        public bool Register(T item)
        {
            if (item == null || !lookup.Add(item))
                return false;

            items.Add(item);

            return true;
        }

        public bool Unregister(T item)
        {
            if (item == null || !lookup.Remove(item))
                return false;

            var index = items.IndexOf(item);

            if (index < 0)
                return true;

            // Во время обхода список не трогаем — оставляем дырку и убираем её, когда
            // обход закончится. Иначе снятие из тела цикла сдвинуло бы хвост, и соседний
            // объект пропустили бы вовсе.
            if (iterationDepth > 0)
            {
                items[index] = null;
                hasHoles = true;
            }
            else
            {
                items.RemoveAt(index);
            }

            return true;
        }

        public bool Contains(T item) => item != null && lookup.Contains(item);

        public void Clear()
        {
            items.Clear();
            lookup.Clear();
            hasHoles = false;
        }

        /// <summary>Перебор без мусора: энумератор — структура, боксить нечего.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);


        private void BeginIteration()
        {
            iterationDepth++;
        }

        // Дырки убираем на выходе из самого внешнего обхода: вложенный цикл по тому же
        // реестру ещё идёт по этому же списку, и сжимать его под ним нельзя.
        private void EndIteration()
        {
            if (iterationDepth > 0)
                iterationDepth--;

            if (iterationDepth > 0 || !hasHoles)
                return;

            items.RemoveAll(item => item == null);
            hasHoles = false;
        }


        /// <summary>
        /// Энумератор реестра. IDisposable не для ресурсов, а чтобы foreach сообщил о конце
        /// обхода даже при break или исключении — иначе реестр остался бы с дырками.
        /// </summary>
        public struct Enumerator : IDisposable
        {
            private readonly TypeRegistry<T> registry;

            private int index;
            private bool finished;


            internal Enumerator(TypeRegistry<T> registry)
            {
                this.registry = registry;
                index = -1;
                finished = false;
                Current = null;

                registry.BeginIteration();
            }


            public T Current { get; private set; }


            public bool MoveNext()
            {
                var source = registry.items;

                while (++index < source.Count)
                {
                    Current = source[index];

                    // Дырка от объекта, снятого прямо из тела цикла.
                    if (Current != null)
                        return true;
                }

                Current = null;

                // Обход дошёл до конца сам — закрываем его сразу, не дожидаясь Dispose:
                // цикл могли написать руками, через while (e.MoveNext()).
                Finish();

                return false;
            }

            public void Dispose() => Finish();


            private void Finish()
            {
                if (finished || registry == null)
                    return;

                finished = true;

                registry.EndIteration();
            }
        }
    }
}
