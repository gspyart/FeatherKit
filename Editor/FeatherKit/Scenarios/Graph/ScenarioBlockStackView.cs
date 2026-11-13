using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Стопка блоков на ноде и в инспекторе ноды: события и условия When, шаги Do, условия Guard While.
    /// Рисует тот же ScenarioBlockList, что и инспектор сценария: «+» с поиском, удаление, перетаскивание,
    /// сворачивание, копировать и вставить блок. Правит через SerializedProperty — undo сам.
    /// </summary>
    internal class ScenarioBlockStackView : VisualElement
    {
        private const float LabelShare = 0.38f;
        private const float MinLabelWidth = 80f;

        private readonly ScenarioBlockList blockList;
        private readonly SerializedObject serializedObject;
        private readonly IMGUIContainer blocksContainer;


        public ScenarioBlockStackView(SerializedProperty blocks, Type blockBaseType, string title, string emptyText,
            SerializedProperty modeProperty = null)
        {
            serializedObject = blocks.serializedObject;
            blockList = new ScenarioBlockList(blocks.Copy(), blockBaseType, emptyText);

            AddToClassList("scenario-stack");

            Add(CreateHeader(title, modeProperty));

            blocksContainer = new IMGUIContainer(DrawBlocks);
            Add(blocksContainer);
        }


        private static VisualElement CreateHeader(string title, SerializedProperty modeProperty)
        {
            var header = new VisualElement();
            header.AddToClassList("scenario-stack__header");
            header.Add(new Label(title));

            if (modeProperty != null)
            {
                var modeField = new PropertyField(modeProperty, "");
                modeField.BindProperty(modeProperty);
                header.Add(modeField);
            }

            return header;
        }


        // UpdateIfRequiredOrScript, а не Update: стопок на холсте сотни, полное чтение графа каждой — квадратичный рост.
        private void DrawBlocks()
        {
            if (serializedObject.targetObject == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(MinLabelWidth, blocksContainer.layout.width * LabelShare);

            blockList.Draw();

            EditorGUIUtility.labelWidth = previousLabelWidth;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
