using FeatherKit.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует поле с <see cref="NavMeshAgentTypeAttribute"/> списком имён типов агента.
    ///
    /// Имена и номера берутся у самого навмеша, а не пишутся руками: типы заводят
    /// в настройках навигации проекта, и список здесь обязан совпадать с тем, что там.
    /// </summary>
    [CustomPropertyDrawer(typeof(NavMeshAgentTypeAttribute))]
    public class NavMeshAgentTypeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var count = NavMesh.GetSettingsCount();

            // Поле не числовое или типов нет вовсе — показываем как есть: молча ничего
            // не рисовать хуже, чем показать сырое значение.
            if (property.propertyType != SerializedPropertyType.Integer || count == 0)
            {
                EditorGUI.PropertyField(position, property, label);

                return;
            }

            var names = new string[count];
            var ids = new int[count];
            var selected = 0;

            for (var i = 0; i < count; i++)
            {
                ids[i] = NavMesh.GetSettingsByIndex(i).agentTypeID;
                names[i] = NavMesh.GetSettingsNameFromID(ids[i]);

                if (ids[i] == property.intValue)
                    selected = i;
            }

            EditorGUI.BeginProperty(position, label, property);

            var picked = EditorGUI.Popup(position, label.text, selected, names);

            property.intValue = ids[Mathf.Clamp(picked, 0, count - 1)];

            EditorGUI.EndProperty();
        }
    }
}
