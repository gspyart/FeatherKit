using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Размер проёма: имя на кнопке, ширина, высота и на какой высоте над полом этажа низ проёма, м.</summary>
    [System.Serializable]
    public struct LiveBuilderOpeningSize
    {
        [Tooltip("Имя на кнопке в окне Live Builder.")]
        [SerializeField] private string displayName;

        [Min(0.1f)]
        [SerializeField] private float width;

        [Min(0.1f)]
        [SerializeField] private float height;

        [Tooltip("Низ проёма над полом этажа, м: 0 — дверь, 1 — окно.")]
        [Min(0f)]
        [SerializeField] private float sill;


        public LiveBuilderOpeningSize(string displayName, float width, float height, float sill)
        {
            this.displayName = displayName;
            this.width = width;
            this.height = height;
            this.sill = sill;
        }


        public string DisplayName => displayName;
        public float Width => width;
        public float Height => height;
        public float Sill => sill;
    }


    /// <summary>
    /// Проёмы для инструмента «Проём»: готовые размеры из настроек проекта (<see cref="LiveBuilderProjectSettings"/>) и свой.
    /// Выбранный и свои числа помнит редактор.
    /// </summary>
    public static class LiveBuilderOpeningPresets
    {
        public const int CustomIndex = -1;


        public static System.Collections.Generic.IReadOnlyList<LiveBuilderOpeningSize> Presets => LiveBuilderProjectSettings.instance.OpeningPresets;

        // Номер готового размера; CustomIndex — свой.
        public static int SelectedIndex
        {
            get => Mathf.Clamp(LiveBuilderPreferences.GetInt("Opening.Preset", 0), CustomIndex, Presets.Count - 1);
            set => LiveBuilderPreferences.SetInt("Opening.Preset", value);
        }

        public static float CustomWidth
        {
            get => LiveBuilderPreferences.GetFloat("Opening.CustomWidth", 1.5f);
            set => LiveBuilderPreferences.SetFloat("Opening.CustomWidth", Mathf.Max(0.1f, value));
        }

        public static float CustomHeight
        {
            get => LiveBuilderPreferences.GetFloat("Opening.CustomHeight", 2.5f);
            set => LiveBuilderPreferences.SetFloat("Opening.CustomHeight", Mathf.Max(0.1f, value));
        }

        public static float CustomSill
        {
            get => LiveBuilderPreferences.GetFloat("Opening.CustomSill", 0f);
            set => LiveBuilderPreferences.SetFloat("Opening.CustomSill", Mathf.Max(0f, value));
        }

        public static LiveBuilderOpeningSize Selected => SelectedIndex == CustomIndex
            ? new LiveBuilderOpeningSize("Свой", CustomWidth, CustomHeight, CustomSill)
            : Presets[SelectedIndex];
    }
}
