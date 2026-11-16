using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.LevelNotes;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Как выглядит тип заметки в редакторе — одно место на флажок, поле у курсора и окно-список:
    /// цвет типа, бледный цвет сделанной заметки, подпись типа из <see cref="InspectorNameAttribute"/>.
    /// </summary>
    public static class LevelNoteKindStyle
    {
        // Сделанная заметка — тот же цвет, но бледнее и прозрачнее: видно, что была, и не мешает.
        private const float DoneAlpha = 0.35f;
        private const float DoneSaturation = 0.35f;

        private static readonly Dictionary<LevelNoteKind, string> LabelsByKind = new();


        public static IReadOnlyList<LevelNoteKind> AllKinds { get; } = (LevelNoteKind[])Enum.GetValues(typeof(LevelNoteKind));


        public static Color ColorOf(LevelNoteKind kind)
        {
            return kind switch
            {
                LevelNoteKind.Idea => new Color(1f, 0.82f, 0.2f),
                LevelNoteKind.Task => new Color(1f, 0.45f, 0.2f),
                LevelNoteKind.Question => new Color(0.35f, 0.7f, 1f),
                LevelNoteKind.ForAgent => new Color(0.4f, 0.9f, 0.45f),
                _ => Color.white
            };
        }


        public static Color ColorOf(LevelNote note)
        {
            var color = ColorOf(note.Kind);

            if (!note.IsDone)
                return color;

            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            var paleColor = Color.HSVToRGB(hue, saturation * DoneSaturation, value);
            paleColor.a = DoneAlpha;
            return paleColor;
        }


        public static string LabelOf(LevelNoteKind kind)
        {
            if (LabelsByKind.TryGetValue(kind, out var label))
                return label;

            var field = typeof(LevelNoteKind).GetField(kind.ToString(), BindingFlags.Public | BindingFlags.Static);
            var inspectorName = field?.GetCustomAttribute<InspectorNameAttribute>();
            label = inspectorName != null ? inspectorName.displayName : kind.ToString();

            LabelsByKind[kind] = label;
            return label;
        }
    }
}
