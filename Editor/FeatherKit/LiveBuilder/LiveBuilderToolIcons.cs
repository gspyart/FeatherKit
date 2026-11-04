using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки инструментов Live Builder — рисуются кодом из линий инструмента (<see cref="LiveBuilderTool.IconLines"/>, <see cref="LiveBuilderIconRasterizer"/>),
    /// без файлов картинок: чёткие на любом экране, под светлую и тёмную тему. Каждый растеризуется один раз и запоминается (<see cref="LiveBuilderIconCache"/>).
    /// Белый вариант красят при выводе (выбранная плитка — цветом акцента), цветной под тему — для готовых кнопок Unity.
    /// </summary>
    public static class LiveBuilderToolIcons
    {
        // Белый значок: цвет задают при выводе через GUI.color.
        public static Texture2D White(LiveBuilderTool tool)
        {
            return Get(tool, Color.white);
        }


        // Значок цвета обычных значков редактора — для кнопок, которые сами красить не умеют.
        public static Texture2D Themed(LiveBuilderTool tool)
        {
            return Get(tool, LiveBuilderIconCache.ThemedColor);
        }


        // Значок в прямоугольнике окна, нужного цвета.
        public static void Draw(Rect rect, LiveBuilderTool tool, Color color)
        {
            LiveBuilderIconCache.Draw(rect, White(tool), color);
        }


        private static Texture2D Get(LiveBuilderTool tool, Color color)
        {
            return LiveBuilderIconCache.Get(tool.Id, color, () => tool.IconLines);
        }
    }
}
