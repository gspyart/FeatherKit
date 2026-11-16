using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.LevelNotes
{
    /// <summary>
    /// Заметка на уровне, привязанная к месту: флажок в окне сцены и текст — для автора и для агентов в редакторе.
    /// Хранит текст (первая строка — название над флажком), тип, отметку «сделано» и кто оставил.
    /// В игре ничего не делает; объект с тегом EditorOnly в сборку не попадает. Рисует флажок редактор.
    /// </summary>
    [FDescription("Заметка на уровне. Поставить — мышь над местом в окне сцены и N, все заметки — FeatherKit → Level Notes. " +
                  "Первая строка текста — название над флажком.")]
    [AddComponentMenu("FeatherKit/Level Design/Level Note")]
    [DisallowMultipleComponent]
    public class LevelNote : MonoBehaviour
    {
        [Tooltip("Текст заметки. Первая строка — название над флажком")]
        [TextArea(3, 12)]
        [SerializeField] private string text = "";

        [Tooltip("Тип — только цвет флажка")]
        [SerializeField] private LevelNoteKind kind;

        [Tooltip("Сделано — флажок рисуется бледным, в списке заметок можно скрыть")]
        [SerializeField] private bool isDone;

        [Tooltip("Кто оставил: имя человека или агента")]
        [SerializeField] private string author = "";


        public string Text
        {
            get => text;
            set => text = value ?? "";
        }

        public LevelNoteKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public bool IsDone
        {
            get => isDone;
            set => isDone = value;
        }

        public string Author
        {
            get => author;
            set => author = value ?? "";
        }

        public string FirstLine
        {
            get
            {
                var trimmed = text.Trim();
                var lineEnd = trimmed.IndexOf('\n');
                return (lineEnd < 0 ? trimmed : trimmed.Substring(0, lineEnd)).Trim();
            }
        }
    }
}
