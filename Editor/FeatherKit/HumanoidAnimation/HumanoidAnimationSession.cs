using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Сеанс анимации: какой риг и какой клип сейчас анимируют. Начать — окно Animation
    /// показывает клип рига, предпросмотр и запись включены: двигаешь ручку — ставится ключ.
    /// Пока сеанс идёт, вернулся в риг после клика мимо — предпросмотр и запись включатся сами.
    /// «Готово» запекает игровой клип и всё выключает.
    ///
    /// Сеанс помнится до закрытия редактора и переживает перекомпиляцию, в сцену не пишется.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidAnimationSession
    {
        private const string RigKey = "FeatherKit.HumanoidAnimation.SessionRig";
        private const string ClipKey = "FeatherKit.HumanoidAnimation.SessionClip";

        private static HumanoidAnimationRig activeRig;
        private static AnimationClip activeClip;
        private static bool isLoaded;

        static HumanoidAnimationSession()
        {
            Selection.selectionChanged += ResumeOnRigSelection;

            // Сцена перезагрузилась — объекты в ней новые, риг ищем заново по сохранённому адресу.
            EditorSceneManager.sceneOpened += (scene, mode) => isLoaded = false;
            EditorApplication.playModeStateChanged += change => isLoaded = false;
        }


        // ── Состояние ──────────────────────────────

        public static HumanoidAnimationRig ActiveRig
        {
            get
            {
                Load();
                return activeRig;
            }
        }

        public static AnimationClip ActiveClip
        {
            get
            {
                Load();
                return activeClip;
            }
        }


        /// <summary>Идёт ли сеанс на этом риге.</summary>
        public static bool IsActiveOn(HumanoidAnimationRig rig) => rig != null && ActiveRig == rig && ActiveClip != null;


        // ── Начать и закончить ──────────────────────────────

        /// <summary>
        /// Начать анимировать клип на риге: подключить клип к ригу, открыть в окне Animation,
        /// включить предпросмотр и запись. Шёл сеанс с другим клипом — тот запекается.
        /// </summary>
        public static bool Start(HumanoidAnimationRig rig, AnimationClip clip)
        {
            if (!HumanoidAnimationRigActions.CanChangeRigNow() || !HumanoidRigClipLibrary.TryAddToRig(rig, clip))
                return false;

            var previousRig = ActiveRig;
            var previousClip = ActiveClip;

            if (previousRig != null && previousClip != null && previousClip != clip)
                HumanoidClipAutoBaker.BakeIfOutdated(previousRig, previousClip);

            Remember(rig, clip);
            HumanoidAnimationWindowRemote.ShowClip(rig, clip, record: true);
            return true;
        }


        /// <summary>Закончить: выключить запись и предпросмотр, запечь игровой клип, если он поменялся.</summary>
        public static void Finish()
        {
            var rig = ActiveRig;
            var clip = ActiveClip;

            HumanoidAnimationWindowRemote.StopAll();
            Remember(null, null);

            if (rig != null && clip != null)
                HumanoidClipAutoBaker.BakeIfOutdated(rig, clip);
        }


        /// <summary>Вернуть предпросмотр и запись, если окно их выключило.</summary>
        public static void Resume()
        {
            var rig = ActiveRig;

            if (rig != null && ActiveClip != null)
                HumanoidAnimationWindowRemote.ShowClip(rig, ActiveClip, record: true);
        }


        // ── Возврат в сеанс ──────────────────────────────

        // Окно Animation выключает предпросмотр, когда выделяешь что-то вне рига. Вернулся в риг —
        // включаем обратно. Окно меняет клип и предпросмотр на следующем тике после выделения —
        // смотрим уже после него.
        private static void ResumeOnRigSelection()
        {
            var rig = HumanoidAnimationRigActions.FindSelectedRig();

            if (!IsActiveOn(rig) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.delayCall += () => ResumeIfStillSelected(rig);
        }


        // Сменил клип в самом окне Animation — сеанс переходит на него. Идёт предпросмотр
        // Timeline — не мешаем ему.
        private static void ResumeIfStillSelected(HumanoidAnimationRig rig)
        {
            if (!IsActiveOn(rig) || !rig.Contains(Selection.activeTransform))
                return;

            var openClip = HumanoidAnimationWindowRemote.OpenClip;

            if (openClip != ActiveClip && HumanoidAnimationWindowRemote.IsPreviewing(openClip) && HumanoidRigClipLibrary.CollectClips(rig).Contains(openClip))
            {
                Remember(rig, openClip);
                return;
            }

            if (!HumanoidAnimationWindowRemote.IsAnyPreviewRunning)
                HumanoidAnimationWindowRemote.ShowClip(rig, ActiveClip, record: true);
        }


        // ── Память сеанса ──────────────────────────────

        private static void Remember(HumanoidAnimationRig rig, AnimationClip clip)
        {
            activeRig = rig;
            activeClip = clip;
            isLoaded = true;

            SessionState.SetString(RigKey, ObjectAddress(rig));
            SessionState.SetString(ClipKey, ObjectAddress(clip));
        }


        private static void Load()
        {
            if (isLoaded)
                return;

            isLoaded = true;
            activeRig = FindByAddress(SessionState.GetString(RigKey, string.Empty)) as HumanoidAnimationRig;
            activeClip = FindByAddress(SessionState.GetString(ClipKey, string.Empty)) as AnimationClip;
        }


        private static string ObjectAddress(Object target) =>
            target != null ? GlobalObjectId.GetGlobalObjectIdSlow(target).ToString() : string.Empty;

        private static Object FindByAddress(string address) =>
            GlobalObjectId.TryParse(address, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) : null;
    }
}
