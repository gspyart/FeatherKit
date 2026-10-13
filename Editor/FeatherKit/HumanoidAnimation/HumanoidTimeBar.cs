using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Полоска времени клипа: ромбики — ключи позы, красная черта — текущий кадр. Клик или
    /// перетаскивание по полоске — перейти к кадру. Ромбик можно перетащить на другой кадр —
    /// вместе с ним переедет вся поза. Время и предпросмотр ведёт окно Animation, полоска — его пульт.
    ///
    /// Полоска показывает клип и немного места после него: чтобы продлить анимацию, перейди за
    /// последний ключ и поставь позу.
    /// </summary>
    internal static class HumanoidTimeBar
    {
        private const float BarHeight = 30f;
        private const float KeySize = 8f;
        private const float KeyPickDistance = 6f;
        private const float MinimalTickSpacing = 7f;
        private const float MinimalVisibleSeconds = 1f;
        private const float VisibleTailShare = 0.25f;

        private static readonly int ControlHint = "FeatherKit.HumanoidTimeBar".GetHashCode();
        private static readonly int[] TickSteps = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };

        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color TickColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color SecondTickColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color KeyColor = new Color(1f, 0.78f, 0.25f);
        private static readonly Color CurrentKeyColor = Color.white;
        private static readonly Color PlayheadColor = new Color(1f, 0.3f, 0.25f);
        private static readonly Color ClipEndColor = new Color(1f, 1f, 1f, 0.35f);

        private static float frozenVisibleLength;
        private static float draggedKeyTime = -1f;
        private static float draggedKeyTargetTime;


        // ── Отрисовка и мышь ──────────────────────────────

        public static void Draw(AnimationClip clip)
        {
            var rect = GUILayoutUtility.GetRect(0f, BarHeight, GUILayout.ExpandWidth(true));
            var id = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            var evt = Event.current;
            var isDragging = GUIUtility.hotControl == id;
            var visibleLength = isDragging ? frozenVisibleLength : MeasureVisibleLength(clip);

            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (evt.button != 0 || !rect.Contains(evt.mousePosition))
                        break;

                    GUIUtility.hotControl = id;
                    frozenVisibleLength = visibleLength;
                    HumanoidAnimationWindowRemote.StopPlayback();
                    StartDrag(clip, rect, visibleLength, evt.mousePosition.x);
                    evt.Use();
                    break;

                case EventType.MouseDrag:
                    if (!isDragging)
                        break;

                    ContinueDrag(clip, rect, visibleLength, evt.mousePosition.x);
                    evt.Use();
                    break;

                case EventType.MouseUp:
                    if (!isDragging)
                        break;

                    GUIUtility.hotControl = 0;
                    FinishDrag(clip);
                    evt.Use();
                    break;

                case EventType.Repaint:
                    DrawBar(clip, rect, visibleLength);
                    break;
            }
        }


        // Нажали у ромбика — тащим ключ позы. Иначе — перематываем время.
        private static void StartDrag(AnimationClip clip, Rect rect, float visibleLength, float mouseX)
        {
            draggedKeyTime = -1f;

            foreach (var keyTime in HumanoidPoseKeys.CollectKeyTimes(clip))
            {
                if (Mathf.Abs(TimeToX(rect, visibleLength, keyTime) - mouseX) > KeyPickDistance)
                    continue;

                draggedKeyTime = keyTime;
                draggedKeyTargetTime = keyTime;
                HumanoidAnimationWindowRemote.CurrentTime = keyTime;
                return;
            }

            HumanoidAnimationWindowRemote.CurrentTime = XToFrameTime(clip, rect, visibleLength, mouseX);
        }


        private static void ContinueDrag(AnimationClip clip, Rect rect, float visibleLength, float mouseX)
        {
            var time = XToFrameTime(clip, rect, visibleLength, mouseX);

            if (draggedKeyTime >= 0f)
                draggedKeyTargetTime = time;
            else
                HumanoidAnimationWindowRemote.CurrentTime = time;
        }


        // Отпустили ромбик на другом кадре — поза переезжает туда, время идёт за ней.
        private static void FinishDrag(AnimationClip clip)
        {
            if (draggedKeyTime >= 0f && HumanoidPoseKeys.ToFrame(clip, draggedKeyTime) != HumanoidPoseKeys.ToFrame(clip, draggedKeyTargetTime))
            {
                HumanoidPoseKeys.MovePoseKey(clip, draggedKeyTime, draggedKeyTargetTime);
                HumanoidAnimationWindowRemote.CurrentTime = draggedKeyTargetTime;
            }

            draggedKeyTime = -1f;
        }


        // ── Рисование ──────────────────────────────

        private static void DrawBar(AnimationClip clip, Rect rect, float visibleLength)
        {
            EditorGUI.DrawRect(rect, BackgroundColor);
            DrawTicks(clip, rect, visibleLength);

            if (clip.length > 0f)
            {
                var endX = TimeToX(rect, visibleLength, clip.length);
                EditorGUI.DrawRect(new Rect(endX, rect.y, 1f, rect.height), ClipEndColor);
            }

            var currentTime = HumanoidAnimationWindowRemote.CurrentTime;
            var tolerance = 0.5f / HumanoidPoseKeys.FrameRate(clip);

            foreach (var keyTime in HumanoidPoseKeys.CollectKeyTimes(clip))
            {
                var isDragged = draggedKeyTime >= 0f && Mathf.Abs(keyTime - draggedKeyTime) <= tolerance;
                var shownTime = isDragged ? draggedKeyTargetTime : keyTime;
                var isCurrent = Mathf.Abs(shownTime - currentTime) <= tolerance;

                DrawKey(new Vector2(TimeToX(rect, visibleLength, shownTime), rect.center.y), isCurrent || isDragged ? CurrentKeyColor : KeyColor);
            }

            var playheadX = TimeToX(rect, visibleLength, currentTime);
            EditorGUI.DrawRect(new Rect(playheadX - 1f, rect.y, 2f, rect.height), PlayheadColor);
        }


        // Чёрточки кадров — с шагом, чтобы не слиплись; каждая секунда — ярче.
        private static void DrawTicks(AnimationClip clip, Rect rect, float visibleLength)
        {
            var frameRate = HumanoidPoseKeys.FrameRate(clip);
            var framesVisible = visibleLength * frameRate;
            var step = TickSteps[TickSteps.Length - 1];

            foreach (var candidate in TickSteps)
            {
                if (rect.width / framesVisible * candidate >= MinimalTickSpacing)
                {
                    step = candidate;
                    break;
                }
            }

            var framesPerSecond = Mathf.Max(1, Mathf.RoundToInt(frameRate));

            for (var frame = 0; frame <= framesVisible; frame += step)
            {
                var isSecond = frame % framesPerSecond == 0;
                var x = TimeToX(rect, visibleLength, frame / frameRate);
                var height = isSecond ? rect.height * 0.5f : rect.height * 0.25f;

                EditorGUI.DrawRect(new Rect(x, rect.yMax - height, 1f, height), isSecond ? SecondTickColor : TickColor);
            }
        }


        // Ромбик — квадрат, повёрнутый на 45°.
        private static void DrawKey(Vector2 center, Color color)
        {
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, center);
            EditorGUI.DrawRect(new Rect(center.x - KeySize * 0.5f, center.y - KeySize * 0.5f, KeySize, KeySize), color);
            GUI.matrix = previousMatrix;
        }


        // ── Время и место на полоске ──────────────────────────────

        // Клип и ещё четверть после него: есть куда перейти, чтобы продлить анимацию.
        private static float MeasureVisibleLength(AnimationClip clip)
        {
            var content = Mathf.Max(MinimalVisibleSeconds, clip.length, HumanoidAnimationWindowRemote.CurrentTime);
            return content * (1f + VisibleTailShare);
        }


        private static float TimeToX(Rect rect, float visibleLength, float time) =>
            rect.x + Mathf.Clamp01(time / visibleLength) * rect.width;


        private static float XToFrameTime(AnimationClip clip, Rect rect, float visibleLength, float x)
        {
            var time = Mathf.Clamp01((x - rect.x) / rect.width) * visibleLength;
            return HumanoidPoseKeys.SnapToFrame(clip, time);
        }
    }
}
