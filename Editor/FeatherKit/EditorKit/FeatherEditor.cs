using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Entities;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// База для кастомных инспекторов. Не зовёт DrawDefaultInspector()/base.OnInspectorGUI():
    /// строку скрипта заменяет своя шапка, остальные поля рисует <see cref="GroupedFieldsLayout"/> —
    /// как у Unity, плюс разметка FeatherKit: рамки, секции, блоки.
    /// </summary>
    public class FeatherEditor : Editor
    {
        private const float BoxPadding = 4f;
        private const float HeaderButtonWidth = 50f;
        private const int BoxDefaultRadius = 6;
        private const int BoxDefaultBorder = 3;
        private const int DefaultSpacing = 3;

        protected virtual Color BoxBackgroundColor => new Color(0f, 0f, 0f, 0.15f);
        protected virtual Color BoxBorderColor => new Color(0f, 0f, 0f, 0.4f);

        // Переопредели в наследнике, чтобы сменить вид кнопок во всём инспекторе разом.
        protected virtual ButtonStyle PrimaryButtonStyle => GUIButtonStyles.Primary;
        protected virtual ButtonStyle SecondaryButtonStyle => GUIButtonStyles.Secondary;
        protected virtual ButtonStyle DestructiveButtonStyle => GUIButtonStyles.Destructive;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var script = serializedObject.FindProperty("m_Script");

            if (script != null)
            {
                DrawHeader(script);
                DrawScriptDescription();
                EditorGUILayout.Space(6);
            }

            // Строку скрипта заменяет своя шапка; остальные поля — как у Unity, плюс группы FeatherKit.
            GroupedFieldsLayout.DrawFields(serializedObject, drawScriptField: false);

            serializedObject.ApplyModifiedProperties();
        }

        // ScriptDescription в FEntityBase — обычное protected C#-свойство, не [SerializeField]-поле,
        // так что SerializedProperty его никогда не найдёт. Достаём значение рефлексией прямо с target.
        private void DrawScriptDescription()
        {
            var property = target.GetType().GetProperty("ScriptDescription", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return;

            var description = property.GetValue(target) as string;
            if (string.IsNullOrEmpty(description))
                return;

            EditorGUILayout.HelpBox(description, MessageType.Info);
        }

        // Шапка со скриптом: фон через BoxBegin/BoxEnd, лейбл и кнопки — обычный layout.
        private void DrawHeader(SerializedProperty scriptProperty)
        {
            var script = scriptProperty.objectReferenceValue as MonoScript;

            FeatherEditorGUIBox.BoxBegin(BoxBackgroundColor, BoxBorderColor, BoxDefaultBorder, 0, fullWidth: true);
            GUILayout.Space(BoxPadding);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BoxPadding);

            EditorGUILayout.LabelField(script != null ? script.name : "Missing Script", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(script == null);

            var buttonHeight = GUILayout.Height(EditorGUIUtility.singleLineHeight);

            if (GUIButton.Draw("Select", SecondaryButtonStyle, GUILayout.Width(HeaderButtonWidth), buttonHeight))
                EditorGUIUtility.PingObject(script);

            if (GUIButton.Draw("Edit", PrimaryButtonStyle, GUILayout.Width(HeaderButtonWidth), buttonHeight))
                AssetDatabase.OpenAsset(script);

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(BoxPadding);
            EditorGUILayout.EndHorizontal();

            // Цепочка наследования — своей строкой через явный Rect, чтобы гарантированно
            // обрезалась по границе бокса. Путь к файлу ушёл в tooltip: смотреть, от кого
            // унаследован компонент, приходится куда чаще, чем вспоминать, в какой он папке.
            if (target != null)
            {
                var chainRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                chainRect.x += BoxPadding;
                chainRect.width -= BoxPadding * 2f;

                var tooltip = script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
                var content = new GUIContent(BuildInheritanceChain(target.GetType()), tooltip);

                EditorGUI.LabelField(chainRect, content, EditorStyles.miniLabel);
            }

            GUILayout.Space(BoxPadding);
            FeatherEditorGUIBox.BoxEnd();
            EditorGUILayout.Space(DefaultSpacing);
        }

        // Строит строку вида "MonoBehaviour → FEntityBase → Enemy".
        // Выше MonoBehaviour/ScriptableObject не поднимаемся: Behaviour/Component/Object одинаковы
        // у всего на свете и только засоряют строку, ничего не сообщая про конкретный компонент.
        private static string BuildInheritanceChain(Type type)
        {
            var chain = new List<string>();

            for (var current = type; current != null; current = current.BaseType)
            {
                chain.Add(current.Name);

                if (current == typeof(MonoBehaviour) || current == typeof(ScriptableObject))
                    break;
            }

            chain.Reverse(); // от базового к конкретному — читается как "во что вырос"
            return string.Join(" → ", chain);
        }
    }


    [CustomEditor(typeof(FEntityBase), true)]
    public class FEntityBaseDrawer : FeatherEditor
    { }
}
