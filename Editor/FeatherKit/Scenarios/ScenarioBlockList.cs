using System;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Один столбик блоков: «Когда», «Если», «Что сделать» или вложенный внутри другого блока.
    /// - блок — цветная плашка категории с подписью настроек, раскрывается в свои поля
    /// - «+» — выбор блока с поиском, перетаскивание меняет порядок
    /// - ПКМ по заголовку: копировать, вставить, дублировать, удалить; весь список — копировать и свернуть
    /// - потерянный блок (класс переименован или удалён) — красная строка вместо полей
    /// </summary>
    internal class ScenarioBlockList
    {
        private const string InvertedFieldName = "isInverted";
        private const float VerticalPadding = 3f;
        private const float FieldSpacing = 2f;
        private const float FoldoutIndent = 12f;
        private const float InvertToggleWidth = 42f;

        private static readonly Color RunningBlockColor = new Color(0.25f, 0.85f, 0.35f, 0.45f);
        private static readonly Color LostBlockColor = new Color(0.9f, 0.3f, 0.3f);

        private static GUIStyle headerStyle;

        private readonly ReorderableList list;
        private readonly Type blockBaseType;
        private readonly Func<int> runningBlockIndex;


        public ScenarioBlockList(SerializedProperty blocks, Type blockBaseType, string emptyText,
            Func<int> runningBlockIndex = null)
        {
            this.blockBaseType = blockBaseType;
            this.runningBlockIndex = runningBlockIndex;

            list = new ReorderableList(blocks.serializedObject, blocks,
                draggable: true, displayHeader: false, displayAddButton: true, displayRemoveButton: true)
            {
                drawElementCallback = DrawBlock,
                elementHeightCallback = BlockHeight,
                drawElementBackgroundCallback = DrawBlockBackground,
                onAddDropdownCallback = ShowAddMenu,
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect, emptyText, EditorStyles.miniLabel)
            };
        }


        private static float LineHeight => EditorGUIUtility.singleLineHeight;

        private static GUIStyle HeaderStyle => headerStyle ??= new GUIStyle(EditorStyles.foldout) { richText = true };

        public bool CanPaste => ScenarioBlockClipboard.CanPasteInto(blockBaseType);

        public float Height => list.GetHeight();

        private SerializedProperty Blocks => list.serializedProperty;


        public void Draw()
        {
            list.DoLayoutList();
        }

        public void Draw(Rect rect)
        {
            list.DoList(rect);
        }

        /// <summary>Вложенный столбик: свойство приходит заново каждую отрисовку, путь к нему тот же.</summary>
        public void Rebind(SerializedProperty blocks)
        {
            list.serializedProperty = blocks;
        }


        // ── Весь список ──────────────────────────────────────────────────

        public void CopyAll()
        {
            var blocks = new List<object>();

            for (var index = 0; index < Blocks.arraySize; index++)
                blocks.Add(Blocks.GetArrayElementAtIndex(index).managedReferenceValue);

            ScenarioBlockClipboard.Copy(blocks);
        }

        public void PasteAtEnd()
        {
            PasteAt(Blocks.arraySize);
        }

        public void SetAllExpanded(bool isExpanded)
        {
            for (var index = 0; index < Blocks.arraySize; index++)
                Blocks.GetArrayElementAtIndex(index).isExpanded = isExpanded;
        }


        // ── Блок ─────────────────────────────────────────────────────────

        private void DrawBlock(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = Blocks.GetArrayElementAtIndex(index);
            var block = element.managedReferenceValue;
            var headerRect = new Rect(rect.x + FoldoutIndent, rect.y + VerticalPadding, rect.width - FoldoutIndent, LineHeight);

            if (block == null)
            {
                DrawLostBlock(headerRect, element);
                return;
            }

            DrawHeader(headerRect, element, block, index);

            if (!element.isExpanded)
                return;

            var y = headerRect.yMax + FieldSpacing;

            foreach (var field in FieldsOf(element))
            {
                var label = ScenarioBlockStyle.LabelOf(block.GetType(), field);
                var height = EditorGUI.GetPropertyHeight(field, label, true);

                EditorGUI.PropertyField(new Rect(headerRect.x, y, headerRect.width, height), field, label, true);
                y += height + FieldSpacing;
            }
        }

        private void DrawHeader(Rect rect, SerializedProperty element, object block, int index)
        {
            var inverted = element.FindPropertyRelative(InvertedFieldName);
            var foldoutRect = rect;

            if (inverted != null)
            {
                foldoutRect.width -= InvertToggleWidth;

                var toggleRect = new Rect(rect.xMax - InvertToggleWidth, rect.y, InvertToggleWidth, rect.height);
                inverted.boolValue = EditorGUI.ToggleLeft(toggleRect, "НЕ", inverted.boolValue);
            }

            var title = HeaderTextOf(block, inverted != null && inverted.boolValue);
            element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, title, true, HeaderStyle);

            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                ShowBlockMenu(index);
                Event.current.Use();
            }
        }

        private static string HeaderTextOf(object block, bool isInverted)
        {
            var title = ScenarioBlockCatalog.TitleOf(block.GetType());
            var summary = SummaryOf(block);
            var prefix = isInverted ? "НЕ " : "";

            return string.IsNullOrEmpty(summary)
                ? $"<b>{prefix}{title}</b>"
                : $"<b>{prefix}{title}</b>   <color=#9a9a9a>{summary}</color>";
        }

        private static string SummaryOf(object block)
        {
            return block switch
            {
                ScenarioEvent scenarioEvent => scenarioEvent.Summary,
                ScenarioCondition condition => condition.Summary,
                ScenarioAction action => action.Summary,
                _ => null
            };
        }

        private static void DrawLostBlock(Rect rect, SerializedProperty element)
        {
            var typeName = element.managedReferenceFullTypename;
            var text = string.IsNullOrEmpty(typeName)
                ? "Пустой блок — удалите его кнопкой «−»"
                : $"Блок потерян: класс {typeName} переименован или удалён. Удалите его кнопкой «−»";

            var previousColor = GUI.contentColor;
            GUI.contentColor = LostBlockColor;
            EditorGUI.LabelField(rect, text, EditorStyles.miniBoldLabel);
            GUI.contentColor = previousColor;
        }

        private float BlockHeight(int index)
        {
            var element = Blocks.GetArrayElementAtIndex(index);
            var height = LineHeight + VerticalPadding * 2f;

            if (!element.isExpanded || element.managedReferenceValue == null)
                return height;

            var blockType = element.managedReferenceValue.GetType();

            foreach (var field in FieldsOf(element))
                height += EditorGUI.GetPropertyHeight(field, ScenarioBlockStyle.LabelOf(blockType, field), true) + FieldSpacing;

            return height;
        }

        // Галка «НЕ» — в заголовке, среди полей её не повторяем.
        private static IEnumerable<SerializedProperty> FieldsOf(SerializedProperty element)
        {
            var field = element.Copy();
            var end = element.GetEndProperty();

            if (!field.NextVisible(true))
                yield break;

            while (field.depth > element.depth && !SerializedProperty.EqualContents(field, end))
            {
                if (field.name != InvertedFieldName)
                    yield return field.Copy();

                if (!field.NextVisible(false))
                    yield break;
            }
        }

        private void DrawBlockBackground(Rect rect, int index, bool isActive, bool isFocused)
        {
            var block = index >= 0 && index < Blocks.arraySize ? Blocks.GetArrayElementAtIndex(index).managedReferenceValue : null;

            if (block == null)
            {
                ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                return;
            }

            ScenarioBlockStyle.DrawBlockBackground(rect, ScenarioBlockStyle.ColorOf(block.GetType()), isActive);

            if (runningBlockIndex != null && index == runningBlockIndex())
                EditorGUI.DrawRect(rect, RunningBlockColor);
        }


        // ── Добавить, вставить, дублировать, удалить ─────────────────────

        private void ShowAddMenu(Rect buttonRect, ReorderableList _)
        {
            new ScenarioBlockPicker(blockBaseType, AddBlock).Show(buttonRect);
        }

        private void AddBlock(Type blockType)
        {
            Blocks.serializedObject.Update();

            Blocks.arraySize++;
            var element = Blocks.GetArrayElementAtIndex(Blocks.arraySize - 1);
            element.managedReferenceValue = Activator.CreateInstance(blockType);
            element.isExpanded = true;

            Blocks.serializedObject.ApplyModifiedProperties();
        }


        private void ShowBlockMenu(int index)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Копировать"), false,
                () => ScenarioBlockClipboard.Copy(new[] { Blocks.GetArrayElementAtIndex(index).managedReferenceValue }));

            if (CanPaste)
                menu.AddItem(new GUIContent($"Вставить после ({ScenarioBlockClipboard.Count})"), false, () => PasteAt(index + 1));
            else
                menu.AddDisabledItem(new GUIContent("Вставить после"));

            menu.AddItem(new GUIContent("Дублировать"), false, () => DuplicateBlock(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Удалить"), false, () => RemoveBlock(index));

            menu.ShowAsContext();
        }

        private void PasteAt(int index)
        {
            if (!CanPaste)
                return;

            Blocks.serializedObject.Update();

            foreach (var block in ScenarioBlockClipboard.CreateCopies())
                InsertBlock(index++, block);

            Blocks.serializedObject.ApplyModifiedProperties();
        }

        private void DuplicateBlock(int index)
        {
            Blocks.serializedObject.Update();

            var source = Blocks.GetArrayElementAtIndex(index).managedReferenceValue;

            if (source == null)
                return;

            InsertBlock(index + 1, ScenarioBlockClipboard.Clone(source));

            Blocks.serializedObject.ApplyModifiedProperties();
        }

        // Новый элемент массива сначала копирует соседа — сразу заменяем его своим блоком.
        private void InsertBlock(int index, object block)
        {
            var position = Mathf.Min(index, Blocks.arraySize);

            Blocks.InsertArrayElementAtIndex(position);
            var element = Blocks.GetArrayElementAtIndex(position);
            element.managedReferenceValue = block;
            element.isExpanded = true;
        }

        private void RemoveBlock(int index)
        {
            Blocks.serializedObject.Update();
            Blocks.DeleteArrayElementAtIndex(index);
            Blocks.serializedObject.ApplyModifiedProperties();
        }
    }
}
