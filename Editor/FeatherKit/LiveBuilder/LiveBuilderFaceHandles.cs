using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки граней коробки в окне сцены — для коробок, комнат, лестниц и вырезов: крупный квадрат в середине каждой грани
    /// и стрелка наружу, под мышью — белые, ловятся издалека. Тянешь — ходит только эта грань, противоположная стоит;
    /// Alt — обе грани навстречу, Alt+Shift — все стороны вместе. Shift+клик — зовущему (у коробки: набрать грань). Пока тянешь — рядом размер по этой оси.
    /// Сетку и что делать с новым размером решает тот, кто зовёт.
    /// </summary>
    public static class LiveBuilderFaceHandles
    {
        // Размер квадрата — доля размера ручки Unity: на экране одинаковый при любом зуме. Штатные точки — около 0,03.
        private const float HandleSize = 0.085f;

        private static readonly int FaceControlHint = "LiveBuilderFaceHandle".GetHashCode();

        private static readonly Color HoverColor = Color.white;


        // Ручки граней коробки min–max в осях frame. Возвращает, тянули ли; min и max — новые, без сетки.
        // axisNames — как назвать размер по X, Y, Z в подписи («длина», «ширина»); null — «X», «Y», «Z».
        // editableAxes — по каким осям есть ручки, биты 1 — X, 2 — Y, 4 — Z: у проёма глубины нет, она — толщина стены.
        // onShiftClick — Shift+клик по квадрату (ось, знак грани): ручка не тянется, клик отдаётся сюда; null — Shift ничего особого не значит.
        public static bool Draw(Matrix4x4 frame, ref Vector3 min, ref Vector3 max, Color color, string[] axisNames = null, int editableAxes = 7,
            System.Action<int, float> onShiftClick = null)
        {
            var isChanged = false;

            using (new Handles.DrawingScope(frame))
                for (var axis = 0; axis < 3; axis++)
                    if ((editableAxes & (1 << axis)) != 0)
                        foreach (var sign in new[] { -1f, 1f })
                            isChanged |= DrawFace(axis, sign, ref min, ref max, color, axisNames, onShiftClick);

            return isChanged;
        }


        // Сдвинутые грани — так, чтобы размер был целым числом клеток сетки; несдвинутые стоят, где были.
        // Для того, что повёрнуто как угодно (вырез, проём): у него нет общей сетки с миром, есть только шаг.
        public static void SnapSizesToGrid(Vector3 oldMin, Vector3 oldMax, ref Vector3 min, ref Vector3 max)
        {
            var step = LiveBuilderGrid.CurrentStep;

            for (var axis = 0; axis < 3; axis++)
            {
                var isMinMoved = !Mathf.Approximately(min[axis], oldMin[axis]);
                var isMaxMoved = !Mathf.Approximately(max[axis], oldMax[axis]);

                if (!isMinMoved && !isMaxMoved)
                    continue;

                var size = Mathf.Max(step, Mathf.Round((max[axis] - min[axis]) / step) * step);

                if (isMinMoved && isMaxMoved)
                {
                    var center = (min[axis] + max[axis]) / 2f;
                    min[axis] = center - size / 2f;
                    max[axis] = center + size / 2f;
                }
                else if (isMaxMoved)
                {
                    max[axis] = min[axis] + size;
                }
                else
                {
                    min[axis] = max[axis] - size;
                }
            }
        }


        // Одна грань: квадрат-ползунок вдоль своей оси и стрелка наружу.
        private static bool DrawFace(int axis, float sign, ref Vector3 min, ref Vector3 max, Color color, string[] axisNames, System.Action<int, float> onShiftClick)
        {
            var control = GUIUtility.GetControlID(FaceControlHint, FocusType.Passive);
            var center = (min + max) / 2f;
            var faceCenter = center;
            faceCenter[axis] = sign > 0f ? max[axis] : min[axis];

            var direction = Vector3.zero;
            direction[axis] = sign;

            var size = HandleUtility.GetHandleSize(faceCenter) * HandleSize;
            var isActive = GUIUtility.hotControl == control || HandleUtility.nearestControl == control && GUIUtility.hotControl == 0;
            Handles.color = isActive ? HoverColor : color;

            if (Event.current.type == EventType.Repaint)
                DrawArrow(faceCenter, direction, size);

            var current = Event.current;

            if (onShiftClick != null && current.type == EventType.MouseDown && current.button == 0 && current.shift && !current.alt
                && HandleUtility.nearestControl == control)
            {
                onShiftClick(axis, sign);
                current.Use();
                return false;
            }

            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(control, faceCenter, direction, size, Handles.DotHandleCap, 0f);

            if (GUIUtility.hotControl == control && Event.current.type == EventType.Repaint)
                LiveBuilderSceneText.DrawLabel(faceCenter + direction * size * 4f,
                    (axisNames != null ? axisNames[axis] : "XYZ"[axis].ToString()) + " " + LiveBuilderSceneText.FormatMeters(max[axis] - min[axis]) + " м");

            if (!EditorGUI.EndChangeCheck())
                return false;

            MoveFace(axis, sign, moved[axis] - faceCenter[axis], ref min, ref max);
            return true;
        }

        // Короткая стрелка от грани наружу: видно, что ручку тянут и куда.
        private static void DrawArrow(Vector3 faceCenter, Vector3 direction, float size)
        {
            var tip = faceCenter + direction * size * 3f;
            Handles.DrawLine(faceCenter, tip, 2f);
            Handles.ConeHandleCap(0, tip, Quaternion.LookRotation(direction), size * 1.4f, EventType.Repaint);
        }

        // Грань сдвинулась на delta: только она; с Alt — и противоположная навстречу; с Alt+Shift — все стороны в той же доле, противоположная грань стоит.
        private static void MoveFace(int axis, float sign, float delta, ref Vector3 min, ref Vector3 max)
        {
            var current = Event.current;
            var oldSize = max - min;

            if (current.shift && current.alt && oldSize[axis] > 0.0001f)
            {
                var newAxisSize = Mathf.Max(0.001f, oldSize[axis] + delta * sign);
                var scale = newAxisSize / oldSize[axis];
                var center = (min + max) / 2f;

                for (var other = 0; other < 3; other++)
                {
                    var half = oldSize[other] * scale / 2f;

                    if (other == axis)
                    {
                        if (sign > 0f)
                            max[other] = min[other] + oldSize[other] * scale;
                        else
                            min[other] = max[other] - oldSize[other] * scale;
                    }
                    else
                    {
                        min[other] = center[other] - half;
                        max[other] = center[other] + half;
                    }
                }

                return;
            }

            if (sign > 0f)
                max[axis] += delta;
            else
                min[axis] += delta;

            if (!current.alt)
                return;

            if (sign > 0f)
                min[axis] -= delta;
            else
                max[axis] -= delta;
        }
    }
}
