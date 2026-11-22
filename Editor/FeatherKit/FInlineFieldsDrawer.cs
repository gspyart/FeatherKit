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
        private const float BoxTopGap = 4f;

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

            if (IsBoxed)
            {
                DrawBoxed(position, property, label);
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

            if (IsBoxed)
                return BoxedHeightOf(property);

            foreach (var child in ChildrenOf(property))
                height += Spacing + EditorGUI.GetPropertyHeight(child, true);

            return height;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        private bool IsBoxed => attribute is FInlineFieldsAttribute { Boxed: true };

        // Как рамка секции инспектора: полоса заголовка, под ней поля с отступом.
        private static void DrawBoxed(Rect position, SerializedProperty property, GUIContent label)
        {
            var header = new Rect(position.x, position.y, position.width, InspectorSection.HeaderHeight);
            var padding = InspectorSection.ContentPadding;

            InspectorSection.DrawFixedFrame(position, header, label.text);
            PropertyTitleMenu.HandleRightClick(header, property);

            EditorGUI.BeginProperty(position, label, property);

            var x = position.x + padding;
            var width = position.width - padding * 2f;
            var y = header.yMax + BoxTopGap;

            foreach (var child in ChildrenOf(property))
            {
                var height = EditorGUI.GetPropertyHeight(child, true);

                EditorGUI.PropertyField(new Rect(x, y, width, height), child, true);
                y += height + Spacing;
            }

            EditorGUI.EndProperty();
        }

        private static float BoxedHeightOf(SerializedProperty property)
        {
            var height = InspectorSection.HeaderHeight + BoxTopGap + InspectorSection.ContentPadding;

            foreach (var child in ChildrenOf(property))
                height += EditorGUI.GetPropertyHeight(child, true) + Spacing;

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
