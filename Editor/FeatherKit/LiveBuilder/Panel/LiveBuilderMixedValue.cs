using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Число у нескольких выделенных сразу, когда оно не поле данных (размер коробки по осям, подгонка текстуры): общее или разное — «—».
    /// - собрать из чисел всех выделенных
    /// - нарисовать полем: разное — «—», введёшь — станет у всех
    /// </summary>
    public readonly struct LiveBuilderMixedValue
    {
        public LiveBuilderMixedValue(float value, bool isMixed)
        {
            Value = value;
            IsMixed = isMixed;
        }


        // Разное — значение первого.
        public float Value { get; }

        public bool IsMixed { get; }


        public static LiveBuilderMixedValue Of(IEnumerable<float> values)
        {
            var hasFirst = false;
            var first = 0f;

            foreach (var value in values)
            {
                if (!hasFirst)
                {
                    first = value;
                    hasFirst = true;
                    continue;
                }

                if (!Mathf.Approximately(value, first))
                    return new LiveBuilderMixedValue(first, true);
            }

            return new LiveBuilderMixedValue(first, false);
        }


        // Поле числа; разное — «—». true — ввели новое число, оно в value.
        public static bool Field(Rect rect, GUIContent label, LiveBuilderMixedValue mixed, out float value)
        {
            EditorGUI.showMixedValue = mixed.IsMixed;
            EditorGUI.BeginChangeCheck();
            value = EditorGUI.FloatField(rect, label, mixed.Value);
            EditorGUI.showMixedValue = false;
            return EditorGUI.EndChangeCheck();
        }


        // То же в разметке.
        public static bool Field(GUIContent label, LiveBuilderMixedValue mixed, out float value, params GUILayoutOption[] options)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            return Field(rect, label, mixed, out value);
        }
    }
}
