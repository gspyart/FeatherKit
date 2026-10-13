using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Верх панели «Анимация гуманоида»: какую анимацию делаем, идёт ли запись, полоска времени,
    /// переходы по ключам, проигрывание, ключ позы, повтор по кругу и «Готово».
    /// Сеанса нет — одна кнопка «Анимировать».
    /// </summary>
    internal static class HumanoidTimePanel
    {
        private const float StartButtonHeight = 28f;
        private const float TransportButtonWidth = 28f;

        private static readonly Color RecordingColor = new Color(1f, 0.38f, 0.32f);
        private static readonly Color WarningColor = new Color(1f, 0.7f, 0.3f);
        private static readonly Color StartTint = new Color(0.45f, 0.8f, 1f);
        private static readonly Color DoneTint = new Color(0.5f, 0.9f, 0.55f);

        private static GUIStyle statusStyle;


        // ── Отрисовка ──────────────────────────────

        public static void Draw(HumanoidAnimationRig rig)
        {
            EnsureStyles();

            if (!HumanoidAnimationSession.IsActiveOn(rig))
            {
                DrawStart(rig);
                return;
            }

            var clip = HumanoidAnimationSession.ActiveClip;

            DrawClipRow(rig, clip);
            DrawStatus(clip);
            GUILayout.Space(2f);
            HumanoidTimeBar.Draw(clip);
            GUILayout.Space(2f);
            DrawTransport(rig, clip);
        }


        // ── Начать ──────────────────────────────

        private static void DrawStart(HumanoidAnimationRig rig)
        {
            var otherRig = HumanoidAnimationSession.ActiveRig;

            if (otherRig != null && otherRig != rig)
                EditorGUILayout.LabelField($"Сейчас анимируется «{otherRig.name}». Начнёте здесь — там всё сохранится.", EditorStyles.wordWrappedMiniLabel);

            if (DrawTintedButton(new GUIContent("Анимировать", "Откроет анимацию этого персонажа и включит запись. Новой — спросит имя."), StartTint, GUILayout.Height(StartButtonHeight)))
            {
                HumanoidAnimationLauncher.AnimateRig(rig);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.LabelField("Кликайте по телу и двигайте — ключи встанут сами.", EditorStyles.centeredGreyMiniLabel);
        }


        // ── Какая анимация ──────────────────────────────

        private static void DrawClipRow(HumanoidAnimationRig rig, AnimationClip clip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var content = new GUIContent(HumanoidAnimationFiles.DisplayName(clip), "Другая анимация этого персонажа или новая.");
                var rect = GUILayoutUtility.GetRect(content, EditorStyles.popup, GUILayout.ExpandWidth(true));

                if (GUI.Button(rect, content, EditorStyles.popup))
                    ShowClipMenu(rig, clip, rect);

                if (DrawTintedButton(new GUIContent("Готово", "Выключить запись и обновить игровой клип. Его и ставьте в аниматор игры."), DoneTint, GUILayout.Width(64f)))
                {
                    HumanoidAnimationSession.Finish();
                    GUIUtility.ExitGUI();
                }
            }
        }


        private static void ShowClipMenu(HumanoidAnimationRig rig, AnimationClip currentClip, Rect rect)
        {
            var menu = new GenericMenu();

            foreach (var clip in HumanoidRigClipLibrary.CollectClips(rig))
            {
                if (HumanoidRigClipLibrary.IsTimelineClip(clip))
                    continue;

                var chosen = clip;
                menu.AddItem(new GUIContent(HumanoidAnimationFiles.DisplayName(clip)), clip == currentClip, () => HumanoidAnimationSession.Start(rig, chosen));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Новая анимация…"), false, () => HumanoidAnimationLauncher.CreateNewAnimation(rig));
            menu.DropDown(rect);
        }


        // ── Запись ──────────────────────────────

        private static void DrawStatus(AnimationClip clip)
        {
            var isPreviewing = HumanoidAnimationWindowRemote.IsPreviewing(clip);
            var isRecording = isPreviewing && HumanoidAnimationWindowRemote.IsRecording;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (isRecording)
                {
                    DrawStatusLabel("● Запись", "Двигайте ручки — ключи ставятся сами.", RecordingColor);
                }
                else if (isPreviewing)
                {
                    DrawStatusLabel("● Без записи", "Ручки двигаются, но ключи не ставятся.", WarningColor);

                    if (GUILayout.Button("Записывать", EditorStyles.miniButton))
                        HumanoidAnimationWindowRemote.SetRecording(true);
                }
                else
                {
                    DrawStatusLabel("● Предпросмотр выключен", "Без предпросмотра ручки двигаются, а тело стоит.", WarningColor);

                    if (GUILayout.Button("Включить", EditorStyles.miniButton))
                    {
                        HumanoidAnimationSession.Resume();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.FlexibleSpace();

                var time = HumanoidAnimationWindowRemote.CurrentTime;
                GUILayout.Label($"кадр {HumanoidPoseKeys.ToFrame(clip, time)} · {time:0.00} с", EditorStyles.miniLabel);
            }
        }

        private static void DrawStatusLabel(string text, string tooltip, Color color)
        {
            statusStyle.normal.textColor = color;
            GUILayout.Label(new GUIContent(text, tooltip), statusStyle);
        }


        // ── Время и ключи ──────────────────────────────

        private static void DrawTransport(HumanoidAnimationRig rig, AnimationClip clip)
        {
            var time = HumanoidAnimationWindowRemote.CurrentTime;
            var previousKey = HumanoidPoseKeys.FindPreviousKeyTime(clip, time);
            var nextKey = HumanoidPoseKeys.FindNextKeyTime(clip, time);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!previousKey.HasValue))
                {
                    if (GUILayout.Button(EditorGUIUtility.TrIconContent("Animation.PrevKey", "К прошлому ключу"), EditorStyles.miniButtonLeft, GUILayout.Width(TransportButtonWidth)))
                        GoTo(previousKey.Value);
                }

                var isPlaying = HumanoidAnimationWindowRemote.IsPlaying;
                var playContent = EditorGUIUtility.TrIconContent("Animation.Play", isPlaying ? "Остановить" : "Проиграть по кругу");

                if (GUILayout.Toggle(isPlaying, playContent, EditorStyles.miniButtonMid, GUILayout.Width(TransportButtonWidth)) != isPlaying)
                {
                    if (isPlaying)
                        HumanoidAnimationWindowRemote.StopPlayback();
                    else
                        HumanoidAnimationWindowRemote.StartPlayback();
                }

                using (new EditorGUI.DisabledScope(!nextKey.HasValue))
                {
                    if (GUILayout.Button(EditorGUIUtility.TrIconContent("Animation.NextKey", "К следующему ключу"), EditorStyles.miniButtonRight, GUILayout.Width(TransportButtonWidth)))
                        GoTo(nextKey.Value);
                }

                GUILayout.Space(6f);
                DrawKeyButtons(rig, clip, time);
                GUILayout.FlexibleSpace();
                DrawLoopToggle(clip);
            }
        }


        private static void GoTo(float time)
        {
            HumanoidAnimationWindowRemote.StopPlayback();
            HumanoidAnimationWindowRemote.CurrentTime = time;
        }


        private static void DrawKeyButtons(HumanoidAnimationRig rig, AnimationClip clip, float time)
        {
            var canEdit = HumanoidAnimationWindowRemote.IsPreviewing(clip);

            using (new EditorGUI.DisabledScope(!canEdit))
            {
                if (GUILayout.Button(new GUIContent("+ ключ", "Ключ всей позе в этом кадре — поза будет держаться здесь."), EditorStyles.miniButtonLeft))
                    HumanoidPoseKeys.KeyPose(rig, clip, time);

                using (new EditorGUI.DisabledScope(!HumanoidPoseKeys.HasKeyAt(clip, time)))
                {
                    if (GUILayout.Button(new GUIContent("− ключ", "Убрать ключ позы в этом кадре."), EditorStyles.miniButtonRight))
                        HumanoidPoseKeys.DeletePoseKey(clip, time);
                }
            }
        }


        // Повтор по кругу — настройка игрового клипа: его играет игра.
        private static void DrawLoopToggle(AnimationClip clip)
        {
            var gameClip = HumanoidAnimationFiles.FindGameClip(clip);

            using (new EditorGUI.DisabledScope(gameClip == null))
            {
                var isLooped = gameClip != null && HumanoidAnimationFiles.IsLooped(gameClip);
                var content = EditorGUIUtility.TrIconContent("preAudioLoopOff", gameClip != null
                    ? "Повторять по кругу: в игре анимация играет без конца. Выключено — один раз."
                    : "Появится после первого «Готово».");

                if (GUILayout.Toggle(isLooped, content, EditorStyles.miniButton, GUILayout.Width(TransportButtonWidth)) != isLooped)
                {
                    HumanoidAnimationFiles.SetLooped(gameClip, !isLooped, withUndo: true);
                    HumanoidAnimationFiles.SetLooped(clip, !isLooped, withUndo: true);
                }
            }
        }


        // ── Общие помощники ──────────────────────────────

        private static bool DrawTintedButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = tint;

            var pressed = GUILayout.Button(content, options);

            GUI.backgroundColor = previousColor;
            return pressed;
        }


        private static void EnsureStyles()
        {
            if (statusStyle != null)
                return;

            statusStyle = new GUIStyle(EditorStyles.miniBoldLabel);
        }
    }
}
