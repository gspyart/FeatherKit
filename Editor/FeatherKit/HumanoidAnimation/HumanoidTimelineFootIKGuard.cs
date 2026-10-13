using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Выключает Foot IK у дорожек Timeline, на которых стоит риг. Timeline включает его
    /// по умолчанию, а у клипа с ручками нет целей для стоп — и Unity тянет стопы в ноль
    /// поверх IK рига. В предпросмотре это выглядит как разъехавшиеся ноги, хотя запечётся всё верно.
    ///
    /// Раз в секунду проверяет таймлайны открытых сцен. Ссылку на пакет Timeline не берёт:
    /// нужные галки меняет через сериализованные поля дорожек и клипов.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidTimelineFootIKGuard
    {
        private const double CheckInterval = 1.0;
        private const string TrackFootIKProperty = "m_InfiniteClipApplyFootIK";
        private const string TrackClipsProperty = "m_Clips";
        private const string TrackChildrenProperty = "m_Children";
        private const string ClipAssetProperty = "m_Asset";
        private const string ClipFootIKProperty = "m_ApplyFootIK";

        private static double nextCheckTime;

        static HumanoidTimelineFootIKGuard()
        {
            EditorApplication.update += CheckPeriodically;
        }


        // ── Проверка ──────────────────────────────

        private static void CheckPeriodically()
        {
            if (EditorApplication.timeSinceStartup < nextCheckTime || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            nextCheckTime = EditorApplication.timeSinceStartup + CheckInterval;

            var animators = FindRigAnimators();

            if (animators.Count == 0)
                return;

            foreach (var director in Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Exclude))
            {
                if (director.playableAsset != null && DisableFootIKOnRigTracks(director, animators))
                    director.RebuildGraph();
            }
        }


        private static HashSet<Animator> FindRigAnimators()
        {
            var animators = new HashSet<Animator>();

            foreach (var rig in Object.FindObjectsByType<HumanoidAnimationRig>(FindObjectsInactive.Exclude))
                animators.Add(rig.Animator);

            return animators;
        }


        // Дорожка привязана к ригу — выключаем Foot IK у неё самой (записанный клип)
        // и у каждого клипа на ней. Вернёт true, если что-то поменяли.
        private static bool DisableFootIKOnRigTracks(PlayableDirector director, HashSet<Animator> rigAnimators)
        {
            var changed = false;

            foreach (var output in director.playableAsset.outputs)
            {
                var track = output.sourceObject;

                if (track == null || !(director.GetGenericBinding(track) is Animator animator) || !rigAnimators.Contains(animator))
                    continue;

                changed |= DisableFootIKOnTrack(track);
            }

            if (changed)
                Debug.Log($"Timeline «{director.playableAsset.name}»: у дорожек рига выключен Foot IK — иначе стопы рига уезжают в предпросмотре.", director);

            return changed;
        }


        // Дорожка, её клипы и её дорожки-переопределения: они играют на тот же аниматор.
        private static bool DisableFootIKOnTrack(Object track)
        {
            var trackObject = new SerializedObject(track);

            var changed = TurnOff(trackObject, TrackFootIKProperty);
            changed |= DisableFootIKOnClips(trackObject);

            var children = trackObject.FindProperty(TrackChildrenProperty);

            for (var i = 0; children != null && children.isArray && i < children.arraySize; i++)
            {
                var child = children.GetArrayElementAtIndex(i).objectReferenceValue;

                if (child != null)
                    changed |= DisableFootIKOnTrack(child);
            }

            return changed;
        }


        private static bool DisableFootIKOnClips(SerializedObject trackObject)
        {
            var clips = trackObject.FindProperty(TrackClipsProperty);

            if (clips == null || !clips.isArray)
                return false;

            var changed = false;

            for (var i = 0; i < clips.arraySize; i++)
            {
                var asset = clips.GetArrayElementAtIndex(i).FindPropertyRelative(ClipAssetProperty)?.objectReferenceValue;

                if (asset != null)
                    changed |= TurnOff(new SerializedObject(asset), ClipFootIKProperty);
            }

            return changed;
        }


        private static bool TurnOff(SerializedObject serializedObject, string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);

            if (property == null || property.propertyType != SerializedPropertyType.Boolean || !property.boolValue)
                return false;

            property.boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
