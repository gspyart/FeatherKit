using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FeatherKit.Attributes;
using UnityEditor;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Раскладка полей одного уровня по секциям <see cref="FSectionAttribute"/> и плоским рамкам
    /// <see cref="FGroupStartAttribute"/>. Только расчёт: рисует <see cref="GroupedFieldsLayout"/>.
    ///
    /// Секция и рамка у поля — от последней метки выше него в том классе, где оно объявлено:
    /// метка ставится на первое поле группы, остальные идут за ним. Секции выстраиваются по номеру,
    /// внутри секции общее от родителей — раньше своего.
    /// </summary>
    internal static class FieldSectionsArrangement
    {
        private const BindingFlags DeclaredFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Отрисовка зовётся на каждом движении мыши — рефлексию считаем один раз.
        private static readonly Dictionary<Type, bool> typesWithSections = new Dictionary<Type, bool>();
        private static readonly Dictionary<(Type, string), FieldPlace> places = new Dictionary<(Type, string), FieldPlace>();


        // ── Есть ли секции ───────────────────────────────────────────────

        /// <summary>Размечены ли поля самого типа или его родителей секциями и рамками.</summary>
        public static bool IsUsedBy(Type type)
        {
            if (typesWithSections.TryGetValue(type, out var isUsed))
                return isUsed;

            isUsed = false;

            for (var current = type; current != null && !isUsed; current = current.BaseType)
                isUsed = current.GetFields(DeclaredFields).Any(IsSectionMark);

            typesWithSections[type] = isUsed;

            return isUsed;
        }

        public static bool IsSectionMark(FieldInfo field)
        {
            return field.IsDefined(typeof(FSectionAttribute), true) || field.IsDefined(typeof(FGroupStartAttribute), true);
        }


        // ── Раскладка ────────────────────────────────────────────────────

        /// <summary>Секции по порядку, в каждой — рамки по порядку, в каждой — поля.</summary>
        public static List<FieldSection> Arrange(IEnumerable<SerializedProperty> properties, Type ownerType)
        {
            // OrderBy сохраняет порядок равных — внутри одного класса поля идут, как в коде.
            var placedFields = properties
                .Select(property => (property, place: PlaceOf(ownerType, property.name)))
                .OrderBy(pair => pair.place.SectionOrder)
                .ThenByDescending(pair => pair.place.InheritanceDepth);

            var sections = new List<FieldSection>();

            foreach (var (property, place) in placedFields)
            {
                var isNewSection = sections.Count == 0 || sections[sections.Count - 1].Title != place.SectionTitle;

                if (isNewSection)
                    sections.Add(new FieldSection(place.SectionTitle));

                var groups = sections[sections.Count - 1].Groups;

                if (isNewSection || place.StartsGroup)
                    groups.Add(new FieldFrameGroup(place.GroupTitle));

                groups[groups.Count - 1].Properties.Add(property);
            }

            return sections;
        }


        // Где поле: в каком классе объявлено и насколько далеко в родителях.
        private static FieldPlace PlaceOf(Type ownerType, string fieldName)
        {
            if (places.TryGetValue((ownerType, fieldName), out var cached))
                return cached;

            var place = new FieldPlace(null, 0, null, false, 0);
            var depth = 0;

            for (var type = ownerType; type != null; type = type.BaseType, depth++)
            {
                if (type.GetField(fieldName, DeclaredFields) == null)
                    continue;

                place = PlaceInDeclaringType(type, fieldName, depth);
                break;
            }

            places[(ownerType, fieldName)] = place;

            return place;
        }

        // Идём по полям класса сверху вниз и помним последние секцию и рамку: поле без своей метки — в них.
        private static FieldPlace PlaceInDeclaringType(Type declaringType, string fieldName, int depth)
        {
            string sectionTitle = null;
            string groupTitle = null;
            var sectionOrder = 0;

            foreach (var field in declaringType.GetFields(DeclaredFields))
            {
                var section = field.GetCustomAttribute<FSectionAttribute>(true);
                var group = field.GetCustomAttribute<FGroupStartAttribute>(true);

                if (section != null)
                    (sectionTitle, sectionOrder, groupTitle) = (section.Title, section.Order, null);

                if (group?.SectionTitle != null)
                    (sectionTitle, sectionOrder, groupTitle) = (group.SectionTitle, group.SectionOrder, null);

                if (group != null)
                    groupTitle = group.Title;

                if (field.Name == fieldName)
                    return new FieldPlace(sectionTitle, sectionOrder, groupTitle, group != null, depth);
            }

            return new FieldPlace(sectionTitle, sectionOrder, groupTitle, false, depth);
        }


        // ── Части раскладки ──────────────────────────────────────────────

        /// <summary>Место поля: секция, рамка и насколько далеко в родителях оно объявлено.</summary>
        private readonly struct FieldPlace
        {
            public FieldPlace(string sectionTitle, int sectionOrder, string groupTitle, bool startsGroup, int inheritanceDepth)
            {
                SectionTitle = sectionTitle;
                SectionOrder = sectionOrder;
                GroupTitle = groupTitle;
                StartsGroup = startsGroup;
                InheritanceDepth = inheritanceDepth;
            }


            public string SectionTitle { get; }

            public int SectionOrder { get; }

            public string GroupTitle { get; }

            /// <summary>С этого поля начинается рамка.</summary>
            public bool StartsGroup { get; }

            /// <summary>0 — объявлено в самом классе, 1 — в родителе, и так далее.</summary>
            public int InheritanceDepth { get; }
        }
    }


    /// <summary>Секция в раскладке: подпись (null — без подписи) и рамки по порядку.</summary>
    internal sealed class FieldSection
    {
        public FieldSection(string title)
        {
            Title = title;
        }


        public string Title { get; }

        public List<FieldFrameGroup> Groups { get; } = new List<FieldFrameGroup>();
    }


    /// <summary>Рамка в секции: заголовок (null — поля без рамки) и поля по порядку.</summary>
    internal sealed class FieldFrameGroup
    {
        public FieldFrameGroup(string title)
        {
            Title = title;
        }


        public string Title { get; }

        public List<SerializedProperty> Properties { get; } = new List<SerializedProperty>();
    }
}
