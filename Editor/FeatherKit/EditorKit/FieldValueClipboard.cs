using System;
using UnityEditor;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Копирование значения поля-класса целиком через буфер обмена: все его настройки разом.
    /// Запасной вариант для <see cref="PropertyTitleMenu"/>, когда родное меню Unity недоступно.
    ///
    /// Формат свой: первая строка — метка и тип, дальше значение в JSON. Вставляется только в поле
    /// того же типа. С копированием самой Unity не совместим — у неё свой формат.
    /// </summary>
    public static class FieldValueClipboard
    {
        private const string TypeMark = "FeatherKit.FieldValue:";
        private const char TypeLineEnd = '\n';


        /// <summary>Положить значение поля в буфер обмена.</summary>
        public static void Copy(SerializedProperty property)
        {
            var value = property.boxedValue;

            if (value == null)
                return;

            EditorGUIUtility.systemCopyBuffer = TypeMark + value.GetType().FullName + TypeLineEnd + EditorJsonUtility.ToJson(value);
        }


        /// <summary>В буфере значение того же типа, что у поля.</summary>
        public static bool CanPaste(SerializedProperty property)
        {
            var value = property.boxedValue;
            return value != null && TryReadBuffer(out var typeName, out _) && typeName == value.GetType().FullName;
        }


        /// <summary>Заменить значение поля тем, что в буфере. Отменяется через Undo.</summary>
        public static void Paste(SerializedProperty property)
        {
            if (!TryReadBuffer(out var typeName, out var json))
                return;

            var serializedObject = property.serializedObject;
            serializedObject.Update();

            var value = property.boxedValue;

            if (value == null || typeName != value.GetType().FullName)
                return;

            EditorJsonUtility.FromJsonOverwrite(json, value);
            property.boxedValue = value;
            serializedObject.ApplyModifiedProperties();
        }


        private static bool TryReadBuffer(out string typeName, out string json)
        {
            typeName = null;
            json = null;

            var buffer = EditorGUIUtility.systemCopyBuffer;

            if (string.IsNullOrEmpty(buffer) || !buffer.StartsWith(TypeMark, StringComparison.Ordinal))
                return false;

            var typeLineEnd = buffer.IndexOf(TypeLineEnd);

            if (typeLineEnd < 0)
                return false;

            typeName = buffer.Substring(TypeMark.Length, typeLineEnd - TypeMark.Length);
            json = buffer.Substring(typeLineEnd + 1);
            return true;
        }
    }
}
