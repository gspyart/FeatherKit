namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Что стало с частью префаба, когда его сопоставили с шаблоном.
    /// </summary>
    public enum PrefabPartMatchKind
    {
        /// <summary>Нашлась в шаблоне по тому же пути: теперь приходит оттуда, свои значения — переопределения.</summary>
        FromTemplate,

        /// <summary>В шаблоне такой нет: остаётся добавленной поверх него.</summary>
        Own,

        /// <summary>Есть только в шаблоне и из варианта убрана — префаб остался как был.</summary>
        TemplateExtraRemoved,

        /// <summary>Есть только в шаблоне и достанется префабу вместе с ним.</summary>
        TemplateExtraKept,

        /// <summary>Пропала при переделке — такой результат не сохраняется.</summary>
        Lost
    }
}
