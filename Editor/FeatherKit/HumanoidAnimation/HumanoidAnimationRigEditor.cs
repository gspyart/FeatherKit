using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Инспектор рабочей копии: пошаговая подсказка, как анимировать, схема тела для выбора
    /// ручек, режимы FK/IK рук и ног, список клипов с их Humanoid-версиями и обслуживание рига.
    /// Сами действия живут в <see cref="HumanoidAnimationRigActions"/>.
    /// </summary>
    [CustomEditor(typeof(HumanoidAnimationRig))]
    internal sealed class HumanoidAnimationRigEditor : Editor
    {
        private const string SectionKeyPrefix = "humanoid-rig.";

        // Клипы ищутся по сцене и проекту — раз в секунду хватает, каждую перерисовку дорого.
        private const double ClipsRefreshInterval = 1.0;

        private const float StepNumberWidth = 18f;
        private const float ClipButtonWidth = 58f;

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
            InspectorSection.Draw(SectionKeyPrefix + "controls", "Ручки", 1, DrawControls);
            InspectorSection.Draw(SectionKeyPrefix + "limbs", "Руки и ноги: FK или IK", 4, DrawLimbModes);
            InspectorSection.Draw(SectionKeyPrefix + "bake", "Клипы", 2, DrawClips);
            InspectorSection.Draw(SectionKeyPrefix + "setup", "Настройка рига", 3, DrawSetup, expandedByDefault: false);
        }


        // ── Как анимировать ──────────────────────────────

        private void DrawGuide()
        {
            DrawStep(1, "Откройте окно Animation или Timeline.");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(StepNumberWidth);

                if (GUILayout.Button(new GUIContent("Окно Animation", "Откроет окно и выделит риг."), EditorStyles.miniButtonLeft))
                    HumanoidAnimationRigActions.OpenAnimationWindow(Rig);

                if (GUILayout.Button(new GUIContent("Окно Timeline", "Перетащите риг на дорожку Animation Track."), EditorStyles.miniButtonRight))
                    HumanoidAnimationRigActions.OpenTimelineWindow(Rig);
            }

            DrawStep(2, "Нет клипа — нажмите Create в окне Animation и сохраните его в любую папку.");
            DrawStep(3, "Включите предпросмотр: без него ручки двигаются, а тело стоит.");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(StepNumberWidth);

                if (HumanoidAnimationRigActions.IsPreviewing)
                {
                    EditorGUILayout.LabelField("Предпросмотр включён", EditorStyles.miniBoldLabel);
                }
                else if (GUILayout.Button("Включить предпросмотр", EditorStyles.miniButton))
                {
                    HumanoidAnimationRigActions.StartPreview(Rig);
                    GUIUtility.ExitGUI();
                }
            }

            DrawStep(4, "Включите запись (красный кружок) и двигайте ручки. Кольца — вращать (E), кубики и шарики — двигать (W), таз — и то и другое.");
            DrawStep(5, $"Ctrl+S — рядом с клипом появится «Имя{HumanoidSourceClips.HumanoidClipSuffix}». Его и используйте в игре.");
        }


        private void DrawStep(int number, string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(number.ToString(), stepNumberStyle, GUILayout.Width(StepNumberWidth));
                GUILayout.Label(text, stepTextStyle);
            }
        }


        // ── Ручки ──────────────────────────────

        private void DrawControls()
        {
            HumanoidBodyPicker.Draw(Rig);

            EditorGUILayout.LabelField("Shift-клик — добавить к выделению. Подсказка — при наведении.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Сбросить выделенные", "Вернуть выделенные ручки в исходную позу. При записи поставит ключи."), EditorStyles.miniButtonLeft))
                    HumanoidAnimationRigActions.ResetControls(HumanoidAnimationRigActions.FindSelectedControls(Rig));

                if (GUILayout.Button(new GUIContent("Сбросить все", "Вернуть все ручки в исходную позу. При записи поставит ключи."), EditorStyles.miniButtonRight))
                    HumanoidAnimationRigActions.ResetControls(Rig.Controls);
            }

            var shouldShow = EditorGUILayout.ToggleLeft(new GUIContent("Показывать ручки в сцене", "Кольца, кубики и шарики, за которые берёшься мышкой."), Rig.ControlsShown);

            if (shouldShow != Rig.ControlsShown)
                HumanoidAnimationRigActions.SetControlsShown(Rig, shouldShow);
        }


        // ── Руки и ноги ──────────────────────────────

        private void DrawLimbModes()
        {
            EditorGUILayout.LabelField("FK — вращаешь суставы по очереди: мах руками, замах, дуги. IK — тянешь кисть или стопу: хват за опору, стопы на полу. В сцене видны ручки только текущего режима.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);

            HumanoidLimbModePanel.Draw(Rig, withBlendSliders: true);
        }


        // ── Клипы ──────────────────────────────

        private void DrawClips()
        {
            var bakeOnSave = EditorGUILayout.ToggleLeft(new GUIContent("Пересобирать Humanoid при Ctrl+S", "Сохранили проект — Humanoid-версии изменённых клипов обновились сами."), Rig.BakeOnSave);

            if (bakeOnSave != Rig.BakeOnSave)
            {
                Undo.RecordObject(Rig, "Пересобирать при сохранении");
                Rig.BakeOnSave = bakeOnSave;
            }

            GUILayout.Space(4f);

            if (rigClips.Count == 0)
                EditorGUILayout.HelpBox("Клипов пока нет. Создайте клип в окне Animation (кнопка Create) или перетащите готовый в поле «Открыть клип» ниже.", MessageType.Info);

            foreach (var clip in rigClips)
                DrawClipRow(clip);

            GUILayout.Space(4f);
            DrawOpenClipDrop();
            GUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(rigClips.Count == 0))
            {
                if (GUIButton.Draw("Запечь все сейчас", GUIButtonStyles.Primary, GUILayout.Height(24f)))
                {
                    HumanoidAnimationRigActions.BakeAll(Rig);
                    ForceClipsRefresh();
                    GUIUtility.ExitGUI();
                }
            }
        }


        // Строка клипа: исходник, под ним — его Humanoid-версия или почему её нет, справа кнопки.
        private void DrawClipRow(AnimationClip clip)
        {
            var hasControlCurves = HumanoidSourceClips.IsSourceFor(clip, Rig);
            var humanoidClip = hasControlCurves ? HumanoidSourceClips.LoadHumanoidClip(clip) : null;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);

                    DrawClipStatus(hasControlCurves, humanoidClip);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ClipButtonWidth)))
                {
                    using (new EditorGUI.DisabledScope(HumanoidRigClipLibrary.IsTimelineClip(clip)))
                    {
                        if (GUILayout.Button(new GUIContent("Открыть", "Открыть клип на риге в окне Animation."), EditorStyles.miniButton))
                        {
                            HumanoidRigClipLibrary.OpenInAnimationWindow(Rig, clip);
                            GUIUtility.ExitGUI();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!hasControlCurves))
                    {
                        if (GUILayout.Button(new GUIContent("Запечь", "Собрать Humanoid-версию этого клипа."), EditorStyles.miniButton))
                        {
                            HumanoidAnimationRigActions.BakeOne(Rig, clip);
                            ForceClipsRefresh();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            GUILayout.Space(2f);
        }


        private static void DrawClipStatus(bool hasControlCurves, AnimationClip humanoidClip)
        {
            if (!hasControlCurves)
            {
                EditorGUILayout.LabelField("пустой — ключей на ручках пока нет", EditorStyles.miniLabel);
                return;
            }

            if (humanoidClip == null)
            {
                EditorGUILayout.LabelField("Humanoid ещё не запечён", EditorStyles.miniLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("→", EditorStyles.miniLabel, GUILayout.Width(14f));

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(humanoidClip, typeof(AnimationClip), false);
            }
        }


        // Любой клип: открыть на риге. Его не было в контроллере — тула добавит.
        private void DrawOpenClipDrop()
        {
            var dropped = (AnimationClip)EditorGUILayout.ObjectField(
                new GUIContent("Открыть клип", "Перетащите сюда клип — он подключится к ригу и откроется в окне Animation."),
                null, typeof(AnimationClip), false);

            if (dropped == null)
                return;

            HumanoidRigClipLibrary.OpenInAnimationWindow(Rig, dropped);
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


        // ── Настройка рига ──────────────────────────────

        private void DrawSetup()
        {
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
