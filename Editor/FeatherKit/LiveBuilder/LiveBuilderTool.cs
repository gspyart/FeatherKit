using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что делает мышь в окне сцены, пока включён Live Builder.</summary>
    public enum LiveBuilderTool
    {
        Box,
        Room,
        Building,
        Wall,
        Opening,
        Stairs,
        Ruler
    }


    /// <summary>
    /// Выбранный инструмент Live Builder, его название и подсказка. Свой у каждого человека, хранится в настройках редактора.
    /// </summary>
    public static class LiveBuilderToolSetting
    {
        // Сколько первых инструментов палитры — «Строить»; остальные — «Детали и мера».
        public const int BuildToolCount = 4;

        private const string PreferenceKey = "FeatherKit.LiveBuilder.Tool";

        // Порядок в палитре окна и панели сцены; номер в нём — клавиша 1, 2, 3… в окне сцены.
        public static readonly LiveBuilderTool[] PaletteOrder =
        {
            LiveBuilderTool.Box, LiveBuilderTool.Room, LiveBuilderTool.Wall, LiveBuilderTool.Building,
            LiveBuilderTool.Stairs, LiveBuilderTool.Opening, LiveBuilderTool.Ruler
        };


        // Сменился инструмент — окно и сцена перерисовываются, начатое рисование отменяется.
        public static event System.Action Changed;


        // Запомнен инструмент, которого в палитре больше нет, — это бывший «Вырез»: он теперь в «Проёме».
        public static LiveBuilderTool Current
        {
            get
            {
                var stored = (LiveBuilderTool)EditorPrefs.GetInt(PreferenceKey, (int)LiveBuilderTool.Box);
                return System.Array.IndexOf(PaletteOrder, stored) >= 0 ? stored : LiveBuilderTool.Opening;
            }
            set
            {
                if (Current == value)
                    return;

                EditorPrefs.SetInt(PreferenceKey, (int)value);
                Changed?.Invoke();
                SceneView.RepaintAll();
            }
        }


        // Клавиша инструмента в окне сцены: «1» у первого в палитре.
        public static string HotkeyOf(LiveBuilderTool tool)
        {
            return (System.Array.IndexOf(PaletteOrder, tool) + 1).ToString();
        }


        // Инструмент по клавише-цифре; такой цифры в палитре нет — false.
        public static bool TryGetByHotkey(KeyCode key, out LiveBuilderTool tool)
        {
            var index = key is >= KeyCode.Alpha1 and <= KeyCode.Alpha9 ? key - KeyCode.Alpha1 : -1;
            tool = index >= 0 && index < PaletteOrder.Length ? PaletteOrder[index] : default;
            return index >= 0 && index < PaletteOrder.Length;
        }


        public static string DisplayName(LiveBuilderTool tool)
        {
            return tool switch
            {
                LiveBuilderTool.Room => "Комната",
                LiveBuilderTool.Building => "Здание",
                LiveBuilderTool.Wall => "Стена",
                LiveBuilderTool.Opening => "Проём",
                LiveBuilderTool.Stairs => "Лестница",
                LiveBuilderTool.Ruler => "Линейка",
                _ => "Коробка"
            };
        }


        public static string Tooltip(LiveBuilderTool tool)
        {
            return tool switch
            {
                LiveBuilderTool.Room => "Пустая комната на высоту этажа: стены, пол, потолок. Прямоугольник — протяни по полу, по контуру — кликами точки. " +
                                        "В здании — только недостающие перегородки, внутри дома из коробок — вырез или стены",
                LiveBuilderTool.Building => "Кликами ставишь контур — по нему встаёт здание: стены на все этажи, перекрытия и крыша. Угол или плавно — переключатель или Shift. Клик по первой точке или Enter — готово",
                LiveBuilderTool.Wall => "Кликами ставишь точки — по ним встаёт стена. Угол или плавно — переключатель в окне или Shift на точке. Enter — готово",
                LiveBuilderTool.Opening => "Клик по стене — проём выбранного в окне размера насквозь. Протяни по любой поверхности, хоть по косой, — свой размер: " +
                                           "насквозь или ниша. Форма — прямоугольник, арка, круг. Клик по дыре — выделить проём",
                LiveBuilderTool.Stairs => "Как кубик: протяни по полу длину и ширину, отпусти, мышью высоту, клик. Ступени подстраиваются сами",
                LiveBuilderTool.Ruler => "Клик — начало, клик — конец: расстояние и допрыгнет ли игрок",
                _ => "Протяни по поверхности, мышью глубина, клик — коробка. Клик по грани — выделить коробку"
            };
        }
    }
}
