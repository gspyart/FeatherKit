using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Пересобирает игровые Humanoid-клипы сам, чтобы человек не думал о запекании:
    /// когда сохраняешь проект (Ctrl+S), перед запуском игры и когда заканчиваешь клип
    /// или переходишь к другому. Пересобирает только клипы, изменённые с прошлого раза.
    ///
    /// После сохранения запекает не сразу, а следующим тиком редактора: внутри сохранения
    /// создавать и писать ассеты нельзя.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class HumanoidClipAutoBaker : AssetModificationProcessor
    {
        private const string ProgressTitle = "Humanoid-клипы";

        private static readonly HashSet<string> SavedPaths = new HashSet<string>();
        private static bool isBakeScheduled;

        static HumanoidClipAutoBaker()
        {
            EditorApplication.playModeStateChanged += BakeBeforePlay;
        }


        // ── Сохранение ──────────────────────────────

        // AssetModificationProcessor
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                if (HumanoidSourceClips.CanContainSourceClips(path))
                    SavedPaths.Add(path);
            }

            if (SavedPaths.Count > 0 && !isBakeScheduled)
            {
                isBakeScheduled = true;
                EditorApplication.delayCall += BakeSavedClips;
            }

            return paths;
        }


        private static void BakeSavedClips()
        {
            isBakeScheduled = false;

            var paths = new List<string>(SavedPaths);
            SavedPaths.Clear();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var rigs = new List<HumanoidAnimationRig>(Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude));

            if (rigs.Count > 0)
                BakeClips(CollectOutdatedSavedClips(paths, rigs));
        }


        // Чей клип, решаем среди всех ригов, а не только тех, что пересобирают сами:
        // иначе клип рига с выключенной пересборкой приписался бы соседнему.
        private static List<(HumanoidAnimationRig rig, AnimationClip clip)> CollectOutdatedSavedClips(List<string> paths, List<HumanoidAnimationRig> rigs)
        {
            var pending = new List<(HumanoidAnimationRig, AnimationClip)>();

            foreach (var path in paths)
            {
                foreach (var clip in HumanoidSourceClips.LoadClipsAt(path))
                {
                    var owner = HumanoidSourceClips.FindOwner(clip, rigs);

                    if (owner != null && owner.BakeOnSave && !HumanoidClipBaker.IsUpToDate(owner, clip))
                        pending.Add((owner, clip));
                }
            }

            return pending;
        }


        // ── Перед запуском игры ──────────────────────────────

        // Нажал Play — в игре сразу свежая анимация. Ассеты ещё можно писать: игра не началась.
        private static void BakeBeforePlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;

            var pending = new List<(HumanoidAnimationRig, AnimationClip)>();

            foreach (var rig in Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude))
            {
                if (!rig.BakeOnSave)
                    continue;

                foreach (var clip in HumanoidSourceClips.CollectFor(rig))
                {
                    if (!HumanoidClipBaker.IsUpToDate(rig, clip))
                        pending.Add((rig, clip));
                }
            }

            BakeClips(pending);
        }


        // ── Один клип ──────────────────────────────

        /// <summary>
        /// Запечь клип, если он менялся с прошлого запекания. Без вопросов и сообщений:
        /// для «Готово» и перехода к другому клипу. Пустой клип без ключей пропускает.
        /// </summary>
        public static void BakeIfOutdated(HumanoidAnimationRig rig, AnimationClip clip)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !HumanoidSourceClips.IsSourceFor(clip, rig) || HumanoidClipBaker.IsUpToDate(rig, clip))
                return;

            BakeClips(new List<(HumanoidAnimationRig, AnimationClip)> { (rig, clip) });
        }


        // ── Запекание ──────────────────────────────

        private static void BakeClips(List<(HumanoidAnimationRig rig, AnimationClip clip)> pending)
        {
            if (pending.Count == 0)
                return;

            var bakedNames = new List<string>();

            try
            {
                for (var i = 0; i < pending.Count; i++)
                {
                    var (rig, clip) = pending[i];
                    var displayName = HumanoidAnimationFiles.DisplayName(clip);
                    EditorUtility.DisplayProgressBar(ProgressTitle, "Пересобираю " + displayName, (float)i / pending.Count);

                    try
                    {
                        HumanoidClipBaker.Bake(rig, clip);
                        bakedNames.Add(displayName);
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception);
                        Debug.LogError($"Не получилось запечь «{displayName}». Подробности — в ошибке выше.", clip);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (bakedNames.Count == 0)
                return;

            AssetDatabase.SaveAssets();
            Debug.Log("Humanoid-клипы пересобраны: " + string.Join(", ", bakedNames));
        }
    }
}
