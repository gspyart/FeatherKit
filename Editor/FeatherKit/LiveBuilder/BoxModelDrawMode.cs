using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что делает нарисованная коробка: «Авто» — решает направление (наружу добавляет, внутрь вырезает), или всегда одно.</summary>
    public enum BoxModelDrawMode
    {
        Auto,
        Add,
        Cut
    }


    /// <summary>Выбранный режим рисования коробок. Свой у каждого человека, хранится в настройках редактора.</summary>
    public static class BoxModelDrawModeSetting
    {
        private const string PreferenceKey = "FeatherKit.LiveBuilder.DrawMode";


        public static BoxModelDrawMode Current
        {
            get => (BoxModelDrawMode)EditorPrefs.GetInt(PreferenceKey, (int)BoxModelDrawMode.Auto);
            set => EditorPrefs.SetInt(PreferenceKey, (int)value);
        }


        // Режим коробки по глубине жеста: плюс — наружу, минус — внутрь.
        public static BoxModelPartMode PartModeFor(float depth)
        {
            return Current switch
            {
                BoxModelDrawMode.Add => BoxModelPartMode.Add,
                BoxModelDrawMode.Cut => BoxModelPartMode.Cut,
                _ => depth >= 0f ? BoxModelPartMode.Add : BoxModelPartMode.Cut
            };
        }


        public static string DisplayName(BoxModelDrawMode mode)
        {
            return mode switch
            {
                BoxModelDrawMode.Add => "Добавить",
                BoxModelDrawMode.Cut => "Вырезать",
                _ => "Авто"
            };
        }
    }
}
