using System;
using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Пульт окна Animation. Предпросмотр, запись и время рига живут в этом окне: тула их не
    /// повторяет, а управляет окном снаружи — показать клип, включить запись, перейти к кадру,
    /// проиграть. Так ручки работают ровно как при обычной работе в окне, со всеми его ключами.
    ///
    /// Проигрывает сама, по часам редактора: окно, спрятанное за другой вкладкой, не обновляется.
    /// Окна нет — открывает его вкладкой рядом с Project или Console, чтобы не закрыть сцену.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidAnimationWindowRemote
    {
        private static readonly string[] DockNextToWindowTypes =
        {
            "UnityEditor.ProjectBrowser,UnityEditor",
            "UnityEditor.ConsoleWindow,UnityEditor",
        };

        private static AnimationWindow cachedWindow;
        private static bool isPlaying;
        private static double lastPlaybackTime;

        static HumanoidAnimationWindowRemote()
        {
            EditorApplication.update += AdvancePlayback;
        }


        // ── Состояние окна ──────────────────────────────

        /// <summary>Идёт ли хоть какой-то предпросмотр — в окне Animation или в Timeline.</summary>
        public static bool IsAnyPreviewRunning => AnimationMode.InAnimationMode();

        /// <summary>Клип, открытый в окне Animation. Окна нет — null.</summary>
        public static AnimationClip OpenClip
        {
            get
            {
                var window = FindWindow();
                return window != null ? window.animationClip : null;
            }
        }

        /// <summary>Включена ли запись: двигаешь ручку — ставится ключ.</summary>
        public static bool IsRecording
        {
            get
            {
                var window = FindWindow();
                return window != null && window.recording;
            }
        }

        /// <summary>Проигрывает ли клип сама тула.</summary>
        public static bool IsPlaying => isPlaying;

        /// <summary>Текущее время в окне Animation, в секундах. Поменял — поза в сцене встала в этот момент.</summary>
        public static float CurrentTime
        {
            get
            {
                var window = FindWindow();
                return window != null ? window.time : 0f;
            }
            set
            {
                var window = FindWindow();

                if (window == null)
                    return;

                window.time = Mathf.Max(0f, value);
                SceneView.RepaintAll();
            }
        }


        // ── Показать клип ──────────────────────────────

        /// <summary>Показывает ли окно этот клип с предпросмотром.</summary>
        public static bool IsPreviewing(AnimationClip clip)
        {
            var window = FindWindow();
            return clip != null && window != null && window.previewing && window.animationClip == clip;
        }


        /// <summary>
        /// Открыть клип рига в окне Animation с предпросмотром и, если просят, с записью.
        /// Клип уже должен быть в контроллере рига: окно показывает только клипы своего аниматора.
        /// </summary>
        public static void ShowClip(HumanoidAnimationRig rig, AnimationClip clip, bool record)
        {
            if (!rig.Contains(Selection.activeTransform))
                Selection.activeGameObject = rig.gameObject;

            var window = OpenWindow();
            ApplyToWindow(window, clip, record);

            // Окно подхватывает новое выделение на следующем тике и может сбросить клип и предпросмотр.
            EditorApplication.delayCall += () =>
            {
                if (window != null && rig != null && clip != null)
                    ApplyToWindow(window, clip, record);
            };
        }

        private static void ApplyToWindow(AnimationWindow window, AnimationClip clip, bool record)
        {
            if (window.animationClip != clip)
                window.animationClip = clip;

            window.previewing = true;

            if (record && window.canRecord)
                window.recording = true;
        }


        /// <summary>Включить или выключить запись в окне Animation.</summary>
        public static void SetRecording(bool record)
        {
            var window = FindWindow();

            if (window != null && (!record || window.canRecord))
                window.recording = record;
        }


        /// <summary>Выключить проигрывание, запись и предпросмотр во всех окнах Animation.</summary>
        public static void StopAll()
        {
            StopPlayback();

            foreach (var window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
            {
                window.recording = false;
                window.previewing = false;
            }
        }


        // ── Проигрывание ──────────────────────────────

        public static void StartPlayback()
        {
            if (FindWindow() == null)
                return;

            isPlaying = true;
            lastPlaybackTime = EditorApplication.timeSinceStartup;
        }


        public static void StopPlayback() => isPlaying = false;


        // Время идёт по часам редактора, по кругу от начала до конца клипа. Пропал предпросмотр
        // или клип пустой — проигрывать нечего, останавливаемся.
        private static void AdvancePlayback()
        {
            if (!isPlaying)
                return;

            var window = FindWindow();
            var clip = window != null ? window.animationClip : null;

            if (clip == null || !window.previewing || clip.length <= 0f || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                isPlaying = false;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var passed = (float)(now - lastPlaybackTime);
            lastPlaybackTime = now;

            window.time = Mathf.Repeat(window.time + passed, clip.length);
            SceneView.RepaintAll();
        }


        // ── Окно ──────────────────────────────

        private static AnimationWindow FindWindow()
        {
            if (cachedWindow != null)
                return cachedWindow;

            var windows = Resources.FindObjectsOfTypeAll<AnimationWindow>();
            cachedWindow = windows.Length > 0 ? windows[0] : null;
            return cachedWindow;
        }


        // Новое окно — вкладкой рядом с Project или Console: само по себе оно открылось бы
        // плавающим поверх сцены.
        private static AnimationWindow OpenWindow()
        {
            var window = FindWindow();

            if (window != null)
                return window;

            var dockNextTo = new List<Type>();

            foreach (var typeName in DockNextToWindowTypes)
            {
                var windowType = Type.GetType(typeName);

                if (windowType != null)
                    dockNextTo.Add(windowType);
            }

            cachedWindow = EditorWindow.GetWindow<AnimationWindow>(dockNextTo.ToArray());
            return cachedWindow;
        }
    }
}
