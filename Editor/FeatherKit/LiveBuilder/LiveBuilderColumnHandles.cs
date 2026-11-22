using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки граней выделенной колонны (<see cref="LiveBuilderColumn"/>) в окне сцены — как у коробки (<see cref="LiveBuilderFaceHandles"/>):
    /// тянешь грань рамки — ходит она, противоположная стоит; размер — целым числом клеток сетки, опора едет за нижним углом. Перенос — стрелками выделенного.
    /// </summary>
    public static class LiveBuilderColumnHandles
    {
        private static readonly string[] AxisNames = { "width", "height", "depth" };


        public static void Draw(LiveBuilderColumn column)
        {
            var oldMax = column.Size;
            var min = Vector3.zero;
            var max = oldMax;

            if (!LiveBuilderFaceHandles.Draw(column.transform.localToWorldMatrix, ref min, ref max, BoxModelPartHandles.AddColor, AxisNames))
                return;

            LiveBuilderFaceHandles.SnapSizesToGrid(Vector3.zero, oldMax, ref min, ref max);
            var newSize = Vector3.Max(max - min, Vector3.one * LiveBuilderGrid.CurrentStep);

            if (min == Vector3.zero && newSize == oldMax)
                return;

            Undo.RecordObjects(new Object[] { column, column.transform }, "Resize Column");
            column.transform.position = column.transform.TransformPoint(min);
            column.Size = newSize;
            EditorUtility.SetDirty(column);
        }
    }
}
