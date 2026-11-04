using System;
using System.Linq;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Цвет иконки группы, как цветные метки коллекций в Blender. Blue — обычная иконка метки.</summary>
    public enum ObjectGroupColor
    {
        Blue,
        Red,
        Orange,
        Yellow,
        Green,
        Violet,
        Pink,
        Gray
    }


    /// <summary>
    /// Цвет иконки группы <see cref="ObjectGroup"/>. Хранится не в метке, а иконкой самого объекта — штатной, как
    /// в шапке инспектора объекта: видна в окне сцены и в окне групп, пишется в сцену, отменяется Ctrl+Z.
    /// Синий — иконки у объекта нет, остаётся обычная иконка метки.
    /// </summary>
    public static class ObjectGroupColors
    {
        private const string IconsFolder = "Packages/com.gspy.featherkit/Editor/FeatherKit/ObjectGroups/Icons/";
        private const string IconNamePrefix = "ObjectGroup Icon ";
        private const string UndoName = "Change Group Color";


        public static ObjectGroupColor[] All => (ObjectGroupColor[])Enum.GetValues(typeof(ObjectGroupColor));

        // Синяя иконка самой метки ObjectGroup.
        private static Texture DefaultIcon => AssetPreview.GetMiniTypeThumbnail(typeof(ObjectGroup));


        // ── Цвет ─────────────────────────────────────────

        // Своя иконка объекта не из набора групп — считаем синей: её поставили руками, не трогаем, пока не выберут цвет.
        public static ObjectGroupColor ColorOf(ObjectGroup group)
        {
            var objectIcon = EditorGUIUtility.GetIconForObject(group.gameObject);

            if (objectIcon == null)
                return ObjectGroupColor.Blue;

            return All.FirstOrDefault(color => color != ObjectGroupColor.Blue && IconOf(color) == objectIcon);
        }


        public static void SetColor(ObjectGroup group, ObjectGroupColor color)
        {
            Undo.RecordObject(group.gameObject, UndoName);
            EditorGUIUtility.SetIconForObject(group.gameObject, color == ObjectGroupColor.Blue ? null : IconOf(color) as Texture2D);
        }


        // ── Иконки ───────────────────────────────────────

        // Иконка для строки окна групп: цветная, если цвет выбран, иначе иконка метки.
        public static Texture IconFor(ObjectGroup group)
        {
            var color = ColorOf(group);
            return color == ObjectGroupColor.Blue ? DefaultIcon : IconOf(color);
        }


        public static Texture IconOf(ObjectGroupColor color)
        {
            return color == ObjectGroupColor.Blue
                ? DefaultIcon
                : AssetDatabase.LoadAssetAtPath<Texture2D>(IconsFolder + IconNamePrefix + color + ".png");
        }
    }
}
