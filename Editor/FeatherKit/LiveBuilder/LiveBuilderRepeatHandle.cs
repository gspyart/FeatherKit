using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручка повтора в окне сцены: стрелка у последней копии выделенного повтора. Тянешь вдоль шага — копий больше или меньше,
    /// рядом надпись «× 5». Всё с Undo.
    /// </summary>
    public static class LiveBuilderRepeatHandle
    {
        private static readonly Color HandleColor = new(0.75f, 0.45f, 1f);

        // Пока тянешь: сколько было копий и где была ручка в начале — число считаем от них.
        private static bool isDragging;
        private static int countAtDragStart;
        private static Vector3 positionAtDragStart;
        private static Vector3 handlePosition;


        public static void DrawForSelection()
        {
            var repeat = LiveBuilderRepeatActions.SelectedRepeat();

            if (isDragging && GUIUtility.hotControl == 0)
                isDragging = false;

            if (repeat == null || repeat.Step.sqrMagnitude < 0.0001f)
                return;

            var model = repeat.GetComponentInParent<BoxModel>();

            if (model == null || !TryGetFirstCopyCenter(model, repeat, out var firstCenter))
                return;

            var localStep = repeat.Step;
            var lastCenter = firstCenter + localStep * (repeat.Count - 1);
            var worldPosition = model.transform.TransformPoint(lastCenter + localStep.normalized * (localStep.magnitude * 0.5f));
            var worldDirection = model.transform.TransformDirection(localStep.normalized);

            Handles.color = HandleColor;
            var position = isDragging ? handlePosition : worldPosition;

            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(position, worldDirection, HandleUtility.GetHandleSize(position) * 0.9f, Handles.ArrowHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                if (!isDragging)
                {
                    isDragging = true;
                    countAtDragStart = repeat.Count;
                    positionAtDragStart = position;
                }

                handlePosition = moved;
                var stepsMoved = Mathf.RoundToInt(Vector3.Dot(moved - positionAtDragStart, worldDirection) / model.transform.TransformVector(localStep).magnitude);
                var newCount = Mathf.Clamp(countAtDragStart + stepsMoved, 1, BoxModelRepeat.MaxCount);

                if (newCount != repeat.Count)
                {
                    Undo.RecordObject(repeat, "Change Repeat Count");
                    repeat.Count = newCount;
                }
            }

            LiveBuilderSceneText.DrawLabel(position, "× " + repeat.Count);
        }

        // Середина первой копии: коробки группы собраны без сдвигов повторов — ровно там, где стоят в сцене.
        private static bool TryGetFirstCopyCenter(BoxModel model, BoxModelRepeat repeat, out Vector3 center)
        {
            var firstCopyBoxes = BoxModelShapeCollector.CollectFirstCopy(model, repeat.transform);
            var bounds = new Bounds();

            for (var index = 0; index < firstCopyBoxes.Count; index++)
            {
                var box = firstCopyBoxes[index].Box;
                var boxBounds = new Bounds();
                boxBounds.SetMinMax(box.MinInMeters, box.MaxInMeters);

                if (index == 0)
                    bounds = boxBounds;
                else
                    bounds.Encapsulate(boxBounds);
            }

            center = bounds.center;
            return firstCopyBoxes.Count > 0;
        }
    }
}
