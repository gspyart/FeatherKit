using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Attributes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует поля объекта в инспекторе с группами FeatherKit: рамки <see cref="FGroupBoxAttribute"/>
    /// и раскрытые поля-классы <see cref="FInlineFieldsAttribute"/>.
    ///
    /// Не инспектор, а общая отрисовка для инспекторов: ею пользуются инспектор компонентов
    /// (<see cref="FDescriptionInspector"/>), ассетов (<see cref="FScriptableObjectInspector"/>)
    /// и база своих инспекторов (<see cref="FeatherEditor"/>).
    ///
    /// В поле-класс заходит сам, только если внутри есть наши атрибуты, — иначе поле рисует Unity,
    /// как обычно. Поэтому PropertyDrawer класса, внутри которого стоят группы, не сработает:
    /// группы важнее. Списки, их элементы и поля со своими PropertyDrawer рисует Unity — групп
    /// внутри них нет.
    ///
    /// Свёрнута ли рамка — хранится в EditorPrefs по типу объекта и пути поля: переживает
    /// перекомпиляцию и одинаково у всех ассетов одного типа.
    /// </summary>
    public static class GroupedFieldsLayout
    {
        private const string ScriptFieldPath = "m_Script";

        // Шаг отступа Unity на один уровень indentLevel — у него это внутренняя константа.
        private const float IndentStepWidth = 15f;

        private const float InlineTitleGap = 4f;

        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Отрисовка зовётся на каждом движении мыши — рефлексию по типам считаем один раз.
        private static readonly Dictionary<Type, bool> typesWithGroups = new Dictionary<Type, bool>();
        private static readonly Dictionary<(Type, string), FieldInfo> fieldsByName = new Dictionary<(Type, string), FieldInfo>();


        // ── Поля объекта ─────────────────────────────────────────────────

        /// <summary>Есть ли у типа наши атрибуты — на нём самом или внутри его полей-классов.</summary>
        public static bool IsUsedBy(Type type)
        {
            if (type == null)
                return false;

            if (typesWithGroups.TryGetValue(type, out var isUsed))
                return isUsed;

            // Заранее «нет»: класс, который ссылается сам на себя, иначе зациклит проверку.
            typesWithGroups[type] = false;
            isUsed = HasGroupedFields(type);
            typesWithGroups[type] = isUsed;

            return isUsed;
        }

        private static bool HasGroupedFields(Type type)
        {
            foreach (var field in SerializedFieldsOf(type))
            {
                if (field.IsDefined(typeof(FGroupBoxAttribute), true) || field.IsDefined(typeof(FInlineFieldsAttribute), true))
                    return true;

                if (IsSerializedClass(field.FieldType) && IsUsedBy(field.FieldType))
                    return true;
            }

            return false;
        }

        private static IEnumerable<FieldInfo> SerializedFieldsOf(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(InstanceFields))
                {
                    if (field.IsNotSerialized)
                        continue;

                    if (field.IsPublic || field.IsDefined(typeof(SerializeField), true))
                        yield return field;
                }
            }
        }

        // Класс или структура, которую Unity раскрывает по полям, — не ссылка на ассет и не список.
        private static bool IsSerializedClass(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsArray)
                return false;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type) || type.IsGenericType)
                return false;

            return type.IsDefined(typeof(SerializableAttribute), false);
        }


        /// <summary>
        /// Нарисовать все поля объекта с группами. Update и ApplyModifiedProperties — за вызывающим,
        /// как у <see cref="Editor.serializedObject"/> в любом инспекторе.
        /// </summary>
        /// <param name="drawScriptField">Строка со ссылкой на скрипт, как у Unity. Свой инспектор со своей шапкой её отключает.</param>
        public static void DrawFields(SerializedObject serializedObject, bool drawScriptField = true)
        {
            var targetType = serializedObject.targetObject.GetType();
            var properties = new List<SerializedProperty>();
            var iterator = serializedObject.GetIterator();

            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyPath != ScriptFieldPath)
                    properties.Add(iterator.Copy());
                else if (drawScriptField)
                    DrawScriptField(iterator);
            }

            DrawGroupedFields(properties, targetType, targetType.FullName);
        }

        private static void DrawScriptField(SerializedProperty script)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(script, true);
        }


        // ── Группы ───────────────────────────────────────────────────────

        // stateKey — чей это уровень: по нему запоминаем, какие рамки свёрнуты.
        private static void DrawGroupedFields(IEnumerable<SerializedProperty> properties, Type ownerType, string stateKey)
        {
            var root = FieldsTreeNode.ForGroup(string.Empty, string.Empty);

            foreach (var property in properties)
            {
                var field = FieldOf(ownerType, property.name);
                var groupPath = field?.GetCustomAttribute<FGroupBoxAttribute>(true)?.Path;

                GroupOf(root, groupPath).Children.Add(FieldsTreeNode.ForField(property, field));
            }

            DrawNodes(root.Children, stateKey, firstAccentIndex: 0);
        }

        // «Общее/Камера» — рамка «Камера» внутри «Общее». Нет такой — встаёт там, где встретилось первое её поле.
        private static FieldsTreeNode GroupOf(FieldsTreeNode root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var group = root;

            foreach (var part in path.Split(FGroupBoxAttribute.PathSeparator))
            {
                var title = part.Trim();

                if (title.Length == 0)
                    continue;

                var child = group.Children.Find(node => node.IsGroup && node.GroupTitle == title);

                if (child == null)
                {
                    var childPath = group.GroupPath.Length == 0 ? title : group.GroupPath + FGroupBoxAttribute.PathSeparator + title;

                    child = FieldsTreeNode.ForGroup(title, childPath);
                    group.Children.Add(child);
                }

                group = child;
            }

            return group;
        }


        // Соседние рамки — разными цветами метки; вложенные начинают со следующего цвета после родителя.
        private static void DrawNodes(List<FieldsTreeNode> nodes, string stateKey, int firstAccentIndex)
        {
            var accentIndex = firstAccentIndex;

            foreach (var node in nodes)
            {
                if (node.IsGroup)
                    DrawGroup(node, stateKey, accentIndex++);
                else
                    DrawField(node.Property, node.Field, stateKey);
            }
        }

        // Рамка встаёт на текущий отступ, а внутри отступ с нуля: иначе поля в ней уезжают вправо дважды.
        private static void DrawGroup(FieldsTreeNode group, string stateKey, int accentIndex)
        {
            var indentLevel = EditorGUI.indentLevel;

            if (indentLevel > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * IndentStepWidth);
                EditorGUILayout.BeginVertical();
            }

            EditorGUI.indentLevel = 0;
            InspectorSection.Draw($"Groups.{stateKey}.{group.GroupPath}", group.GroupTitle, accentIndex, () => DrawNodes(group.Children, stateKey, accentIndex + 1));
            EditorGUI.indentLevel = indentLevel;

            if (indentLevel > 0)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }


        // ── Поля ─────────────────────────────────────────────────────────

        private static void DrawField(SerializedProperty property, FieldInfo field, string stateKey)
        {
            var isInline = field != null && field.IsDefined(typeof(FInlineFieldsAttribute), true);

            if (!CanOpenInside(property, field, isInline))
            {
                EditorGUILayout.PropertyField(property, true);
                return;
            }

            DrawDecorators(field);

            var label = new GUIContent(property.displayName, property.tooltip);

            if (isInline)
            {
                // Просвет отделяет заголовок от полей предыдущего блока.
                EditorGUILayout.Space(InlineTitleGap);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }
            else
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);

                if (!property.isExpanded)
                    return;
            }

            EditorGUI.indentLevel++;
            DrawGroupedFields(ChildrenOf(property), field.FieldType, stateKey + "." + property.propertyPath);
            EditorGUI.indentLevel--;
        }

        // Заходим в поле сами, только когда внутри есть что группировать или его просили раскрыть.
        private static bool CanOpenInside(SerializedProperty property, FieldInfo field, bool isInline)
        {
            if (field == null || property.propertyType != SerializedPropertyType.Generic || property.isArray || !property.hasVisibleChildren)
                return false;

            return isInline || IsUsedBy(field.FieldType);
        }

        // Поле рисуем сами, а не через Unity, — значит, и его [Header] и [Space] тоже.
        private static void DrawDecorators(FieldInfo field)
        {
            foreach (var decorator in field.GetCustomAttributes<PropertyAttribute>(true))
            {
                switch (decorator)
                {
                    case SpaceAttribute space:
                        EditorGUILayout.Space(space.height);
                        break;

                    case HeaderAttribute header:
                        EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);
                        EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
                        break;
                }
            }
        }


        // Поле по имени свойства — в самом типе или у родителей: приватные поля родителя видны только с его уровня.
        private static FieldInfo FieldOf(Type ownerType, string name)
        {
            if (fieldsByName.TryGetValue((ownerType, name), out var cached))
                return cached;

            FieldInfo found = null;

            for (var current = ownerType; current != null && found == null; current = current.BaseType)
                found = current.GetField(name, InstanceFields);

            fieldsByName[(ownerType, name)] = found;

            return found;
        }

        private static IEnumerable<SerializedProperty> ChildrenOf(SerializedProperty property)
        {
            var children = new List<SerializedProperty>();
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            for (var enterChildren = true; iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end); enterChildren = false)
                children.Add(iterator.Copy());

            return children;
        }


        // ── Дерево полей ─────────────────────────────────────────────────

        /// <summary>Узел дерева полей одного уровня: либо поле, либо рамка со своими узлами внутри.</summary>
        private sealed class FieldsTreeNode
        {
            private FieldsTreeNode(SerializedProperty property, FieldInfo field, string groupTitle, string groupPath)
            {
                Property = property;
                Field = field;
                GroupTitle = groupTitle;
                GroupPath = groupPath;
            }


            public bool IsGroup => Property == null;

            public SerializedProperty Property { get; }

            public FieldInfo Field { get; }

            public string GroupTitle { get; }

            public string GroupPath { get; }

            public List<FieldsTreeNode> Children { get; } = new List<FieldsTreeNode>();


            public static FieldsTreeNode ForField(SerializedProperty property, FieldInfo field)
            {
                return new FieldsTreeNode(property, field, null, null);
            }

            public static FieldsTreeNode ForGroup(string title, string path)
            {
                return new FieldsTreeNode(null, null, title, path);
            }
        }
    }
}
