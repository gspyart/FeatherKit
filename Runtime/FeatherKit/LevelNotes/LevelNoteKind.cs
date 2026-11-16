using UnityEngine;

namespace FeatherKit.LevelNotes
{
    /// <summary>Тип заметки на уровне. Влияет только на цвет флажка.</summary>
    public enum LevelNoteKind
    {
        [InspectorName("Идея")] Idea,
        [InspectorName("Задача")] Task,
        [InspectorName("Вопрос")] Question,
        [InspectorName("Для агента")] ForAgent
    }
}
