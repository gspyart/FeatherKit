using System;
using System.Text;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Переводит клип с ручками в Humanoid-клип. Покадрово проигрывает исходник вместе с IK,
    /// снимает позу тела в «мышцах» аватара и пишет их в игровой клип: в сам файл анимации,
    /// если исходник спрятан в нём, или рядом с исходником в «_Humanoid».
    ///
    /// Проигрывает не на самом риге, а на копии в отдельной скрытой сцене: запекание не сбивает
    /// предпросмотр в Animation и Timeline. На копии ручки без ключей стоят в исходной позе —
    /// результат зависит только от клипа, а не от того, что сейчас открыто в предпросмотре.
    ///
    /// Готовый клип обновляется на месте — ссылки на него не рвутся, его настройки
    /// (корень, зацикливание, зеркало) и события остаются твоими. Перезаписывает только свои
    /// файлы: метит их в настройках импорта, заодно запоминая исходник, его версию и аватар,
    /// на котором пекли.
    /// </summary>
    internal static class HumanoidClipBaker
    {
        private const string GraphName = "Humanoid Clip Bake";
        private const string BakedClipMark = "FeatherKit.HumanoidAnimation.BakedClip";
        private const char MarkSeparator = '|';
        private const char SourceIdSeparator = '/';
        private const int MarkSourcePart = 1;
        private const int MarkFingerprintPart = 2;
        private const int MarkAvatarPart = 3;
        private const float MinimalFrameRate = 30f;
        private const string ConstraintWeightProperty = "m_Weight";

        // Последний кадр берём чуть раньше конца: у зацикленного клипа ровно на конце время
        // заворачивается в ноль, и последняя поза оказалась бы первой.
        private const float EndTimeMargin = 0.0001f;


        // ── Запекание ──────────────────────────────

        /// <summary>Запечь клип и вернуть его Humanoid-версию. Не получилось — исключение с объяснением.</summary>
        public static AnimationClip Bake(HumanoidAnimationRig rig, AnimationClip sourceClip)
        {
            Validate(rig);

            var path = HumanoidSourceClips.GetHumanoidClipPath(sourceClip);

            if (IsOccupiedByOtherAsset(path, sourceClip))
                throw new InvalidOperationException($"По пути «{path}» уже лежит клип, который тула сделала не из «{sourceClip.name}» или не делала вовсе. Переименуйте его или исходник — тула его не перезапишет.");

            var curves = SampleOnHiddenCopy(rig, sourceClip);
            var clip = WriteHumanoidClip(curves, sourceClip, path);

            // Сначала на диск, потом метка: метка перечитывает файл.
            AssetDatabase.SaveAssetIfDirty(clip);
            Mark(path, SourceId(sourceClip), Fingerprint(rig, sourceClip), AvatarId(rig.Animator.avatar));

            return clip;
        }


        /// <summary>Запечённый клип сделан из этой же версии исходника и рига — пересобирать незачем.</summary>
        public static bool IsUpToDate(HumanoidAnimationRig rig, AnimationClip sourceClip)
        {
            var path = HumanoidSourceClips.GetHumanoidClipPath(sourceClip);
            var parts = ReadMark(path);

            return parts != null
                && parts.Length > MarkFingerprintPart
                && parts[MarkSourcePart] == SourceId(sourceClip)
                && parts[MarkFingerprintPart] == Fingerprint(rig, sourceClip);
        }


        private static void Validate(HumanoidAnimationRig rig)
        {
            // Только идущая игра: перед самым запуском запекать можно и нужно.
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Во время игры не запекаем — выйдите из Play Mode.");

            var animator = rig.Animator;

            if (animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException($"У «{rig.name}» нет правильного Humanoid-аватара.");

            if (rig.GetComponent<RigBuilder>() == null || rig.ControlsRoot == null)
                throw new InvalidOperationException($"У «{rig.name}» сломан риг. Пересоздайте ручки в «Настройке рига».");
        }


        // ── Копия для запекания ──────────────────────────────

        private static HumanPoseCurves SampleOnHiddenCopy(HumanoidAnimationRig rig, AnimationClip sourceClip)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            var hiddenCopy = Object.Instantiate(rig.gameObject);

            try
            {
                hiddenCopy.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(hiddenCopy, previewScene);

                var copyRig = hiddenCopy.GetComponent<HumanoidAnimationRig>();
                PrepareCopy(copyRig, rig);

                return SamplePoses(copyRig.Animator, hiddenCopy.GetComponent<RigBuilder>(), sourceClip);
            }
            finally
            {
                Object.DestroyImmediate(hiddenCopy);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }


        // Ручки — в исходную позу: без ключа ручка для клипа стоит именно там.
        // Вес IK без ключа — тот, что выбран на риге. Но если его сейчас крутит предпросмотр
        // другого клипа, это не выбор человека — берём режим по умолчанию.
        private static void PrepareCopy(HumanoidAnimationRig copyRig, HumanoidAnimationRig originalRig)
        {
            foreach (var control in copyRig.Controls)
            {
                if (control.Handle == null)
                    continue;

                control.Handle.localPosition = control.RestLocalPosition;
                control.Handle.localRotation = control.RestLocalRotation;
            }

            for (var i = 0; i < copyRig.Limbs.Count && i < originalRig.Limbs.Count; i++)
            {
                if (copyRig.Limbs[i].IKConstraint != null)
                    copyRig.Limbs[i].IKConstraint.weight = ChosenIKWeight(originalRig.Limbs[i]);
            }
        }

        private static float ChosenIKWeight(HumanoidRigLimb limb)
        {
            var isAnimatedByPreview = limb.IKConstraint != null && AnimationMode.IsPropertyAnimated(limb.IKConstraint, ConstraintWeightProperty);
            return isAnimatedByPreview ? HumanoidRigLimbBuilder.DefaultIKWeight(limb.Limb) : limb.IKWeight;
        }


        // Тот же путь, что у предпросмотра окна Animation: граф с клипом, к нему цепочка
        // ограничений рига, сэмплирование через AnimationMode. Свой драйвер AnimationMode,
        // чтобы не выключить чужой предпросмотр. Всё созданное убирается в любом случае.
        private static HumanPoseCurves SamplePoses(Animator animator, RigBuilder rigBuilder, AnimationClip sourceClip)
        {
            var curves = new HumanPoseCurves();
            var driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            var graph = PlayableGraph.Create(GraphName);
            HumanPoseHandler poseHandler = null;
            var isPreviewStarted = false;

            try
            {
                AnimationMode.StartAnimationMode(driver);

                poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                var clipPlayable = AnimationClipPlayable.Create(graph, sourceClip);
                clipPlayable.SetApplyFootIK(false);

                rigBuilder.StartPreview();
                isPreviewStarted = true;

                var output = AnimationPlayableOutput.Create(graph, GraphName, animator);
                output.SetSourcePlayable(rigBuilder.BuildPreviewGraph(graph, clipPlayable));

                var frameRate = SamplingFrameRate(sourceClip);
                var frameCount = sourceClip.length > 0f ? Mathf.Max(1, Mathf.CeilToInt(sourceClip.length * frameRate)) : 0;
                var pose = new HumanPose();

                for (var frame = 0; frame <= frameCount; frame++)
                {
                    var time = Mathf.Min(frame / frameRate, sourceClip.length);
                    var sampleTime = frame == frameCount && frame > 0 ? Mathf.Max(0f, time - EndTimeMargin) : time;

                    rigBuilder.UpdatePreviewGraph(graph);

                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, sampleTime);
                    AnimationMode.EndSampling();

                    poseHandler.GetHumanPose(ref pose);
                    curves.AddPose(time, pose);
                }
            }
            finally
            {
                // Каждый шаг уборки отдельно: упадёт один — остальные всё равно выполнятся,
                // и AnimationMode не останется висеть включённым.
                try
                {
                    if (isPreviewStarted)
                        rigBuilder.StopPreview();
                }
                finally
                {
                    if (graph.IsValid())
                        graph.Destroy();

                    poseHandler?.Dispose();
                    AnimationMode.StopAnimationMode(driver);
                    Object.DestroyImmediate(driver);
                }
            }

            return curves;
        }


        private static float SamplingFrameRate(AnimationClip clip) => Mathf.Max(clip.frameRate, MinimalFrameRate);


        // ── Запись клипа ──────────────────────────────

        // Новый клип берёт настройки и события исходника. У существующего меняем только длину:
        // корень, зацикливание, зеркало и события на нём выставлял человек.
        private static AnimationClip WriteHumanoidClip(HumanPoseCurves curves, AnimationClip sourceClip, string path)
        {
            // Главный клип файла: в файле анимации рядом с ним лежит спрятанный исходник.
            var clip = AssetDatabase.LoadMainAssetAtPath(path) as AnimationClip;
            var isNew = clip == null;

            if (isNew)
                clip = new AnimationClip();
            else
                clip.ClearCurves();

            var settings = AnimationUtility.GetAnimationClipSettings(isNew ? sourceClip : clip);
            var events = AnimationUtility.GetAnimationEvents(isNew ? sourceClip : clip);

            clip.frameRate = SamplingFrameRate(sourceClip);
            curves.WriteTo(clip);

            settings.startTime = 0f;
            settings.stopTime = sourceClip.length;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, events);

            if (isNew)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);

            return clip;
        }


        // ── Пометка своих файлов ──────────────────────────────
        //
        // Пометка в настройках импорта: «наша метка | исходник | отпечаток версии | аватар».
        // Файл есть, а метки нет — файл чужой, трогать нельзя.

        /// <summary>Сделан ли файл тулой: игровой клип или файл анимации со спрятанным исходником.</summary>
        public static bool IsOwnFile(string path) => ReadMark(path) != null;


        /// <summary>Запечён ли игровой клип по этому пути из этого исходника.</summary>
        public static bool IsBakedFrom(string path, AnimationClip sourceClip)
        {
            var parts = ReadMark(path);
            return parts != null && parts.Length > MarkSourcePart && parts[MarkSourcePart] == SourceId(sourceClip);
        }


        /// <summary>Пометить своим новый файл анимации. Отпечатка нет — клип считается незапечённым.</summary>
        public static void MarkNewFile(string path, AnimationClip sourceClip, Avatar avatar) =>
            Mark(path, SourceId(sourceClip), string.Empty, AvatarId(avatar));


        /// <summary>
        /// Исходник переехал в другой файл, кривые те же: метка переходит на него, а запечённый
        /// клип остаётся актуальным.
        /// </summary>
        public static void MoveMarkToSource(string path, HumanoidAnimationRig rig, AnimationClip newSourceClip) =>
            Mark(path, SourceId(newSourceClip), Fingerprint(rig, newSourceClip), AvatarId(rig.Animator.avatar));


        /// <summary>Исходник, из которого пекли клип по этому пути. Метки нет или исходник удалён — null.</summary>
        public static AnimationClip LoadMarkedSource(string path)
        {
            var parts = ReadMark(path);

            if (parts == null || parts.Length <= MarkSourcePart)
                return null;

            var sourceId = parts[MarkSourcePart];
            var separator = sourceId.IndexOf(SourceIdSeparator);

            if (separator < 0)
                return null;

            var sourcePath = AssetDatabase.GUIDToAssetPath(sourceId.Substring(0, separator));
            var clipName = sourceId.Substring(separator + 1);

            foreach (var clip in HumanoidSourceClips.LoadClipsAt(sourcePath))
            {
                if (SafeClipName(clip) == clipName)
                    return clip;
            }

            return null;
        }


        /// <summary>Аватар персонажа, на котором пекли клип. Метка старая, без аватара — null.</summary>
        public static Avatar LoadMarkedAvatar(string path)
        {
            var parts = ReadMark(path);

            if (parts == null || parts.Length <= MarkAvatarPart || !GlobalObjectId.TryParse(parts[MarkAvatarPart], out var avatarId))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarId) as Avatar;
        }


        // Свой файл анимации со спрятанным исходником — наш, как бы исходник ни звали.
        // Отдельный «_Humanoid» — наш, только если запечён из этого же исходника.
        private static bool IsOccupiedByOtherAsset(string path, AnimationClip sourceClip)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                return false;

            if (HumanoidAnimationFiles.IsEmbeddedSource(sourceClip))
                return false;

            return !IsBakedFrom(path, sourceClip);
        }


        private static string[] ReadMark(string path)
        {
            var importer = AssetImporter.GetAtPath(path);

            if (importer == null || string.IsNullOrEmpty(importer.userData) || !importer.userData.StartsWith(BakedClipMark))
                return null;

            return importer.userData.Split(MarkSeparator);
        }


        private static void Mark(string path, string sourceId, string fingerprint, string avatarId)
        {
            var importer = AssetImporter.GetAtPath(path);

            if (importer == null)
                return;

            importer.userData = string.Join(MarkSeparator.ToString(), BakedClipMark, sourceId, fingerprint, avatarId);
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }


        // Исходник — файл и имя клипа в нём: у клипов внутри таймлайна файл общий.
        private static string SourceId(AnimationClip sourceClip) =>
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceClip)) + SourceIdSeparator + SafeClipName(sourceClip);

        private static string SafeClipName(AnimationClip clip) => clip.name.Replace(MarkSeparator, '_');


        private static string AvatarId(Avatar avatar) =>
            avatar != null ? GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString() : string.Empty;


        // Отпечаток всего, от чего зависит результат: кривые и длина клипа, устройство рига
        // и выбранные режимы рук и ног. Совпал — запечённый клип актуален.
        private static string Fingerprint(HumanoidAnimationRig rig, AnimationClip sourceClip)
        {
            var text = new StringBuilder();

            text.Append(sourceClip.length).Append(';').Append(sourceClip.frameRate).Append(';');
            text.Append(rig.Controls.Count).Append(';').Append(rig.HasFingerControls).Append(';');

            foreach (var limb in rig.Limbs)
                text.Append(ChosenIKWeight(limb)).Append(';');

            foreach (var binding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                text.Append(binding.path).Append(binding.propertyName);

                foreach (var key in AnimationUtility.GetEditorCurve(sourceClip, binding).keys)
                    text.Append(key.time).Append(key.value).Append(key.inTangent).Append(key.outTangent);
            }

            return Hash128.Compute(text.ToString()).ToString();
        }
    }
}
