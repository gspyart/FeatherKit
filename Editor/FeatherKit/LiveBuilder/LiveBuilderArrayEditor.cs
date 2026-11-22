using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор повтора (<see cref="LiveBuilderArray"/>): его поля и сверху — что с ним не так (<see cref="LiveBuilderArrayCopyPoses.WarningsOf"/>),
    /// сколько копий и какой шаг X и Y на самом деле — у Fill и от этажей. У лестницы по этажам — «и на последнем этаже» (пропуск «last» по Y).
    /// </summary>
    [CustomEditor(typeof(LiveBuilderArray))]
    [CanEditMultipleObjects]
    public class LiveBuilderArrayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var array = (LiveBuilderArray)target;

            foreach (var warning in LiveBuilderArrayCopyPoses.WarningsOf(array))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            var context = LiveBuilderArrayCopyPoses.FilledContextOf(array);
            var counts = LiveBuilderArrayLayout.CountsOf(array, context);
            var steps = LiveBuilderArrayLayout.StepsOf(array, context);
            EditorGUILayout.LabelField("Copies " + counts.x + " × " + counts.y + " × " + counts.z + ", step X " + LiveBuilderSceneText.FormatMeters(steps.x) +
                                       " m, step Y " + LiveBuilderSceneText.FormatMeters(steps.y) + " m", EditorStyles.miniLabel);

            if (targets.Length == 1 && array.CountYToBuildingTop && array.GetComponent<BoxModelStairs>() != null)
                DrawTopStoreyToggle(array);

            DrawDefaultInspector();
        }


        // Лестница по этажам обычно без верхнего: с него идти некуда. Выход на крышу — включить.
        internal static void DrawTopStoreyToggle(LiveBuilderArray array)
        {
            var tokens = new List<string>(array.SkipY.Split(','));
            var lastIndex = tokens.FindIndex(token => token.Trim().ToLowerInvariant() == SplineBuildingInteriorPlacement.SkipTopStorey);
            var isOnTopStorey = EditorGUILayout.Toggle(new GUIContent("Also On Top Storey",
                "Лестница и на верхнем этаже — например, выход на крышу. Обычно с верхнего идти некуда: Skip Y — «last»."), lastIndex < 0);

            if (isOnTopStorey == lastIndex < 0)
                return;

            if (isOnTopStorey)
                tokens.RemoveAt(lastIndex);
            else
                tokens.Add(SplineBuildingInteriorPlacement.SkipTopStorey);

            tokens.RemoveAll(token => token.Trim().Length == 0);
            Undo.RecordObject(array, "Change Stairs Top Storey");
            array.SkipY = string.Join(", ", tokens.ConvertAll(token => token.Trim()));
        }
    }
}
