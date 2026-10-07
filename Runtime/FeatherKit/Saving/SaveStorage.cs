using System;
using System.IO;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Saving
{
    /// <summary>
    /// Запись и чтение сохранений: JSON в файл рядом с игрой.
    ///
    /// Намеренно только хранилище. Что именно игра сохраняет, когда и как переносит старые
    /// сохранения на новую версию — решает сама игра: у платформера и у выживача это
    /// совершенно разные вещи, и общего кода тут не выходит.
    ///
    /// PlayerPrefs не используем: он не переживает переустановку на части платформ и
    /// не годится ни для чего, кроме пары настроек.
    /// </summary>
    public static class SaveStorage
    {
        private const string Extension = ".json";
        private const string TempExtension = ".tmp";


        /// <summary>Где лежат файлы — пригодится, чтобы открыть папку при отладке.</summary>
        public static string Folder => Application.persistentDataPath;


        public static bool Exists(string slot)
        {
            return IsValidSlot(slot) && (File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + TempExtension));
        }

        /// <summary>
        /// Сохранить. Пишем во временный файл и только потом подменяем настоящий: если игру
        /// закроют или телефон сядет посреди записи, старое сохранение останется целым.
        /// Прямая запись в этот момент оставила бы обрезанный файл — то есть ничего.
        /// </summary>
        public static bool Save<T>(string slot, T data) where T : class
        {
            if (data == null || !IsValidSlot(slot))
                return false;

            var path = PathFor(slot);
            var temp = path + TempExtension;

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);

                File.WriteAllText(temp, JsonUtility.ToJson(data, true));

                // Replace, а не Delete + Move: между удалением и переносом файла нет ни мгновения,
                // когда сохранения не существует. Нет старого файла — переносим просто так.
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception exception)
            {
                FLog.Error(("Не сохранилось: ", FLogColor.Bad), (slot, FLogColor.Value),
                    (" — " + exception.Message, FLogColor.Muted));

                return false;
            }
        }

        /// <summary>Прочитать. Файла нет или он битый — вернётся null, решает вызывающий.</summary>
        public static T Load<T>(string slot) where T : class
        {
            if (!IsValidSlot(slot))
                return null;

            var path = PathFor(slot);

            // Временный файл дописан целиком, а подменить настоящий не успели — он и есть
            // самое свежее сохранение.
            if (!File.Exists(path))
                path += TempExtension;

            if (!File.Exists(path))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                FLog.Error(("Сохранение битое: ", FLogColor.Bad), (slot, FLogColor.Value),
                    (" — " + exception.Message, FLogColor.Muted));

                return null;
            }
        }

        public static bool Delete(string slot)
        {
            if (!IsValidSlot(slot))
                return false;

            var path = PathFor(slot);

            if (!File.Exists(path) && !File.Exists(path + TempExtension))
                return false;

            try
            {
                File.Delete(path);
                File.Delete(path + TempExtension);

                return true;
            }
            catch (Exception exception)
            {
                FLog.Error(("Не удалилось: ", FLogColor.Bad), (slot, FLogColor.Value),
                    (" — " + exception.Message, FLogColor.Muted));

                return false;
            }
        }

        private static string PathFor(string slot)
        {
            return Path.Combine(Application.persistentDataPath, slot + Extension);
        }

        // Слот — имя файла, а не путь: «../» или слэш увели бы запись из папки сохранений.
        private static bool IsValidSlot(string slot)
        {
            return !string.IsNullOrWhiteSpace(slot)
                   && slot == Path.GetFileName(slot)
                   && slot.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
