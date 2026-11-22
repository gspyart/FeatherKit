using System.Collections.Generic;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Палитра инструментов Live Builder: все наследники <see cref="LiveBuilderTool"/> по одному, по их месту в палитре,
    /// и выбранный из них. Клавиша инструмента — его ярлык в Edit → Shortcuts (по умолчанию номер места: «1» у первого);
    /// клавиша уже выбранного — следующий режим, с Shift — предыдущий. Выбранный свой у каждого человека, помнится в настройках редактора по Id.
    /// </summary>
    public static class LiveBuilderToolPalette
    {
        private const string PreferenceKey = "FeatherKit.LiveBuilder.CurrentTool";

        // Раньше инструмент помнился номером в списке — он читается один раз и переписывается в Id.
        private const string NumberPreferenceKey = "FeatherKit.LiveBuilder.Tool";

        // Бывшие инструменты: «Комната» — теперь режим Room «Коробки», «Проём» — «Cut».
        private const string OldRoomId = "Room";
        private const string OldOpeningId = "Opening";

        // Номер за концом старого списка — бывший «Вырез»: его заменил «Проём», на него и переводим.
        private const string ReplacementForRemovedToolId = OldOpeningId;

        // Номер в старом списке → Id.
        private static readonly string[] IdsByOldNumber = { "Box", OldRoomId, "Building", "Wall", OldOpeningId, "Stairs", "Ruler", "Select" };

        private static List<LiveBuilderTool> tools;
        private static LiveBuilderTool current;


        // Сменился инструмент — окно и сцена перерисовываются, начатое рисование отменяется.
        public static event System.Action Changed;


        public static IReadOnlyList<LiveBuilderTool> Tools => tools ??= CreateTools();

        // Запомнен инструмент, которого больше нет, — «Выделение». Чужой экземпляр (не из палитры) — тот же инструмент палитры по Id;
        // такого Id в палитре нет — не меняется.
        public static LiveBuilderTool Current
        {
            get => current ??= FindById(StoredId()) ?? Get<LiveBuilderSelectTool>();
            set
            {
                var paletteTool = value != null ? FindById(value.Id) : null;

                if (paletteTool == null || Current == paletteTool)
                    return;

                current = paletteTool;
                EditorPrefs.SetString(PreferenceKey, paletteTool.Id);
                Changed?.Invoke();
                SceneView.RepaintAll();
            }
        }


        // ── Инструменты ──────────────────────────────────────────────────

        // Инструмент палитры этого вида; нет такого — null.
        public static T Get<T>() where T : LiveBuilderTool
        {
            foreach (var tool in Tools)
                if (tool is T found)
                    return found;

            return null;
        }


        public static void Select<T>() where T : LiveBuilderTool
        {
            Current = Get<T>();
        }


        // Клавиша инструмента: выбрать его; уже выбран — следующий режим.
        public static void SelectToolOrNextMode<T>() where T : LiveBuilderTool
        {
            if (Current is T)
                Current.Modes?.SelectNext();
            else
                Select<T>();
        }


        // Shift + клавиша инструмента: выбрать его; уже выбран — предыдущий режим.
        public static void SelectToolOrPreviousMode<T>() where T : LiveBuilderTool
        {
            if (Current is T)
                Current.Modes?.SelectPrevious();
            else
                Select<T>();
        }


        // Подпись клавиши инструмента — из его привязки в Shortcuts: переназначил — подпись следом.
        public static string HotkeyOf(LiveBuilderTool tool)
        {
            return LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.ToolOf(tool));
        }


        // Место инструмента в палитре, с нуля; не из палитры — -1.
        public static int IndexOf(LiveBuilderTool tool)
        {
            for (var index = 0; index < Tools.Count; index++)
                if (Tools[index] == tool)
                    return index;

            return -1;
        }


        // По одному экземпляру каждого наследника, по месту в палитре.
        private static List<LiveBuilderTool> CreateTools()
        {
            var created = LiveBuilderTypeCatalog.CreateAll<LiveBuilderTool>();
            created.Sort((first, second) => first.PaletteOrder.CompareTo(second.PaletteOrder));
            return created;
        }


        // Инструмент палитры с этим Id; нет такого — null.
        private static LiveBuilderTool FindById(string id)
        {
            foreach (var tool in Tools)
                if (tool.Id == id)
                    return tool;

            return null;
        }


        // ── Хранение ─────────────────────────────────────────────────────

        // Id из настроек редактора; помнится старый номер или Id бывшего инструмента — переводит в нынешний и запоминает его.
        private static string StoredId()
        {
            var storedId = EditorPrefs.GetString(PreferenceKey, "");

            if (!EditorPrefs.HasKey(PreferenceKey) && EditorPrefs.HasKey(NumberPreferenceKey))
            {
                var number = EditorPrefs.GetInt(NumberPreferenceKey, -1);
                storedId = number >= 0 && number < IdsByOldNumber.Length ? IdsByOldNumber[number] : ReplacementForRemovedToolId;
            }

            var id = MigrateStoredId(storedId);

            if (id != EditorPrefs.GetString(PreferenceKey, ""))
                EditorPrefs.SetString(PreferenceKey, id);

            return id;
        }


        // Id бывшего инструмента → нынешний: «Комната» — «Коробка» в режиме Room (режим ставится тут же), «Проём» — «Cut». Прочие — как есть.
        public static string MigrateStoredId(string storedId)
        {
            switch (storedId)
            {
                case OldRoomId:
                    LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Room;
                    return Get<LiveBuilderBoxTool>().Id;

                case OldOpeningId:
                    return Get<LiveBuilderCutTool>().Id;

                default:
                    return storedId;
            }
        }
    }
}
