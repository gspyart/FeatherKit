using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Числа подгонки текстуры в секции Material панели: Offset U/V, Scale U/V, Rotation — у всех выбранных мест сразу.
    /// Разное у мест — «—»; введённое число ставится всем, остальные числа у каждого места — свои. Всё с Undo.
    /// </summary>
    public static class LiveBuilderTextureFitFields
    {
        private static readonly string[] RotationNames = { "0°", "90°", "180°", "270°" };


        public static void Draw(IReadOnlyList<LiveBuilderMaterialSlot> slots)
        {
            var fits = new List<LiveBuilderTextureFit>();

            foreach (var slot in slots)
                if (slot.Fit.CanGet)
                    fits.Add(slot.Fit.Get() ?? new LiveBuilderTextureFit());

            if (fits.Count == 0)
                return;

            DrawPair(slots, fits, new GUIContent("Offset, m", "Сдвиг рисунка по сторонам грани, метры"), fit => fit.OffsetMeters,
                (fit, value) => fit.OffsetMeters = value);
            DrawPair(slots, fits, new GUIContent("Scale", "Во сколько раз крупнее плитка материала; 2 — вдвое крупнее"), fit => fit.Scale,
                (fit, value) => fit.Scale = value);
            DrawRotation(slots, fits);
        }


        // Пара чисел U и V одной строкой.
        private static void DrawPair(IReadOnlyList<LiveBuilderMaterialSlot> slots, List<LiveBuilderTextureFit> fits, GUIContent label,
            System.Func<LiveBuilderTextureFit, Vector2> read, System.Action<LiveBuilderTextureFit, Vector2> write)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(label);

                for (var axis = 0; axis < 2; axis++)
                {
                    var values = fits.ConvertAll(fit => read(fit)[axis]);
                    var labelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 13f;
                    var isChanged = LiveBuilderMixedValue.Field(new GUIContent(axis == 0 ? "U" : "V"), LiveBuilderMixedValue.Of(values), out var newValue,
                        GUILayout.MinWidth(40f));
                    EditorGUIUtility.labelWidth = labelWidth;

                    if (!isChanged)
                        continue;

                    var changedAxis = axis;
                    Apply(slots, "Edit Texture Fit", fit =>
                    {
                        var pair = read(fit);
                        pair[changedAxis] = newValue;
                        write(fit, pair);
                    });
                }
            }
        }


        private static void DrawRotation(IReadOnlyList<LiveBuilderMaterialSlot> slots, List<LiveBuilderTextureFit> fits)
        {
            var turns = LiveBuilderMixedValue.Of(fits.ConvertAll(fit => (float)fit.QuarterTurns));
            EditorGUI.showMixedValue = turns.IsMixed;
            var chosen = LiveBuilderWindowControls.Switch(new GUIContent("Rotation", "Поворот рисунка на грани, четвертями оборота"),
                turns.IsMixed ? -1 : (int)turns.Value, RotationNames);
            EditorGUI.showMixedValue = false;

            if (chosen >= 0 && (turns.IsMixed || chosen != (int)turns.Value))
                Apply(slots, "Rotate Texture", fit => fit.QuarterTurns = chosen);
        }


        // Поменять подгонку каждого места — копию его нынешней, одним шагом Undo на всех владельцев.
        private static void Apply(IReadOnlyList<LiveBuilderMaterialSlot> slots, string undoName, System.Action<LiveBuilderTextureFit> change)
        {
            foreach (var slot in slots)
            {
                if (!slot.Fit.CanSet)
                    continue;

                var fit = (slot.Fit.Get() ?? new LiveBuilderTextureFit()).Copy();
                change(fit);
                Undo.RecordObject(slot.Owner, undoName);
                slot.Fit.Set(fit);
                EditorUtility.SetDirty(slot.Owner);
            }
        }
    }
}
