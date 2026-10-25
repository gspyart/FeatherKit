using System;

namespace FeatherKit.Attributes
{
    /// <summary>
    /// Кладёт поле в рамку с заголовком в инспекторе.
    ///
    /// Поля с одним и тем же путём попадают в одну рамку, даже если в коде идут не подряд:
    /// рамка встаёт там, где встретилось её первое поле. Группа в группе — через косую черту:
    /// <c>[FGroupBox("Общее/Камера")]</c>. Рамку можно свернуть по заголовку.
    ///
    /// Работает на компонентах, ассетах и внутри полей-классов. Внутри элементов списков
    /// и полей со своим PropertyDrawer — нет: их рисует Unity, а не FeatherKit.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FGroupBoxAttribute : Attribute
    {
        public const char PathSeparator = '/';


        public FGroupBoxAttribute(string path)
        {
            Path = path;
        }


        /// <summary>Заголовок рамки, а для вложенной — путь: «Общее/Камера».</summary>
        public string Path { get; }
    }
}
