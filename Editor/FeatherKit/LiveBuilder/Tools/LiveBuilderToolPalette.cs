using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Палитра инструментов Live Builder: все наследники <see cref="LiveBuilderTool"/> по одному, по их месту в палитре,
    /// и выбранный из них. Номер места — клавиша в окне сцены: «1» у первого. Выбранный свой у каждого человека, помнится в настройках редактора по Id.
    /// </summary>
    public static class LiveBuilderToolPalette
    {
        private const string PreferenceKey = "FeatherKit.LiveBuilder.CurrentTool";

        // Раньше инструмент помнился номером в списке — он читается один раз и переписывается в Id.
        private const string NumberPreferenceKey = "FeatherKit.LiveBuilder.Tool";

        // Номер за концом старого списка — бывший «Вырез»: он теперь в «Проёме».
        private const string RemovedToolId = "Opening";

        // Номер в старом списке → Id.
        private static readonly string[] IdsByOldNumber = { "Box", "Room", "Building", "Wall", "Opening", "Stairs", "Ruler", "Select" };

        private static List<LiveBuilderTool> tools;
        private static LiveBuilderTool current;


        // Сменился инструмент — окно и сцена перерисовываются, начатое рисование отменяется.
        public static event System.Action Changed;


        public static IReadOnlyList<LiveBuilderTool> Tools => tools ??= CreateTools();

        // Запомнен инструмент, которого больше нет, — «Выделение».
        public static LiveBuilderTool Current
        {
            get => current ??= FindById(StoredId()) ?? Get<LiveBuilderSelectTool>();
            set
            {
                if (value == null || Current == value)
                    return;

                current = value;
                EditorPrefs.SetString(PreferenceKey, value.Id);
                Changed?.Invoke();
                SceneView.RepaintAll();
            }
        }


        // ── Инструменты ──────────────────────────────────────────────────

        public static T Get<T>() where T : LiveBuilderTool
        {
            foreach (var tool in Tools)
                if (tool is T found)
                    return found;

            return null;
        }


        // Клавиша инструмента в окне сцены: «1» у первого в палитре.
        public static string HotkeyOf(LiveBuilderTool tool)
        {
            return (IndexOf(tool) + 1).ToString();
        }


        // Инструмент по клавише-цифре; такой цифры в палитре нет — false.
        public static bool TryGetByHotkey(KeyCode key, out LiveBuilderTool tool)
        {
            var index = key is >= KeyCode.Alpha1 and <= KeyCode.Alpha9 ? key - KeyCode.Alpha1 : -1;
            var isFound = index >= 0 && index < Tools.Count;
            tool = isFound ? Tools[index] : null;
            return isFound;
        }


        public static int IndexOf(LiveBuilderTool tool)
        {
            for (var index = 0; index < Tools.Count; index++)
                if (Tools[index] == tool)
                    return index;

            return -1;
        }


        private static List<LiveBuilderTool> CreateTools()
        {
            var created = LiveBuilderTypeCatalog.CreateAll<LiveBuilderTool>();
            created.Sort((first, second) => first.PaletteOrder.CompareTo(second.PaletteOrder));
            return created;
        }


        private static LiveBuilderTool FindById(string id)
        {
            foreach (var tool in Tools)
                if (tool.Id == id)
                    return tool;

            return null;
        }


        // ── Хранение ─────────────────────────────────────────────────────

        // Id из настроек редактора; помнится старый номер — переводит его в Id и запоминает уже по Id.
        private static string StoredId()
        {
            if (EditorPrefs.HasKey(PreferenceKey) || !EditorPrefs.HasKey(NumberPreferenceKey))
                return EditorPrefs.GetString(PreferenceKey, "");

            var number = EditorPrefs.GetInt(NumberPreferenceKey, -1);
            var id = number >= 0 && number < IdsByOldNumber.Length ? IdsByOldNumber[number] : RemovedToolId;
            EditorPrefs.SetString(PreferenceKey, id);
            return id;
        }
    }
}
