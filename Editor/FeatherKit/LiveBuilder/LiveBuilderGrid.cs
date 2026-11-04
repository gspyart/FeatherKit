using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Шаг сетки инструмента «Live Builder» — тот же, что у сетки Unity в окне сцены (панель Grid and Snap): какие клетки видишь, к тем и прилипает.
    /// Края коробок привязываются к сетке всегда, независимо от кнопки привязки Unity.
    /// Шаг меняют кнопки окна Live Builder и клавиши [ и ] — по ряду привычных шагов.
    /// </summary>
    public static class LiveBuilderGrid
    {
        // Меньше этого шаг не бывает — защита от нулевого размера клеток, м.
        private const float MinStep = 0.01f;

        // Шаги для кнопок и клавиш [ ]: от мелочей до клетки набора, м.
        public static readonly float[] Steps = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };


        // Шаг по горизонтали сейчас, м.
        public static float CurrentStep => StepOn(0);


        // Шаг по оси объекта: 0 — X, 1 — Y, 2 — Z, м.
        public static float StepOn(int axis)
        {
            return Mathf.Max(MinStep, EditorSnapSettings.gridSize[axis]);
        }


        public static float Snap(float meters, int axis)
        {
            var step = StepOn(axis);
            return Mathf.Round(meters / step) * step;
        }


        public static Vector3 Snap(Vector3 meters)
        {
            return new Vector3(Snap(meters.x, 0), Snap(meters.y, 1), Snap(meters.z, 2));
        }


        // ── Смена шага ───────────────────────────────────────────────────

        // Один шаг на все оси — так же, как его ставит панель Grid and Snap.
        public static void SetStep(float step)
        {
            EditorSnapSettings.gridSize = Vector3.one * Mathf.Max(MinStep, step);
            SceneView.RepaintAll();
        }


        // Следующий шаг из ряда: крупнее или мельче текущего.
        public static void ChangeStep(bool isLarger)
        {
            var current = CurrentStep;

            if (isLarger)
            {
                foreach (var step in Steps)
                    if (step > current + 0.0001f)
                    {
                        SetStep(step);
                        return;
                    }
            }
            else
            {
                for (var index = Steps.Length - 1; index >= 0; index--)
                    if (Steps[index] < current - 0.0001f)
                    {
                        SetStep(Steps[index]);
                        return;
                    }
            }
        }
    }
}
