using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одна копия выреза (<see cref="GeometryCutter"/>) — копия повторов над ним (<see cref="LiveBuilderArrayCopyPoses.CutterCopiesOf"/>) и где форма в мире.
    /// Все копии выреза — одним списком для вычитания, подписи, рамки, ручек и выбора кликом.
    /// </summary>
    public readonly struct GeometryCutterCopy
    {
        public GeometryCutterCopy(GeometryCutter cutter, Vector3 repeatOffset)
        {
            Cutter = cutter;
            RepeatOffset = repeatOffset;

            // Без сдвига повтора — матрица самого выреза, до бита: меши построек без повторов те же.
            var shapeToWorld = GeometryCutterOutline.ShapeToWorld(cutter);
            ToWorld = repeatOffset == Vector3.zero ? shapeToWorld : Matrix4x4.Translate(repeatOffset) * shapeToWorld;
        }


        public GeometryCutter Cutter { get; }

        // Сдвиг копии повторов в мире; без повторов — ноль.
        public Vector3 RepeatOffset { get; }

        // Форма этой копии в мире.
        public Matrix4x4 ToWorld { get; }


        // Все копии по порядку повторов; без повторов — один сам вырез.
        public static List<GeometryCutterCopy> AllOf(GeometryCutter cutter)
        {
            var repeatOffsets = LiveBuilderArrayCopyPoses.CutterCopiesOf(cutter);
            var copies = new List<GeometryCutterCopy>(repeatOffsets.Count);

            foreach (var repeatOffset in repeatOffsets)
                copies.Add(new GeometryCutterCopy(cutter, repeatOffset));

            return copies;
        }
    }
}
