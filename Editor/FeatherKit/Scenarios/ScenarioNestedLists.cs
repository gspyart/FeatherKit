using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вложенные столбики блоков — ветки «то / иначе», тело повтора, группа условий — в цветной скобке, как C-блок в Scratch.
    /// Хранит готовые списки на время жизни SerializedObject: перестраивать их каждую отрисовку дорого
    /// и сбрасывало бы выделение.
    /// </summary>
    internal static class ScenarioNestedLists
    {
        private const float BracketIndent = 10f;
        private const float HeaderSpacing = 2f;
        private const float BottomPadding = 4f;
        private const string UnityElementLabelPrefix = "Element ";

        private static readonly ConditionalWeakTable<SerializedObject, Dictionary<string, ScenarioBlockList>> ListsBySerializedObject =
            new ConditionalWeakTable<SerializedObject, Dictionary<string, ScenarioBlockList>>();


        private static float LineHeight => EditorGUIUtility.singleLineHeight;


        public static ScenarioBlockList ListOf(SerializedProperty blocks, Type blockBaseType, string emptyText)
        {
            var lists = ListsBySerializedObject.GetOrCreateValue(blocks.serializedObject);

            if (lists.TryGetValue(blocks.propertyPath, out var list))
            {
                list.Rebind(blocks.Copy());
                return list;
            }

            list = new ScenarioBlockList(blocks.Copy(), blockBaseType, emptyText);
            lists[blocks.propertyPath] = list;

            return list;
        }


        public static float HeightOf(ScenarioBlockList list)
        {
            return LineHeight + HeaderSpacing + list.Height + BottomPadding;
        }

        /// <summary>Заголовок столбика, справа от подписи — своё поле (режим условий), если передано.</summary>
        public static void Draw(Rect rect, SerializedProperty owner, GUIContent label, ScenarioBlockList list,
            SerializedProperty headerField = null)
        {
            var headerRect = new Rect(rect.x, rect.y, rect.width, LineHeight);
            var headerLabel = label;

            if (headerField == null)
            {
                EditorGUI.LabelField(headerRect, headerLabel, EditorStyles.boldLabel);
            }
            else
            {
                var fieldRect = EditorGUI.PrefixLabel(headerRect, headerLabel, EditorStyles.boldLabel);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                EditorGUI.PropertyField(fieldRect, headerField, GUIContent.none);
                EditorGUI.indentLevel = previousIndent;
            }

            var bodyRect = new Rect(rect.x, headerRect.yMax + HeaderSpacing, rect.width, list.Height);

            ScenarioBlockStyle.DrawBracket(new Rect(bodyRect.x, bodyRect.y - 1f, BracketIndent, bodyRect.height + 2f),
                ScenarioBlockStyle.OwnerColorOf(owner));

            list.Draw(new Rect(bodyRect.x + BracketIndent, bodyRect.y, bodyRect.width - BracketIndent, bodyRect.height));
        }

        /// <summary>
        /// Подпись столбика: элемент списка веток — «Ветка 1» вместо «Element 0»,
        /// поле — из ScenarioLabelAttribute, иначе как передал Unity.
        /// </summary>
        public static GUIContent ReadableLabelOf(GUIContent label, FieldInfo field)
        {
            if (label.text.StartsWith(UnityElementLabelPrefix) &&
                int.TryParse(label.text.Substring(UnityElementLabelPrefix.Length), out var index))
                return new GUIContent($"Ветка {index + 1}", label.tooltip);

            var text = field?.GetCustomAttribute<ScenarioLabelAttribute>()?.Text;

            return text != null ? new GUIContent(text, label.tooltip) : label;
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioActionList))]
    internal class ScenarioActionListDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ScenarioNestedLists.HeightOf(ListOf(property));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ScenarioNestedLists.Draw(position, property, ScenarioNestedLists.ReadableLabelOf(label, fieldInfo), ListOf(property));
        }

        private static ScenarioBlockList ListOf(SerializedProperty property)
        {
            return ScenarioNestedLists.ListOf(property.FindPropertyRelative("actions"), typeof(ScenarioAction), "Нет шагов");
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioConditionList))]
    internal class ScenarioConditionListDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ScenarioNestedLists.HeightOf(ListOf(property));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ScenarioNestedLists.Draw(position, property, ScenarioNestedLists.ReadableLabelOf(label, fieldInfo), ListOf(property),
                property.FindPropertyRelative("mode"));
        }

        private static ScenarioBlockList ListOf(SerializedProperty property)
        {
            return ScenarioNestedLists.ListOf(property.FindPropertyRelative("conditions"), typeof(ScenarioCondition),
                "Без условий — выполнено всегда");
        }
    }


    /// <summary>Ветка «случайно одно из»: строка «Ветка N — вес», под ней её шаги в скобке.</summary>
    [CustomPropertyDrawer(typeof(WeightedActionBranch))]
    internal class WeightedActionBranchDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + Spacing
                   + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("actions"), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var weightRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var branchLabel = ScenarioNestedLists.ReadableLabelOf(label, null);
            var weight = property.FindPropertyRelative("weight");

            EditorGUI.PropertyField(weightRect, weight, new GUIContent($"{branchLabel.text} — вес", weight.tooltip));

            var actions = property.FindPropertyRelative("actions");
            var actionsRect = new Rect(position.x, weightRect.yMax + Spacing, position.width,
                EditorGUI.GetPropertyHeight(actions, true));

            EditorGUI.PropertyField(actionsRect, actions, new GUIContent("Шаги"), true);
        }
    }
}
