using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Attributes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует описание под шапкой ассета и выделенного объекта — у всего, что помечено
    /// <see cref="FDescriptionAttribute"/>.
    ///
    /// Через событие шапки, а не своим инспектором: событие добавляет строку поверх любого
    /// инспектора, ничего у него не отбирая. Так конфиги со своими инспекторами — тун-шейдер,
    /// сторонние пакеты с атрибутами — остаются как были.
    ///
    /// До компонентов ВНУТРИ инспектора событие не доходит: оно зовётся только для верхнего
    /// объекта. Ими занимается <see cref="FDescriptionInspector"/>.
    ///
    /// Атрибуты кешируем по типу: шапка перерисовывается на каждом движении мыши,
    /// а рефлексия там ни к чему.
    /// </summary>
    [InitializeOnLoad]
    internal static class FDescriptionHeader
    {
        private static readonly Dictionary<Type, string> descriptions = new Dictionary<Type, string>();


        static FDescriptionHeader()
        {
            // Снимаем перед подпиской: статический конструктор зовётся и после
            // перезагрузки домена, и без этого строка нарисовалась бы дважды.
            Editor.finishedDefaultHeaderGUI -= Draw;
            Editor.finishedDefaultHeaderGUI += Draw;
        }


        /// <summary>
        /// Описание этого объекта или null. Общее на двоих: тем же ответом пользуется
        /// <see cref="FDescriptionInspector"/>, и кеш у них один.
        /// </summary>
        internal static string DescriptionOf(UnityEngine.Object target)
        {
            return target != null ? DescriptionOf(target.GetType()) : null;
        }

        private static void Draw(Editor editor)
        {
            var description = DescriptionOf(editor != null ? editor.target : null);

            if (string.IsNullOrEmpty(description))
                return;

            // Без иконки: это пояснение, а не предупреждение, и восклицательный знак
            // рядом с каждым компонентом читался бы как ошибка.
            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private static string DescriptionOf(Type type)
        {
            if (descriptions.TryGetValue(type, out var cached))
                return cached;

            var attribute = type.GetCustomAttribute<FDescriptionAttribute>(true);
            var text = attribute != null ? attribute.Text : null;

            descriptions[type] = text;

            return text;
        }
    }
}
