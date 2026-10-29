using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Раскрытое поле-класс <see cref="FInlineFieldsAttribute"/> там, где поля рисует Unity:
    /// в элементах списков и внутри чужих PropertyDrawer. Сверху имя поля жирным, под ним его поля.
    ///
    /// В обычном инспекторе такое поле рисует <see cref="GroupedFieldsLayout"/> — у него внутри
    /// работают и рамки групп; здесь рамок нет, только поля.
    /// </summary>
    [CustomPropertyDrawer(typeof(FInlineFieldsAttribute))]
    public class FInlineFieldsDrawer : PropertyDrawer
    {
        private const string NotAClassHint = "[FInlineFields] — только для полей-классов";

        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Простое поле через PropertyField не нарисовать: Unity снова позовёт этот же PropertyDrawer.
            if (!CanOpenInside(property))
            {
                EditorGUI.LabelField(line, label, new GUIContent(NotAClassHint));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(line, label, EditorStyles.boldLabel);
            PropertyTitleMenu.HandleRightClick(line, property);
            EditorGUI.indentLevel++;

            var y = line.yMax + Spacing;

            foreach (var child in ChildrenOf(property))
            {
                var height = EditorGUI.GetPropertyHeight(child, true);

                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                y += height + Spacing;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;

            if (!CanOpenInside(property))
                return height;

            foreach (var child in ChildrenOf(property))
                height += Spacing + EditorGUI.GetPropertyHeight(child, true);

            return height;
        }


        private static bool CanOpenInside(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren;
        }

        private static IEnumerable<SerializedProperty> ChildrenOf(SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            for (var enterChildren = true; iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end); enterChildren = false)
                yield return iterator.Copy();
        }
    }
}
