using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Настройки взгляда (ScenarioGazeCheck у Looks At и Looked At) — прямо в блоке, без лишней раскрывашки.
    /// Показывает только поля выбранных режимов: у конуса — угол, у кадра камеры — запас и камеру, у Clear View — слои.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioGazeCheck))]
    internal class ScenarioGazeCheckDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 0f;

            foreach (var field in VisibleFields(property))
                height += EditorGUI.GetPropertyHeight(field, LabelOf(field), true) + EditorGUIUtility.standardVerticalSpacing;

            return Mathf.Max(0f, height - EditorGUIUtility.standardVerticalSpacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var y = position.y;

            foreach (var field in VisibleFields(property))
            {
                var fieldLabel = LabelOf(field);
                var height = EditorGUI.GetPropertyHeight(field, fieldLabel, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), field, fieldLabel, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }


        private static System.Collections.Generic.IEnumerable<SerializedProperty> VisibleFields(SerializedProperty property)
        {
            var targetMode = (ScenarioLookTargetMode)property.FindPropertyRelative("targetMode").enumValueIndex;
            var angleMode = (ScenarioLookAngleMode)property.FindPropertyRelative("angleMode").enumValueIndex;
            var requiresClearView = property.FindPropertyRelative("requiresClearView").boolValue;

            var field = property.Copy();
            var end = property.GetEndProperty();

            for (var entered = field.NextVisible(true); entered && !SerializedProperty.EqualContents(field, end); entered = field.NextVisible(false))
            {
                if (IsShown(field.name, targetMode, angleMode, requiresClearView))
                    yield return field.Copy();
            }
        }

        private static bool IsShown(string fieldName, ScenarioLookTargetMode targetMode, ScenarioLookAngleMode angleMode, bool requiresClearView)
        {
            return fieldName switch
            {
                "target" => targetMode != ScenarioLookTargetMode.WorldDirection,
                "worldDirection" => targetMode == ScenarioLookTargetMode.WorldDirection,
                "objectAxis" => targetMode == ScenarioLookTargetMode.ObjectAxis,
                "maxAngle" => angleMode == ScenarioLookAngleMode.Cone,
                "maxYaw" or "maxPitch" => angleMode == ScenarioLookAngleMode.HorizontalAndVertical,
                "frameMarginPercent" or "frameCamera" => angleMode == ScenarioLookAngleMode.InCameraFrame,
                "clearViewLayers" => requiresClearView,
                _ => true
            };
        }

        private static GUIContent LabelOf(SerializedProperty field)
        {
            return ScenarioBlockStyle.LabelOf(typeof(ScenarioGazeCheck), field);
        }
    }
}
