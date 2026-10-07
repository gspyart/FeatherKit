using UnityEngine;

namespace FeatherKit.Logging
{
    /// <summary>
    /// Цвета под смысл, а не под название. Чтобы «плохое» во всех логах проекта выглядело
    /// одинаково, а не «где-то красный, где-то оранжевый».
    ///
    /// Подобраны так, чтобы читаться и на светлой, и на тёмной теме консоли.
    /// </summary>
    public static class FLogColor
    {
        public static readonly Color Good = new Color(0.30f, 0.75f, 0.35f);
        public static readonly Color Bad = new Color(0.90f, 0.30f, 0.30f);
        public static readonly Color Warn = new Color(0.95f, 0.65f, 0.15f);
        public static readonly Color Accent = new Color(0.25f, 0.65f, 0.95f);
        public static readonly Color Value = new Color(0.65f, 0.45f, 0.90f);
        public static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f);
    }
}
