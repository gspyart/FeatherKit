using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Поставить повтор (<see cref="LiveBuilderArray"/>) выделенному одним действием с Undo — меню «+ Add» секции Array панели:
    /// - вверх по этажам: в здании — до его верха, иначе — на три этажа уровня
    /// - по X или Z с шагом по размеру выделенного; рядом вдоль стены или контура здания во всю сторону
    /// </summary>
    public static class LiveBuilderArrayActions
    {
        private const int NewCount = 3;

        // Между соседними копиями ряда вдоль стены остаётся хотя бы столько, м.
        private const float MinGapAlongWall = 0.3f;


        // Вверх: в здании — до его верха (у лестницы без верхнего этажа), иначе — на три этажа уровня.
        public static void RepeatUp(GameObject selected)
        {
            var building = SplineBuildingStoreys.BuildingOfStoreyContent(selected.transform);

            if (building != null)
            {
                SplineBuildingInteriorPlacement.RepeatOnStoreysAbove(selected.transform, building, selected.GetComponent<BoxModelStairs>() != null);
                return;
            }

            var array = AddArray(selected);
            array.CountY = NewCount;
            array.StepYIsStoreyHeight = true;
        }


        // По оси X или Z: шаг — размер выделенного по этой оси с запасом в клетку сетки.
        public static void AddAlongAxis(GameObject selected, int axis)
        {
            var array = AddArray(selected);
            var step = StepAround(SizeOf(selected)[axis]);

            if (axis == 0)
            {
                array.CountX = NewCount;
                array.StepX = step;
            }
            else
            {
                array.CountZ = NewCount;
                array.StepZ = step;
            }
        }


        // Ряд вдоль стены или контура здания во всю сторону, оригинал — на первое место.
        public static void AddAlongWall(GameObject selected)
        {
            var array = AddArray(selected);
            array.Along = LiveBuilderArrayAlong.Wall;
            array.Mode = LiveBuilderArrayMode.Fill;
            array.Length = 0f;
            array.StepX = Mathf.Max(LiveBuilderGrid.CurrentStep, SizeOf(selected).x + MinGapAlongWall);
            Undo.RecordObject(selected.transform, "Add Array");
            LiveBuilderArrayWallPath.MoveToFirstPlace(array);
        }


        // Без копий по всем осям — каждый способ ставит свою ось.
        private static LiveBuilderArray AddArray(GameObject selected)
        {
            var array = Undo.AddComponent<LiveBuilderArray>(selected);
            array.CountX = 1;
            array.CountY = 1;
            array.CountZ = 1;
            array.StepYIsStoreyHeight = false;
            return array;
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Размер выделенного в его осях: у коробки и выреза — свой, у остального — по рендерам под ним, а без них — клетка сетки.
        private static Vector3 SizeOf(GameObject selected)
        {
            if (selected.TryGetComponent<BoxModelPart>(out var part))
                return part.Size;

            if (selected.TryGetComponent<GeometryCutter>(out var cutter))
                return new Vector3(cutter.Width, cutter.Height, cutter.Depth);

            var renderers = selected.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return Vector3.one * LiveBuilderGrid.CurrentStep;

            var bounds = renderers[0].bounds;

            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return selected.transform.InverseTransformVector(bounds.size);
        }

        // Шаг повтора по размеру выделенного — с запасом в клетку сетки, чтобы копии не слиплись.
        private static float StepAround(float size)
        {
            var grid = LiveBuilderGrid.CurrentStep;
            return Mathf.Max(grid, Mathf.Ceil(Mathf.Abs(size) / grid) * grid + grid);
        }
    }
}
