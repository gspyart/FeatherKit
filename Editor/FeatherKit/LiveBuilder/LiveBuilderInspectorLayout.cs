using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общий вид инспекторов Live Builder: группы с заголовком, свёрнутые группы для редкого («Материалы», «Тонко»),
    /// поля по именам. Порядок групп везде один: размер → этажи → вид → тонко.
    /// </summary>
    public static class LiveBuilderInspectorLayout
    {
        private const string FoldoutKeyPrefix = "FeatherKit.LiveBuilder.Inspector.";


        // Заголовок группы с отступом сверху.
        public static void Section(string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }


        // Свёрнутая группа: открыта ли. Помнится до перезапуска Unity, одна на все объекты этого вида.
        public static bool Foldout(string key, string title)
        {
            EditorGUILayout.Space(6f);
            var isOpen = SessionState.GetBool(FoldoutKeyPrefix + key, false);
            isOpen = EditorGUILayout.Foldout(isOpen, title, true, EditorStyles.foldoutHeader);
            SessionState.SetBool(FoldoutKeyPrefix + key, isOpen);
            return isOpen;
        }


        public static void Properties(SerializedObject serializedObject, params string[] names)
        {
            foreach (var name in names)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        }


        // Поле, которое задаёт кто-то другой — здание или тип стены: видно значение, менять нельзя.
        public static void DrivenValue(string label, string value, string tooltip, string source = "от здания")
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), new GUIContent(value + " — " + source, tooltip));
        }


        // Группа «Этажи» у всего с копиями вверх: свои число и шаг, а в здании — только показать, задаёт привязка ниже.
        public static void StoreyCopies(SerializedObject serializedObject, IStoreyCopies storeyCopies, bool isLinkedToBuilding)
        {
            Section("Этажи");

            if (isLinkedToBuilding)
                DrivenValue("Copies Up", storeyCopies.CopiesUp.ToString(), "Сколько этажей — в привязке к зданию ниже");
            else
                Properties(serializedObject, "copiesUp", "copyStep");
        }
    }
}
