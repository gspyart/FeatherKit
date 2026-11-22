using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Режим «Коробки»: тело (Solid), пустая комната (Room), пандус (Ramp) или колонна (Column).</summary>
    public enum LiveBuilderBoxMode
    {
        Solid,
        Room,
        Ramp,
        Column
    }


    /// <summary>Что даёт жест протягивания «Коробки»: обычную коробку, пандус (коробка с полным скосом) или колонну (отдельный объект-цилиндр).</summary>
    public enum LiveBuilderBoxShape
    {
        Box,
        Ramp,
        Column
    }


    /// <summary>Режим «Коробки» и стороны новой колонны — у каждого человека свои, в настройках редактора.</summary>
    public static class LiveBuilderBoxModeSettings
    {
        public const int DefaultColumnSides = 16;

        private const string ModeKey = "Box.Mode";

        // Раньше помнилась только форма (Box, Ramp, Column) — читается, пока режим не запомнен.
        private const string OldShapeKey = "Box.Shape";


        public static LiveBuilderBoxMode Mode
        {
            get => LiveBuilderPreferences.GetEnum(ModeKey, ModeOfOldShape());
            set => LiveBuilderPreferences.SetInt(ModeKey, (int)value);
        }

        public static int ColumnSides
        {
            get => Mathf.Clamp(LiveBuilderPreferences.GetInt("Column.Sides", DefaultColumnSides), 4, 32);
            set => LiveBuilderPreferences.SetInt("Column.Sides", Mathf.Clamp(value, 4, 32));
        }


        private static LiveBuilderBoxMode ModeOfOldShape()
        {
            return LiveBuilderPreferences.GetEnum(OldShapeKey, LiveBuilderBoxShape.Box) switch
            {
                LiveBuilderBoxShape.Ramp => LiveBuilderBoxMode.Ramp,
                LiveBuilderBoxShape.Column => LiveBuilderBoxMode.Column,
                _ => LiveBuilderBoxMode.Solid
            };
        }
    }


    /// <summary>Уклон и свес двускатной крыши для кнопки «Gable Roof» — помнятся у каждого человека свои.</summary>
    public static class LiveBuilderGableRoofSettings
    {
        public const float DefaultPitchDegrees = 30f;
        public const float DefaultOverhang = 0.3f;


        // Уклон ската от горизонта, градусы.
        public static float PitchDegrees
        {
            get => Mathf.Clamp(LiveBuilderPreferences.GetFloat("GableRoof.Pitch", DefaultPitchDegrees), 5f, 60f);
            set => LiveBuilderPreferences.SetFloat("GableRoof.Pitch", Mathf.Clamp(value, 5f, 60f));
        }

        // Насколько крыша выходит за наружные стены, м.
        public static float Overhang
        {
            get => Mathf.Max(0f, LiveBuilderPreferences.GetFloat("GableRoof.Overhang", DefaultOverhang));
            set => LiveBuilderPreferences.SetFloat("GableRoof.Overhang", Mathf.Max(0f, value));
        }
    }
}
