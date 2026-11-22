using System;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Режимы инструмента Live Builder — виды одного рисования, между которыми переключает клавиша инструмента.
    /// - названия, значки и подсказки режимов — для строки инструмента
    /// - текущий режим живёт в настройке самого инструмента: так он и помнится
    /// - следующий и предыдущий — по кругу
    /// </summary>
    public sealed class LiveBuilderToolModes
    {
        private readonly LiveBuilderToolModeLabel[] labels;
        private readonly Func<int> readCurrent;
        private readonly Action<int> writeCurrent;


        public LiveBuilderToolModes(Func<int> readCurrent, Action<int> writeCurrent, params LiveBuilderToolModeLabel[] labels)
        {
            this.readCurrent = readCurrent;
            this.writeCurrent = writeCurrent;
            this.labels = labels;
        }


        public IReadOnlyList<LiveBuilderToolModeLabel> Labels => labels;

        public int Count => labels.Length;

        // Режим сменился — окна перерисовываются: строка инструмента и подсказка в сцене следом.
        public int CurrentIndex
        {
            get => Mathf.Clamp(readCurrent(), 0, labels.Length - 1);
            set
            {
                var index = Mathf.Clamp(value, 0, labels.Length - 1);

                if (index == CurrentIndex)
                    return;

                writeCurrent(index);
                InternalEditorUtility.RepaintAllViews();
            }
        }

        public LiveBuilderToolModeLabel Current => labels[CurrentIndex];

        // Значки есть у каждого режима — переключатель значками, иначе словами.
        public bool HasIcons => Array.TrueForAll(labels, label => label.Icon.HasValue);


        // ── Смена ────────────────────────────────────────────────────────

        public void SelectNext()
        {
            CurrentIndex = (CurrentIndex + 1) % labels.Length;
        }


        public void SelectPrevious()
        {
            CurrentIndex = (CurrentIndex + labels.Length - 1) % labels.Length;
        }
    }


    /// <summary>Как режим инструмента выглядит на кнопке: название, значок (может не быть) и всплывающая подсказка.</summary>
    public readonly struct LiveBuilderToolModeLabel
    {
        public LiveBuilderToolModeLabel(string name, LiveBuilderOptionIcon? icon, string tooltip)
        {
            Name = name;
            Icon = icon;
            Tooltip = tooltip;
        }


        public string Name { get; }

        public LiveBuilderOptionIcon? Icon { get; }

        public string Tooltip { get; }
    }
}
