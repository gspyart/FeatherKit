using System;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Точка в мире (ScenarioPoint) в две строки: кнопка-иконка вида и поле вида, под ними смещение.
    /// - виды: этот объект, вызвавший, игрок, его глаза, объект, переменная графа (Object или Vector3), координата
    /// - у точки на объекте — переключатель «центр рендереров»
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioPoint))]
    internal class ScenarioPointDrawer : PropertyDrawer
    {
        private const float OffsetIndent = 12f;
        private const float InlineOffsetLabelWidth = 40f;
        private const float CenterToggleWidth = 22f;

        private static GUIStyle kindTextStyle;


        private static GUIStyle KindTextStyle => kindTextStyle ??= new GUIStyle(EditorStyles.textField) { imagePosition = ImagePosition.ImageLeft };


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kindProperty = property.FindPropertyRelative("kind");
            var kind = (ScenarioPointKind)kindProperty.enumValueIndex;
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var variable = kind == ScenarioPointKind.Variable ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue) : null;
            var problem = kind == ScenarioPointKind.Variable ? ProblemOf(variable) : null;
            var lineHeight = EditorGUIUtility.singleLineHeight;

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var firstLine = new Rect(position.x, position.y, position.width, lineHeight);
                var fieldRect = ScenarioVariableField.PrefixLabel(firstLine, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var kindRect = ScenarioVariableField.KindButtonRect(fieldRect);
                var contentRect = ScenarioVariableField.ContentRect(fieldRect, kindRect);
                var buttonContent = problem != null ? new GUIContent(ScenarioIcons.Error, problem)
                    : new GUIContent(IconOf(kind), $"{TitleOf(kind)} — click to change");

                if (EditorGUI.DropdownButton(kindRect, buttonContent, FocusType.Passive, EditorStyles.miniButton))
                    ShowKindMenu(property, label.text);

                DrawKindContent(contentRect, property, kind, variable, problem);

                var secondLine = new Rect(position.x, position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, lineHeight);
                DrawOffsetLine(secondLine, fieldRect.x, property, kind, variable);

                EditorGUI.indentLevel = previousIndent;
            }

            if (ScenarioVariablePicking.HandleVariableDrop(position, variableId, Fits))
                kindProperty.enumValueIndex = (int)ScenarioPointKind.Variable;
        }

        // Точке подходит переменная-позиция или объект, у которого есть Transform.
        private static bool Fits(ScenarioVariableDefinition variable)
        {
            return ScenarioVariableKinds.Fits(variable, typeof(Vector3)) || ScenarioVariableKinds.Fits(variable, typeof(Transform));
        }

        private static string ProblemOf(ScenarioVariableDefinition variable)
        {
            if (variable == null)
                return "variable not found";

            return Fits(variable) ? null : $"“{variable.Name}” is a {variable.Kind}, a point needs an Object or a Vector3";
        }


        // ── Строки ───────────────────────────────────────────────────────

        private static void DrawKindContent(Rect rect, SerializedProperty property, ScenarioPointKind kind, ScenarioVariableDefinition variable,
            string problem)
        {
            switch (kind)
            {
                case ScenarioPointKind.Object:
                    ScenarioObjectFieldGUI.Draw(rect, property.FindPropertyRelative("targetTransform"), typeof(Transform));
                    break;

                case ScenarioPointKind.WorldPoint:
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("worldPosition"), GUIContent.none);
                    break;

                case ScenarioPointKind.Variable:
                    ScenarioVariableField.DrawVariableName(rect, variable, problem);
                    break;

                default:
                    EditorGUI.LabelField(rect, new GUIContent(TitleOf(kind), IconOf(kind)), KindTextStyle);
                    break;
            }
        }

        // Центр рендереров есть только у объекта: у координаты и переменной-позиции его нет.
        // Подпись — в колонке подписей, смещение — на всю ширину поля: на узкой ноде иначе цифр не видно.
        private static void DrawOffsetLine(Rect rect, float fieldX, SerializedProperty property, ScenarioPointKind kind, ScenarioVariableDefinition variable)
        {
            var hasObject = kind != ScenarioPointKind.WorldPoint && variable is not { Kind: ScenarioVariableKind.Vector3 };
            fieldX = Mathf.Max(fieldX, rect.x + OffsetIndent + InlineOffsetLabelWidth);
            var labelRect = new Rect(rect.x + OffsetIndent, rect.y, fieldX - rect.x - OffsetIndent, rect.height);
            var vectorWidth = rect.xMax - fieldX - (hasObject ? CenterToggleWidth + ScenarioVariableField.Spacing : 0f);
            var vectorRect = new Rect(fieldX, rect.y, vectorWidth, rect.height);

            EditorGUI.LabelField(labelRect, new GUIContent("Offset", property.FindPropertyRelative("offset").tooltip), EditorStyles.miniLabel);
            EditorGUI.PropertyField(vectorRect, property.FindPropertyRelative("offset"), GUIContent.none);

            if (!hasObject)
                return;

            var center = property.FindPropertyRelative("usesRenderersCenter");
            var toggleRect = new Rect(rect.xMax - CenterToggleWidth, rect.y, CenterToggleWidth, rect.height);
            center.boolValue = GUI.Toggle(toggleRect, center.boolValue,
                new GUIContent(ScenarioIcons.TypeIcon(typeof(MeshRenderer)), "Aim at the middle of the object's renderers instead of its pivot"),
                EditorStyles.miniButton);
        }


        // ── Выбор вида ───────────────────────────────────────────────────

        private static void ShowKindMenu(SerializedProperty property, string fieldName)
        {
            var serializedObject = property.serializedObject;
            var kindPath = property.FindPropertyRelative("kind").propertyPath;
            var currentKind = (ScenarioPointKind)property.FindPropertyRelative("kind").enumValueIndex;
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var menu = new GenericMenu();

            foreach (ScenarioPointKind kind in Enum.GetValues(typeof(ScenarioPointKind)))
            {
                if (kind == ScenarioPointKind.Variable)
                    continue;

                var chosenKind = kind;
                menu.AddItem(new GUIContent(TitleOf(kind)), currentKind == kind, () =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty(kindPath).enumValueIndex = (int)chosenKind;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            var graph = ScenarioVariablePicking.GraphOf(property);

            if (graph == null)
            {
                menu.ShowAsContext();
                return;
            }

            void ChooseVariableKind(SerializedProperty chosenId) =>
                chosenId.serializedObject.FindProperty(kindPath).enumValueIndex = (int)ScenarioPointKind.Variable;

            menu.AddSeparator("");
            var fitting = graph.Variables.Where(Fits).ToList();
            ScenarioVariablePicking.AddVariableItems(menu, variableId, fitting, ChooseVariableKind);
            menu.AddSeparator("");
            ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…/Object", variableId, typeof(Transform), fieldName, null, ChooseVariableKind);
            ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…/Position (Vector3)", variableId, typeof(Vector3), fieldName, null,
                ChooseVariableKind);
            menu.ShowAsContext();
        }

        private static string TitleOf(ScenarioPointKind kind)
        {
            return kind switch
            {
                ScenarioPointKind.ThisObject => "This Object",
                ScenarioPointKind.Caller => "Caller",
                ScenarioPointKind.Player => "Player",
                ScenarioPointKind.PlayerEyes => "Player Eyes",
                ScenarioPointKind.Object => "Object",
                ScenarioPointKind.Variable => "Variable",
                _ => "World Point"
            };
        }

        private static Texture IconOf(ScenarioPointKind kind)
        {
            return kind switch
            {
                ScenarioPointKind.ThisObject => ScenarioIcons.TypeIcon(typeof(Transform)),
                ScenarioPointKind.Caller => ScenarioIcons.Caller,
                ScenarioPointKind.Player => ScenarioIcons.Player,
                ScenarioPointKind.PlayerEyes => ScenarioIcons.TypeIcon(typeof(Camera)),
                ScenarioPointKind.Object => ScenarioIcons.TypeIcon(typeof(GameObject)),
                ScenarioPointKind.Variable => ScenarioIcons.TypeIcon(typeof(ScriptableObject)),
                _ => ScenarioIcons.WorldPoint
            };
        }
    }
}
