using System.Collections.Generic;
using System.Linq;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Группы, в которые клали объекты последними, — для верха меню «Move to Group».
    /// Помнятся до закрытия редактора: переживают перекомпиляцию, но не перезапуск Unity.
    /// </summary>
    public static class ObjectGroupRecentList
    {
        private const string StorageKey = "FeatherKit.ObjectGroups.Recent";
        private const char AddressSeparator = '|';
        private const int MaxCount = 5;


        // Свежая — первой; повтор поднимается наверх, а не дублируется.
        public static void Remember(ObjectGroup group)
        {
            var address = GlobalObjectId.GetGlobalObjectIdSlow(group).ToString();
            var addresses = LoadAddresses();

            addresses.Remove(address);
            addresses.Insert(0, address);

            SessionState.SetString(StorageKey, string.Join(AddressSeparator, addresses.Take(MaxCount)));
        }


        // Живые группы этой сцены: удалённые и группы других сцен пропускаются.
        public static List<ObjectGroup> GroupsIn(Scene scene)
        {
            return LoadAddresses()
                .Select(FindByAddress)
                .Where(group => group != null && group.gameObject.scene == scene)
                .ToList();
        }


        // ── Хранение ─────────────────────────────────────

        private static List<string> LoadAddresses()
        {
            var stored = SessionState.GetString(StorageKey, string.Empty);
            return stored.Split(AddressSeparator).Where(address => address.Length > 0).ToList();
        }

        private static ObjectGroup FindByAddress(string address)
        {
            return GlobalObjectId.TryParse(address, out var id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as ObjectGroup
                : null;
        }
    }
}
