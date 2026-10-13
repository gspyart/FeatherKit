using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Инспектор рабочей копии: как анимировать в три шага и кнопка «Анимировать», список
    /// анимаций персонажа с их игровыми клипами, режимы FK/IK рук и ног, вид глазами и
    /// обслуживание рига. Сами действия живут в своих классах — здесь только кнопки к ним.
    /// </summary>
    [CustomEditor(typeof(HumanoidAnimationRig))]
    internal sealed class HumanoidAnimationRigEditor : Editor
    {
        private const string SectionKeyPrefix = "humanoid-rig.";

        // Клипы ищутся по сцене и проекту — раз в секунду хватает, каждую перерисовку дорого.
        private const double ClipsRefreshInterval = 1.0;

        private const float StepNumberWidth = 18f;
        private const float ClipButtonWidth = 64f;

        private readonly List<AnimationClip> rigClips = new List<AnimationClip>();
        private double clipsRefreshTime;

        private GUIStyle stepNumberStyle;
        private GUIStyle stepTextStyle;


        private HumanoidAnimationRig Rig => (HumanoidAnimationRig)target;


        // ── Отрисовка ──────────────────────────────

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            HumanoidRigClipLibrary.RememberController(Rig);
            RefreshClipsIfNeeded();
            HumanoidLimbModeSwitcher.SyncVisibility(Rig);

            EditorGUILayout.LabelField("Рабочая копия для анимации. В билд не попадает: объект помечен EditorOnly.", EditorStyles.wordWrappedMiniLabel);

            InspectorSection.Draw(SectionKeyPrefix + "guide", "Как анимировать", 0, DrawGuide);
            InspectorSection.Draw(SectionKeyPrefix + "bake", "Анимации", 2, DrawClips);
            InspectorSection.Draw(SectionKeyPrefix + "limbs", "Руки и ноги: FK или IK", 4, DrawLimbModes);
            InspectorSection.Draw(SectionKeyPrefix + "eyes", "Вид глазами", 5, DrawEyesView, expandedByDefault: false);
            InspectorSection.Draw(SectionKeyPrefix + "setup", "Настройка рига", 3, DrawSetup, expandedByDefault: false);
        }


        // ── Как анимировать ──────────────────────────────

        private void DrawGuide()
        {
            if (HumanoidAnimationSession.IsActiveOn(Rig))
            {
                EditorGUILayout.LabelField($"Идёт анимация «{HumanoidAnimationFiles.DisplayName(HumanoidAnimationSession.ActiveClip)}».", EditorStyles.boldLabel);

                if (GUIButton.Draw("Готово", GUIButtonStyles.Primary, GUILayout.Height(26f)))
                {
                    HumanoidAnimationSession.Finish();
                    GUIUtility.ExitGUI();
                }
            }
            else if (GUIButton.Draw("Анимировать", GUIButtonStyles.Primary, GUILayout.Height(26f)))
            {
                HumanoidAnimationLauncher.AnimateRig(Rig);
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(4f);
            DrawStep(1, "«Анимировать» — откроется анимация персонажа и включится запись. Анимаций нет — тула спросит имя новой.");
            DrawStep(2, "Кликайте прямо по телу и двигайте: кольца вращают, кубики и шарики двигают. Ключ встаёт всей позе сразу. Время, ключи и проигрывание — на панели в окне сцены.");
            DrawStep(3, "«Готово» — игровой клип с именем анимации обновится. Его и ставьте в аниматор игры. Ctrl+S и запуск игры обновляют его тоже.");
        }


        private void DrawStep(int number, string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(number.ToString(), stepNumberStyle, GUILayout.Width(StepNumberWidth));
                GUILayout.Label(text, stepTextStyle);
            }
        }


        // ── Анимации ──────────────────────────────

        private void DrawClips()
        {
            if (rigClips.Count == 0)
                EditorGUILayout.HelpBox("Анимаций пока нет. Нажмите «Новая анимация» или перетащите готовый клип в поле «Открыть клип» ниже.", MessageType.Info);

            foreach (var clip in rigClips)
                DrawClipRow(clip);

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUIButton.Draw("Новая анимация", GUIButtonStyles.Secondary, GUILayout.Height(22f)))
                {
                    HumanoidAnimationLauncher.CreateNewAnimation(Rig);
                    ForceClipsRefresh();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(rigClips.Count == 0))
                {
                    if (GUIButton.Draw("Запечь все сейчас", GUIButtonStyles.Secondary, GUILayout.Height(22f)))
                    {
                        HumanoidAnimationRigActions.BakeAll(Rig);
                        ForceClipsRefresh();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            GUILayout.Space(4f);
            DrawOpenClipDrop();
        }


        // Строка анимации: игровой клип (или исходник, пока игрового нет), под ним — состояние, справа кнопки.
        private void DrawClipRow(AnimationClip clip)
        {
            var hasControlCurves = HumanoidSourceClips.IsSourceFor(clip, Rig);
            var gameClip = HumanoidAnimationFiles.FindGameClip(clip);
            var isLegacyPair = HumanoidAnimationFiles.IsLegacyPair(clip);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(gameClip != null ? gameClip : clip, typeof(AnimationClip), false);

                    EditorGUILayout.LabelField(DescribeClipState(clip, gameClip, hasControlCurves), EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ClipButtonWidth)))
                {
                    using (new EditorGUI.DisabledScope(HumanoidRigClipLibrary.IsTimelineClip(clip)))
                    {
                        if (GUILayout.Button(new GUIContent("Открыть", "Анимировать этот клип: окно Animation, предпросмотр и запись."), EditorStyles.miniButton))
                        {
                            HumanoidAnimationSession.Start(Rig, clip);
                            GUIUtility.ExitGUI();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!hasControlCurves))
                    {
                        if (GUILayout.Button(new GUIContent("Запечь", "Обновить игровой клип сейчас."), EditorStyles.miniButton))
                        {
                            HumanoidAnimationRigActions.BakeOne(Rig, clip);
                            ForceClipsRefresh();
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (isLegacyPair && GUILayout.Button(new GUIContent("В один файл", "Старая пара «клип + клип_Humanoid» станет одним файлом. Ссылки игры на клип сохранятся, старый исходник уйдёт в корзину."), EditorStyles.miniButton))
                    {
                        MergeLegacyPair(clip);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            GUILayout.Space(2f);
        }


        private static string DescribeClipState(AnimationClip clip, AnimationClip gameClip, bool hasControlCurves)
        {
            if (!hasControlCurves)
                return "пустая — ключей на ручках пока нет";

            if (gameClip == null || !gameClip.humanMotion)
                return "игровой клип ещё не запечён";

            if (HumanoidAnimationFiles.IsEmbeddedSource(clip))
                return "игровой клип — этот файл";

            return "старая пара: исходник «" + clip.name + "» отдельно";
        }


        private void MergeLegacyPair(AnimationClip clip)
        {
            if (!HumanoidAnimationRigActions.CanChangeRigNow())
                return;

            if (!EditorUtility.DisplayDialog("Анимация гуманоида", $"Собрать «{clip.name}» и его Humanoid-клип в один файл?\n\nИсходник с ручками переедет внутрь игрового клипа, файл получит имя «{clip.name}». Ссылки игры на клип сохранятся. Старый файл исходника уйдёт в корзину.", "Собрать", "Отмена"))
                return;

            var wasActive = HumanoidAnimationSession.IsActiveOn(Rig) && HumanoidAnimationSession.ActiveClip == clip;

            if (wasActive)
                HumanoidAnimationSession.Finish();

            var embedded = HumanoidAnimationFiles.MergeLegacyPair(Rig, clip);

            if (wasActive)
                HumanoidAnimationSession.Start(Rig, embedded);

            ForceClipsRefresh();
        }


        // Любой клип: открыть на риге. Его не было в контроллере — тула добавит.
        private void DrawOpenClipDrop()
        {
            var dropped = (AnimationClip)EditorGUILayout.ObjectField(
                new GUIContent("Открыть клип", "Перетащите сюда клип — он подключится к ригу и откроется для анимации."),
                null, typeof(AnimationClip), false);

            if (dropped == null)
                return;

            HumanoidAnimationSession.Start(Rig, HumanoidAnimationFiles.FindSourceClip(dropped));
            ForceClipsRefresh();
            GUIUtility.ExitGUI();
        }


        private void RefreshClipsIfNeeded()
        {
            if (EditorApplication.timeSinceStartup < clipsRefreshTime)
                return;

            // Список меняем только на разметке: иначе разметка и отрисовка кадра разойдутся.
            if (Event.current.type != EventType.Layout)
                return;

            clipsRefreshTime = EditorApplication.timeSinceStartup + ClipsRefreshInterval;

            rigClips.Clear();
            rigClips.AddRange(HumanoidRigClipLibrary.CollectClips(Rig));
        }

        private void ForceClipsRefresh() => clipsRefreshTime = 0;


        // ── Руки и ноги ──────────────────────────────

        private void DrawLimbModes()
        {
            EditorGUILayout.LabelField("FK — вращаешь суставы по очереди: мах руками, замах, дуги. IK — тянешь кисть или стопу: хват за опору, стопы на полу. В сцене видны ручки только текущего режима.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);

            HumanoidLimbModePanel.Draw(Rig, withBlendSliders: true);
        }


        // ── Вид глазами ──────────────────────────────

        private void DrawEyesView()
        {
            var text = Rig.HasEyesView
                ? $"Своя точка взгляда запомнена, обзор {Rig.EyesViewFieldOfView:0}°."
                : "Точка взгляда — между глаз чуть впереди лица. Своя: поставьте камеру сцены, как камеру в игре, и нажмите «Запомнить вид».";

            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(HumanoidEyesView.IsLookingThroughEyes ? "Вернуть камеру" : "Смотреть глазами"), EditorStyles.miniButtonLeft))
                    HumanoidEyesView.Toggle(Rig);

                if (GUILayout.Button(new GUIContent("Запомнить вид", "Запомнить, откуда сейчас смотрит камера сцены, и её обзор."), EditorStyles.miniButtonMid))
                    HumanoidEyesView.RememberView(Rig);

                using (new EditorGUI.DisabledScope(!Rig.HasEyesView))
                {
                    if (GUILayout.Button(new GUIContent("Забыть", "Снова между глаз."), EditorStyles.miniButtonRight))
                        HumanoidEyesView.ForgetView(Rig);
                }
            }
        }


        // ── Настройка рига ──────────────────────────────

        private void DrawSetup()
        {
            DrawToggle(new GUIContent("Ключ на всю позу", "Сдвинул одну ручку при записи — ключ встаёт всем ручкам в этом кадре. Выключено — только сдвинутой."),
                Rig.KeyWholePose, value => Rig.KeyWholePose = value, "Ключ на всю позу");

            DrawToggle(new GUIContent("Обновлять игровые клипы сами", "По Ctrl+S и перед запуском игры игровые клипы изменённых анимаций пересобираются сами."),
                Rig.BakeOnSave, value => Rig.BakeOnSave = value, "Обновлять игровые клипы сами");

            var shouldShowControls = EditorGUILayout.ToggleLeft(new GUIContent("Показывать ручки в сцене", "Кольца, кубики и шарики, за которые берёшься мышкой. Выключено — клик по телу тоже не выбирает ручки."), Rig.ControlsShown);

            if (shouldShowControls != Rig.ControlsShown)
                HumanoidAnimationRigActions.SetControlsShown(Rig, shouldShowControls);

            GUILayout.Space(8f);

            var fingersText = Rig.HasFingerControls
                ? "Ручки пальцев есть. Убрать их — схема станет проще, анимация пальцев перестанет работать."
                : "Ручек пальцев нет. Добавить — появятся кольца на каждой фаланге.";

            EditorGUILayout.LabelField(fingersText, EditorStyles.wordWrappedMiniLabel);

            if (GUIButton.Draw(Rig.HasFingerControls ? "Убрать пальцы" : "Добавить пальцы", GUIButtonStyles.Secondary, GUILayout.Height(22f)))
            {
                HumanoidAnimationRigActions.Rebuild(Rig, !Rig.HasFingerControls);
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Ручки сломались или кости поменялись — пересоздайте. Анимации сохранятся: имена ручек те же.", EditorStyles.wordWrappedMiniLabel);

            if (GUIButton.Draw("Пересоздать ручки", GUIButtonStyles.Secondary, GUILayout.Height(22f)))
            {
                HumanoidAnimationRigActions.Rebuild(Rig, Rig.HasFingerControls);
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(8f);

            if (GUIButton.Draw("Удалить рабочую копию", GUIButtonStyles.Destructive, GUILayout.Height(22f)))
            {
                HumanoidAnimationRigActions.DeleteRig(Rig);
                GUIUtility.ExitGUI();
            }
        }


        private void DrawToggle(GUIContent content, bool value, System.Action<bool> apply, string undoName)
        {
            var newValue = EditorGUILayout.ToggleLeft(content, value);

            if (newValue == value)
                return;

            Undo.RecordObject(Rig, undoName);
            apply(newValue);
            EditorUtility.SetDirty(Rig);
        }


        // ── Стили ──────────────────────────────

        private void EnsureStyles()
        {
            if (stepNumberStyle != null)
                return;

            stepNumberStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            stepTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        }
    }
}
