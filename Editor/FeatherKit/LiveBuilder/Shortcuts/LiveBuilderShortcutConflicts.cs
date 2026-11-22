using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Клавиши Live Builder, которые в активном профиле совпали с ярлыками Unity в окне сцены. Спор только там, где контексты Live Builder
    /// не удалось поставить приоритетными (<see cref="LiveBuilderPriorityShortcutContexts"/>): тогда Unity спрашивает окном, что выполнить.
    /// - находит совпадения; при настоящем споре раз за сеанс пишет о них в консоль
    /// - меню Resolve Shortcut Conflicts: по нажатию человека снимает привязки этих ярлыков Unity в его профиле
    /// </summary>
    public static class LiveBuilderShortcutConflicts
    {
        private const string MenuPath = "Tools/FeatherKit/Live Builder/Resolve Shortcut Conflicts";

        // Профиль Unity по умолчанию менять нельзя — привязки снимаются в этом, его создаём сами.
        private const string OwnProfileId = "Live Builder";

        private const string WarnedSessionKey = "FeatherKit.LiveBuilder.ShortcutConflictsWarned";

        // Ярлыки Unity, которые работают в окне сцены одновременно с Live Builder: контексты окна сцены, его области, видимости и оверлеев.
        // Глобальные (X — Toggle Pivot Orientation) не спорят: свой контекст Unity ставит выше глобального.
        private static readonly string[] UnitySceneViewShortcutIds =
        {
            "Scene View/Toggle 2D Mode",
            "Overlays/Show Tool Settings",
            "3D Viewport/Fly Mode Forward (Alt)",
            "3D Viewport/Fly Mode Backward (Alt)",
            "3D Viewport/Fly Mode Left (Alt)",
            "3D Viewport/Fly Mode Right (Alt)",
            "Grid and Snap/Toggle Snapping",
            "Scene View/Vertex Snapping",
            "Scene View/Toggle Vertex Snapping",
            "Scene Visibility/Toggle Selection And Descendants Visibility",
            "Scene Visibility/Toggle Isolation On Selection And Descendants",
            "Scene Picking/Toggle Picking On Selection And Descendants",
            "Overlays/Show Overlay Menu",
            "Overlays/Toggle All Overlays",
            "Tools/Cycle Tool Modes"
        };


        // ── Поиск ────────────────────────────────────────────────────────

        // Пары «ярлык Unity — ярлык Live Builder» с одной клавишей в активном профиле. Ярлыка нет в этой версии Unity — пропускаем.
        public static List<LiveBuilderShortcutConflict> Find()
        {
            var manager = ShortcutManager.instance;
            var available = new HashSet<string>(manager.GetAvailableShortcutIds());
            var conflicts = new List<LiveBuilderShortcutConflict>();

            foreach (var liveBuilderId in LiveBuilderShortcutIds.AllIds())
            {
                if (!available.Contains(liveBuilderId))
                    continue;

                var binding = manager.GetShortcutBinding(liveBuilderId);

                if (binding.Equals(ShortcutBinding.empty))
                    continue;

                foreach (var unityId in UnitySceneViewShortcutIds)
                    if (available.Contains(unityId) && manager.GetShortcutBinding(unityId).Equals(binding))
                        conflicts.Add(new LiveBuilderShortcutConflict(unityId, liveBuilderId, binding));
            }

            return conflicts;
        }


        // Раз за сеанс редактора: после каждой перекомпиляции одно и то же в консоли не нужно.
        public static void WarnOncePerSession()
        {
            if (LiveBuilderPriorityShortcutContexts.IsAvailable || SessionState.GetBool(WarnedSessionKey, false))
                return;

            SessionState.SetBool(WarnedSessionKey, true);
            var conflicts = Find();

            if (conflicts.Count == 0)
                return;

            var keys = string.Join(", ", conflicts.Select(conflict => conflict.Binding.ToString()).Distinct());
            Debug.LogWarning($"Live Builder: keys {keys} are also Unity shortcuts in the Scene view, so Unity asks which one to run. " +
                             $"To give them to Live Builder: {MenuPath}.");
        }


        // ── Снять привязки ───────────────────────────────────────────────

        // Только в активном профиле человека; профиль Unity по умолчанию только для чтения — тогда сначала свой профиль, и он становится активным.
        // Возвращает имя профиля, где сняты привязки.
        public static string UnbindUnityShortcuts(IEnumerable<string> unityIds)
        {
            var manager = ShortcutManager.instance;

            if (manager.IsProfileReadOnly(manager.activeProfileId))
            {
                if (!manager.GetAvailableProfileIds().Contains(OwnProfileId))
                    manager.CreateProfile(OwnProfileId);

                manager.activeProfileId = OwnProfileId;
            }

            foreach (var unityId in unityIds)
                manager.RebindShortcut(unityId, ShortcutBinding.empty);

            return manager.activeProfileId;
        }


        [MenuItem(MenuPath)]
        private static void ResolveFromMenu()
        {
            if (LiveBuilderPriorityShortcutContexts.IsAvailable)
            {
                EditorUtility.DisplayDialog("Live Builder Shortcuts",
                    "No conflicts: while Live Builder is on, its keys take priority over Unity's Scene view shortcuts. Nothing to unbind.", "OK");
                return;
            }

            var conflicts = Find();

            if (conflicts.Count == 0)
            {
                EditorUtility.DisplayDialog("Live Builder Shortcuts", "No conflicts: no Unity Scene view shortcut uses a Live Builder key.", "OK");
                return;
            }

            var lines = conflicts.Select(conflict => $"{conflict.Binding}  —  {conflict.UnityShortcutId}  (Live Builder: {conflict.LiveBuilderShortcutId})");
            var manager = ShortcutManager.instance;
            var profileNote = manager.IsProfileReadOnly(manager.activeProfileId)
                ? $"The Unity profile \"{manager.activeProfileId}\" can't be changed, so a profile \"{OwnProfileId}\" will be created and made active."
                : $"Changes go to your active profile \"{manager.activeProfileId}\".";

            var message = "These Unity shortcuts will lose their keys:\n\n" + string.Join("\n", lines.Distinct()) + "\n\n" + profileNote +
                          " Profiles are shared by all your Unity projects. To undo: Edit → Shortcuts → Reset, or switch the profile back.";

            if (!EditorUtility.DisplayDialog("Resolve Shortcut Conflicts", message, "Unbind Unity Shortcuts", "Cancel"))
                return;

            var profile = UnbindUnityShortcuts(conflicts.Select(conflict => conflict.UnityShortcutId).Distinct().ToList());
            Debug.Log($"Live Builder: unbound {conflicts.Count} Unity shortcut key(s) in the profile \"{profile}\".");
        }
    }


    // Ярлык Unity и ярлык Live Builder на одной клавише.
    public readonly struct LiveBuilderShortcutConflict
    {
        public LiveBuilderShortcutConflict(string unityShortcutId, string liveBuilderShortcutId, ShortcutBinding binding)
        {
            UnityShortcutId = unityShortcutId;
            LiveBuilderShortcutId = liveBuilderShortcutId;
            Binding = binding;
        }


        public string UnityShortcutId { get; }

        public string LiveBuilderShortcutId { get; }

        public ShortcutBinding Binding { get; }
    }
}
