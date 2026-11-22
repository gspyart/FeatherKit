using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ряд проёмов по Shift + клику инструмента «Cut»: повтор на самом вырезе (<see cref="LiveBuilderArray"/>) — вдоль стены, Fill во всю сторону.
    /// Шаг и отступ — из настроек инструмента; повтор по этажам (To top) остаётся — ряд встаёт и на этажах выше.
    /// </summary>
    public static class LiveBuilderCutRow
    {
        // Между соседними проёмами ряда остаётся хотя бы столько стены, м.
        private const float MinWallBetween = 0.3f;


        public static LiveBuilderArray Repeat(GeometryCutter cutter)
        {
            var array = cutter.GetComponent<LiveBuilderArray>();

            if (array == null)
            {
                array = Undo.AddComponent<LiveBuilderArray>(cutter.gameObject);
                array.CountY = 1;
            }
            else
            {
                Undo.RecordObject(array, "Cut Row");
            }

            array.Along = LiveBuilderArrayAlong.Wall;
            array.Mode = LiveBuilderArrayMode.Fill;
            array.Length = 0f;
            array.Margin = LiveBuilderCutterSettings.RowMargin;
            array.StepX = Mathf.Max(LiveBuilderCutterSettings.RowStep, cutter.Width + MinWallBetween);

            LiveBuilderArrayWallPath.MoveToFirstPlace(array);
            return array;
        }
    }
}
