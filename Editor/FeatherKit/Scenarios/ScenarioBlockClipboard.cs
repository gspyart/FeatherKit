using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Буфер блоков сценария: скопировать блок или весь раздел и вставить в другой сценарий.
    /// Хранит снимки настроек, а не сами блоки — правка оригинала копию не трогает.
    /// </summary>
    internal static class ScenarioBlockClipboard
    {
        private static readonly List<BlockSnapshot> CopiedBlocks = new List<BlockSnapshot>();


        public static int Count => CopiedBlocks.Count;


        public static void Copy(IEnumerable<object> blocks)
        {
            CopiedBlocks.Clear();

            foreach (var block in blocks.Where(block => block != null))
                CopiedBlocks.Add(new BlockSnapshot(block.GetType(), EditorJsonUtility.ToJson(block)));
        }

        public static bool CanPasteInto(Type blockBaseType)
        {
            return CopiedBlocks.Count > 0 && CopiedBlocks.All(snapshot => blockBaseType.IsAssignableFrom(snapshot.BlockType));
        }

        /// <summary>Новые блоки из буфера — каждый раз свои, сколько ни вставляй.</summary>
        public static List<object> CreateCopies()
        {
            return CopiedBlocks.Select(snapshot => snapshot.Restore()).ToList();
        }


        // Копия через JSON: вставка элемента в массив дала бы второй указатель на ТОТ ЖЕ блок.
        public static object Clone(object block)
        {
            return new BlockSnapshot(block.GetType(), EditorJsonUtility.ToJson(block)).Restore();
        }


        /// <summary>Класс блока и его настройки в JSON.</summary>
        private readonly struct BlockSnapshot
        {
            private readonly string json;


            public BlockSnapshot(Type blockType, string json)
            {
                BlockType = blockType;
                this.json = json;
            }


            public Type BlockType { get; }


            public object Restore()
            {
                var block = Activator.CreateInstance(BlockType);
                EditorJsonUtility.FromJsonOverwrite(json, block);

                return block;
            }
        }
    }
}
