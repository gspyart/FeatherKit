using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Пересобирает Humanoid-клипы, когда сохраняешь проект (Ctrl+S). Ловит сохранение
    /// клипов и таймлайнов, находит среди них исходники ригов из открытых сцен и запекает.
    ///
    /// Запекает не прямо в момент сохранения, а следующим тиком редактора: внутри
    /// сохранения создавать и писать ассеты нельзя.
    /// </summary>
    internal sealed class HumanoidClipAutoBaker : AssetModificationProcessor
    {
        private static readonly HashSet<string> SavedPaths = new HashSet<string>();
        private static bool isBakeScheduled;


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

            if (rigs.Count == 0)
                return;

            var pending = CollectOutdatedClips(paths, rigs);

            if (pending.Count == 0)
                return;

            var bakedNames = new List<string>();

            try
            {
                for (var i = 0; i < pending.Count; i++)
                {
                    var (rig, clip) = pending[i];
                    EditorUtility.DisplayProgressBar("Humanoid-клипы", "Пересобираю " + clip.name, (float)i / pending.Count);

                    try
                    {
                        HumanoidClipBaker.Bake(rig, clip);
                        bakedNames.Add(clip.name);
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception);
                        Debug.LogError($"Не получилось запечь «{clip.name}». Подробности — в ошибке выше.", clip);
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


        // Чей клип, решаем среди всех ригов, а не только тех, что пересобирают по Ctrl+S:
        // иначе клип рига с выключенной пересборкой приписался бы соседнему.
        // Клипы, которые не менялись с прошлого запекания, пропускаем.
        private static List<(HumanoidAnimationRig rig, AnimationClip clip)> CollectOutdatedClips(List<string> paths, List<HumanoidAnimationRig> rigs)
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
    }
}
