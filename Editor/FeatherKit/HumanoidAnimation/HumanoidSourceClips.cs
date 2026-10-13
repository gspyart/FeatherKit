using System.Collections.Generic;
using System.IO;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Какие клипы анимируют ручки рига и куда класть их Humanoid-версию.
    ///
    /// Отдельного списка нет: создал клип в окне Animation или записал в Timeline —
    /// тула узнаёт его сама. Чей клип, решает привязка: клип в контроллере рига или на
    /// дорожке Timeline, привязанной к ригу. Имена ручек у всех ригов одинаковые, поэтому
    /// по одним кривым владельца угадываем, только когда подходит ровно один риг.
    /// </summary>
    internal static class HumanoidSourceClips
    {
        public const string HumanoidClipSuffix = "_Humanoid";

        private const string ClipExtension = ".anim";
        private const string TimelineExtension = ".playable";

        private const string TrackInfiniteClipProperty = "m_InfiniteClip";
        private const string TrackClipsProperty = "m_Clips";
        private const string TrackChildrenProperty = "m_Children";
        private const string ClipAssetProperty = "m_Asset";
        private const string PlayableAssetClipProperty = "m_Clip";


        // ── Узнать исходник ──────────────────────────────

        /// <summary>Есть ли в клипе кривые ручек этого рига.</summary>
        public static bool IsSourceFor(AnimationClip clip, HumanoidAnimationRig rig)
        {
            if (clip == null || rig == null || rig.ControlsRoot == null || clip.humanMotion)
                return false;

            var rootPath = HumanoidAnimationRig.ControlsRootName;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var isControlPath = binding.path == rootPath || binding.path.StartsWith(rootPath + "/");

                if (isControlPath && AnimationUtility.GetAnimatedObject(rig.gameObject, binding) != null)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Риг, которому принадлежит клип. Сначала по привязке — контроллер или дорожка Timeline,
        /// потом по кривым, если подходит ровно один риг. Не понять — null: лучше не запечь,
        /// чем запечь на чужом персонаже.
        /// </summary>
        public static HumanoidAnimationRig FindOwner(AnimationClip clip, IReadOnlyList<HumanoidAnimationRig> rigs)
        {
            foreach (var rig in rigs)
            {
                if (IsBoundTo(clip, rig) && IsSourceFor(clip, rig))
                    return rig;
            }

            HumanoidAnimationRig onlyMatch = null;

            foreach (var rig in rigs)
            {
                if (!IsSourceFor(clip, rig))
                    continue;

                if (onlyMatch != null)
                    return null;

                onlyMatch = rig;
            }

            return onlyMatch;
        }


        private static bool IsBoundTo(AnimationClip clip, HumanoidAnimationRig rig)
        {
            var controller = rig.Animator.runtimeAnimatorController;

            if (controller != null && System.Array.IndexOf(controller.animationClips, clip) >= 0)
                return true;

            return CollectTimelineClips(rig.Animator).Contains(clip);
        }


        // ── Собрать исходники ──────────────────────────────

        /// <summary>
        /// Клипы рига с кривыми ручек: из его контроллера и с дорожек Timeline, привязанных к нему.
        /// </summary>
        public static List<AnimationClip> CollectFor(HumanoidAnimationRig rig)
        {
            var found = new List<AnimationClip>();

            var controller = rig.Animator.runtimeAnimatorController;

            if (controller != null)
            {
                foreach (var clip in controller.animationClips)
                    AddIfSource(found, clip, rig);
            }

            foreach (var clip in CollectTimelineClips(rig.Animator))
                AddIfSource(found, clip, rig);

            return found;
        }

        private static void AddIfSource(List<AnimationClip> found, AnimationClip clip, HumanoidAnimationRig rig)
        {
            if (!found.Contains(clip) && IsSourceFor(clip, rig))
                found.Add(clip);
        }


        // Клипы с дорожек Timeline, привязанных к аниматору: записанный клип самой дорожки
        // и клипы на ней, включая дорожки-переопределения. Ссылку на пакет Timeline не берём,
        // чтобы модуль от него не зависел: читаем сериализованные поля дорожек.
        private static List<AnimationClip> CollectTimelineClips(Animator animator)
        {
            var clips = new List<AnimationClip>();

            foreach (var director in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include))
            {
                if (director.playableAsset == null)
                    continue;

                foreach (var output in director.playableAsset.outputs)
                {
                    if (output.sourceObject != null && director.GetGenericBinding(output.sourceObject) as Animator == animator)
                        AddTrackClips(clips, output.sourceObject);
                }
            }

            return clips;
        }

        private static void AddTrackClips(List<AnimationClip> clips, Object track)
        {
            var trackObject = new SerializedObject(track);

            if (trackObject.FindProperty(TrackInfiniteClipProperty)?.objectReferenceValue is AnimationClip infiniteClip && !clips.Contains(infiniteClip))
                clips.Add(infiniteClip);

            var trackClips = trackObject.FindProperty(TrackClipsProperty);

            for (var i = 0; trackClips != null && trackClips.isArray && i < trackClips.arraySize; i++)
            {
                var asset = trackClips.GetArrayElementAtIndex(i).FindPropertyRelative(ClipAssetProperty)?.objectReferenceValue;

                if (asset != null && new SerializedObject(asset).FindProperty(PlayableAssetClipProperty)?.objectReferenceValue is AnimationClip clip && !clips.Contains(clip))
                    clips.Add(clip);
            }

            // Дорожки-переопределения играют на тот же аниматор, что и родитель.
            var children = trackObject.FindProperty(TrackChildrenProperty);

            for (var i = 0; children != null && children.isArray && i < children.arraySize; i++)
            {
                var child = children.GetArrayElementAtIndex(i).objectReferenceValue;

                if (child != null)
                    AddTrackClips(clips, child);
            }
        }


        /// <summary>
        /// Лежит ли клип внутри чужого файла: в таймлайне, модели или файле анимации.
        /// AssetDatabase.IsSubAsset спрятанные вложения (HideInHierarchy) не видит — поэтому своя проверка.
        /// </summary>
        public static bool IsNestedClip(AnimationClip clip) =>
            clip != null && EditorUtility.IsPersistent(clip) && !AssetDatabase.IsMainAsset(clip);


        /// <summary>Все клипы файла: сам .anim или клипы внутри таймлайна.</summary>
        public static List<AnimationClip> LoadClipsAt(string assetPath)
        {
            var clips = new List<AnimationClip>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is AnimationClip clip)
                    clips.Add(clip);
            }

            return clips;
        }


        /// <summary>Может ли в этом файле лежать исходник: клип или таймлайн.</summary>
        public static bool CanContainSourceClips(string assetPath) =>
            assetPath.EndsWith(ClipExtension) || assetPath.EndsWith(TimelineExtension);


        // ── Куда класть результат ──────────────────────────────

        /// <summary>
        /// Путь Humanoid-версии. Исходник спрятан в файле анимации — сам этот файл.
        /// Отдельный исходник — рядом с ним: «Walk.anim» → «Walk_Humanoid.anim».
        /// Клип внутри таймлайна → «Таймлайн_Клип_Humanoid.anim» рядом с файлом таймлайна.
        /// </summary>
        public static string GetHumanoidClipPath(AnimationClip sourceClip)
        {
            var sourcePath = AssetDatabase.GetAssetPath(sourceClip);

            if (HumanoidAnimationFiles.IsEmbeddedSource(sourceClip))
                return sourcePath;

            var folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var mainAssetName = Path.GetFileNameWithoutExtension(sourcePath);

            var baseName = IsNestedClip(sourceClip)
                ? mainAssetName + "_" + sourceClip.name
                : mainAssetName;

            return folder + "/" + RemoveInvalidFileNameChars(baseName) + HumanoidClipSuffix + ClipExtension;
        }

        private static string RemoveInvalidFileNameChars(string fileName)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');

            return fileName;
        }


        /// <summary>
        /// Humanoid-версия клипа — главный клип её файла. Отдельный «_Humanoid» ещё не пекли — null.
        /// У файла анимации игровой клип есть всегда, но до первого запекания он пустой.
        /// </summary>
        public static AnimationClip LoadHumanoidClip(AnimationClip sourceClip) =>
            AssetDatabase.LoadMainAssetAtPath(GetHumanoidClipPath(sourceClip)) as AnimationClip;
    }
}
