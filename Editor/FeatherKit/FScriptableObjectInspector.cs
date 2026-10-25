using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инспектор ассетов-настроек по умолчанию: рисует группы FeatherKit — рамки [FGroupBox]
    /// и раскрытые поля [FInlineFields]. Без них рисует ровно как Unity.
    ///
    /// Запасной (isFallback): любой другой инспектор ассета — свой у его типа или из стороннего
    /// пакета — важнее, этот берётся, только когда других нет. Поэтому, в отличие от
    /// <see cref="FDescriptionInspector"/>, ничьи инспекторы не перебивает.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    public class FScriptableObjectInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!GroupedFieldsLayout.IsUsedBy(target.GetType()))
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();
            GroupedFieldsLayout.DrawFields(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
