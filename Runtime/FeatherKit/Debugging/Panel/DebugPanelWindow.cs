using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Одно окно отладки: заголовок и строки «свойство — значение». Только данные,
    /// рисует их <see cref="DebugPanelDrawer"/>.
    ///
    /// Значение хранится функцией, а не числом: владелец пишет строку один раз, а панель
    /// опрашивает её сама. Иначе каждому пришлось бы обновлять свои значения каждый кадр
    /// и не забывать это делать.
    /// </summary>
    public class DebugPanelWindow
    {
        private readonly List<Row> rows = new List<Row>();


        public string Title { get; }

        public DebugAnchor Anchor { get; internal set; }

        public int RowCount => rows.Count;


        internal DebugPanelWindow(string title, DebugAnchor anchor)
        {
            Title = title;
            Anchor = anchor;
        }


        public string LabelAt(int index) => rows[index].Label;

        /// <summary>
        /// Значение строки, уже готовое к отрисовке. Падение в функции значения гасим:
        /// объект мог уехать в пул или умереть, а панель из-за этого разваливаться
        /// не должна — тем более что исключение из OnGUI ломает всю верстку кадра.
        /// </summary>
        public string ValueAt(int index)
        {
            try
            {
                return Format(rows[index].Value?.Invoke());
            }
            catch (Exception exception)
            {
                return $"ошибка: {exception.GetType().Name}";
            }
        }


        // Повторная запись той же пары «окно + свойство» заменяет строку, а не плодит вторую:
        // регистрацию часто зовут из OnEnable, а он случается не один раз за жизнь объекта.
        internal void Set(object owner, string label, Func<object> value)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Label != label)
                    continue;

                rows[i] = new Row(owner, label, value);
                return;
            }

            rows.Add(new Row(owner, label, value));
        }

        internal void Forget(object owner)
        {
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(rows[i].Owner, owner))
                    rows.RemoveAt(i);
            }
        }


        // Числа с хвостом в двадцать знаков читать невозможно, а именно они на панели и живут.
        private static string Format(object value)
        {
            switch (value)
            {
                case null: return "—";
                case bool flag: return flag ? "да" : "нет";
                case float number: return number.ToString("0.##");
                case double number: return number.ToString("0.##");
                case Vector2 vector: return vector.ToString("0.##");
                case Vector3 vector: return vector.ToString("0.##");
                default: return value.ToString();
            }
        }


        private readonly struct Row
        {
            public readonly object Owner;
            public readonly string Label;
            public readonly Func<object> Value;


            public Row(object owner, string label, Func<object> value)
            {
                Owner = owner;
                Label = label;
                Value = value;
            }
        }
    }
}
