using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вид блоков сценария как в Scratch: цвет и иконка по категории, вложенный столбик — в цветной скобке.
    /// - категория — первая часть пути блока: «Sound/…», «Control/…»; игра добавляет свои через RegisterCategory
    /// - подпись поля из ScenarioLabelAttribute
    /// </summary>
    internal static class ScenarioBlockStyle
    {
        private const float BackgroundAlpha = 0.16f;
        private const float ActiveBackgroundAlpha = 0.26f;
        private const float StripeWidth = 4f;
        private const float BracketWidth = 3f;
        private const float BracketCapLength = 10f;
        private const string WhenCategory = "Trigger";

        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.55f);

        // Разные оттенки на всё колесо: полоса категории на ноде — 3 px, похожие цвета там сливаются.
        // События — жёлтые, поток — оранжевый, как привыкли по Scratch.
        private static readonly Dictionary<string, Color> ColorsByCategory = new Dictionary<string, Color>
        {
            { "Trigger", Hex(0xFFD24A) },
            { "Signals", Hex(0xFFD24A) },
            { "Control", Hex(0xFF8A3D) },
            { "Flow", Hex(0xFF8A3D) },
            { "Logic", Hex(0xB5E655) },
            { "Variables", Hex(0x2EC4B6) },
            { "Object", Hex(0x4FA3E0) },
            { "Transform", Hex(0x6A6CF0) },
            { "Light", Hex(0xF2E2A0) },
            { "Sound", Hex(0xD35FC9) },
            { "Animation", Hex(0x9A63E8) },
            { "Timeline", Hex(0x9A63E8) },
            { "Player", Hex(0x3DBE6A) },
            { "Scenario", Hex(0xE84B5A) }
        };

        // Иконка категории — иконка этого типа Unity: так её не надо рисовать и она совпадает с инспектором.
        private static readonly Dictionary<string, Type> IconTypesByCategory = new Dictionary<string, Type>
        {
            { "Light", typeof(Light) },
            { "Sound", typeof(AudioSource) },
            { "Animation", typeof(Animator) },
            { "Timeline", typeof(UnityEngine.Playables.PlayableDirector) },
            { "Object", typeof(GameObject) },
            { "Transform", typeof(Transform) },
            { "Trigger", typeof(BoxCollider) },
            { "Variables", typeof(ScriptableObject) },
            { "Signals", typeof(ScriptableObject) },
            { "Scenario", typeof(Scenario) },
            { "Code", typeof(MonoScript) }
        };

        private static readonly Dictionary<(Type, string), GUIContent> LabelsByField = new Dictionary<(Type, string), GUIContent>();


        // ── Категории ────────────────────────────────────────────────────

        /// <summary>Категория игры: цвет и тип, чья иконка её обозначает. Звать из редакторного кода игры, [InitializeOnLoad].</summary>
        public static void RegisterCategory(string category, Color color, Type iconType)
        {
            ColorsByCategory[category] = color;
            IconTypesByCategory[category] = iconType;
        }


        /// <summary>Цвет блока по категории. Нода When из нескольких событий — цвет событий.</summary>
        public static Color ColorOf(Type blockType)
        {
            var category = blockType == typeof(ScenarioWhenNode) ? WhenCategory : CategoryOf(blockType);

            return ColorsByCategory.TryGetValue(category, out var color) ? color : DefaultColor;
        }

        /// <summary>Тип Unity, чья иконка обозначает категорию блока. Null — своей иконки у категории нет.</summary>
        public static Type IconTypeOf(Type blockType)
        {
            return IconTypeOfCategory(CategoryOf(blockType));
        }

        public static Type IconTypeOfCategory(string category)
        {
            return category != null && IconTypesByCategory.TryGetValue(category, out var iconType) ? iconType : null;
        }

        /// <summary>Блок из категорий потока («Flow», «Control»): у них общая иконка-развилка.</summary>
        public static bool IsFlowCategory(Type blockType)
        {
            var category = CategoryOf(blockType);

            return category == "Flow" || category == "Control";
        }

        private static string CategoryOf(Type blockType)
        {
            var path = ScenarioBlockCatalog.MenuPathOf(blockType);
            var slash = path.IndexOf('/');

            return slash > 0 ? path.Substring(0, slash) : "";
        }


        /// <summary>Цвет блока, внутри которого лежит это поле; не внутри блока — серый.</summary>
        public static Color OwnerColorOf(SerializedProperty property)
        {
            var path = property.propertyPath;

            for (var cut = path.LastIndexOf('.'); cut > 0; cut = path.LastIndexOf('.'))
            {
                path = path.Substring(0, cut);
                var owner = property.serializedObject.FindProperty(path);

                if (owner != null && owner.propertyType == SerializedPropertyType.ManagedReference
                                  && owner.managedReferenceValue != null)
                    return ColorOf(owner.managedReferenceValue.GetType());
            }

            return DefaultColor;
        }

        public static void DrawBlockBackground(Rect rect, Color color, bool isActive)
        {
            var inner = new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f);
            var fill = color;
            fill.a = isActive ? ActiveBackgroundAlpha : BackgroundAlpha;

            EditorGUI.DrawRect(inner, fill);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, StripeWidth, inner.height), color);
        }

        /// <summary>Скоба слева от вложенного столбика: «C» в цвет блока-владельца.</summary>
        public static void DrawBracket(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, BracketWidth, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, BracketCapLength, BracketWidth), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BracketWidth, BracketCapLength, BracketWidth), color);
        }


        // ── Подписи полей ────────────────────────────────────────────────

        /// <summary>Подпись поля блока: из ScenarioLabelAttribute, а без неё — имя поля, как обычно в Unity.</summary>
        public static GUIContent LabelOf(Type blockType, SerializedProperty field)
        {
            var key = (blockType, field.name);

            if (LabelsByField.TryGetValue(key, out var label))
                return label;

            var fieldInfo = FindField(blockType, field.name);
            var text = fieldInfo?.GetCustomAttribute<ScenarioLabelAttribute>()?.Text ?? field.displayName;
            var tooltip = fieldInfo?.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? field.tooltip;

            // Подпись на узкой ноде обрезается — полный текст хотя бы в подсказке.
            if (string.IsNullOrEmpty(tooltip))
                tooltip = text;

            label = new GUIContent(text, tooltip);
            LabelsByField[key] = label;

            return label;
        }

        /// <summary>Поле класса по имени, приватное тоже, вместе с полями базовых классов.</summary>
        public static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null)
                    return field;
            }

            return null;
        }


        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
