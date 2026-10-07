namespace FeatherKit.Health
{
    /// <summary>Что сделать со значением здоровья.</summary>
    public enum HealthChange
    {
        /// <summary>Прибавить: лечение, аптечка.</summary>
        Add = 0,

        /// <summary>Отнять: урон, яд, плата здоровьем.</summary>
        Subtract,

        /// <summary>Умножить: 0.5 — вдвое меньше, 1.2 — плюс двадцать процентов.</summary>
        Multiply,

        /// <summary>Поставить ровно столько: чекпоинт, добивание.</summary>
        Set
    }
}
