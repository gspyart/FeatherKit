using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Настройки заметок на уровне на проект: куда на сцене кладутся новые заметки.
    /// Общие на проект: лежат в ProjectSettings/FeatherKit и попадают в git; страница — <see cref="LevelNotesProjectSettingsPage"/>.
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/LevelNotes.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LevelNotesProjectSettings : ScriptableSingleton<LevelNotesProjectSettings>
    {
        [Header("Новые заметки")]
        [Tooltip("Группа на сцене для новых заметок, путь от корня: «Level/Debug/Notes». Каких групп пути нет — создаются. " +
                 "Поле пустое — заметка ложится в корень сцены.")]
        [SerializeField] private string newNotesGroup = "";


        public string NewNotesGroup => newNotesGroup.Trim('/', ' ');


        // Записать в ProjectSettings — зовёт страница настроек после правки.
        public void SaveChanges()
        {
            Save(true);
        }
    }
}
