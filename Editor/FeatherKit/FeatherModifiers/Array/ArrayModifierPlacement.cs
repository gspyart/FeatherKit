using System.Collections.Generic;
using FeatherKit.LevelDesign;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Одна копия массива: её номер (у самого объекта — 1), место и поворот относительно объекта — в его осях, в метрах.</summary>
    public readonly struct ArrayCopyPose
    {
        public ArrayCopyPose(int number, Vector3 position, Quaternion rotation)
        {
            Number = number;
            Position = position;
            Rotation = rotation;
        }


        public int Number { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }


    /// <summary>
    /// Где стоят копии <see cref="ArrayModifier"/> относительно самого объекта — он копия №1, в осях объекта без его масштаба.
    /// Сетка нумерует копии вдоль X, потом вверх по Y, потом по Z — у окон фасада номера идут вдоль стены, этаж за этажом.
    /// Круг — вокруг оси Y, объект — первая точка круга, центр — напротив его «лица», если копии смотрят в центр.
    /// Пропущенные номера — мест нет, номера остальных те же; объект пропустить нельзя.
    /// </summary>
    public static class ArrayModifierPlacement
    {
        // Сколько копий по настройкам, вместе с самим объектом и пропущенными.
        public static int TotalCountOf(ArrayModifier array)
        {
            var counts = array.Counts;
            return array.Layout == ArrayModifierLayout.Circle ? array.CircleCount : counts.x * counts.y * counts.z;
        }


        // Шаг сетки по осям объекта: от опоры до опоры или от края копии — её размер плюс зазор.
        public static Vector3 GridStepOf(ArrayModifier array, Vector3 copySize)
        {
            return array.StepsFromSize ? copySize + array.Gap : array.Step;
        }


        // Места всех копий, кроме самого объекта и пропущенных; больше предела — не считает вовсе.
        public static List<ArrayCopyPose> CopyPosesOf(ArrayModifier array, Vector3 copySize)
        {
            var poses = AllPosesOf(array, copySize);
            poses.RemoveAll(pose => pose.Number == 1);
            return poses;
        }


        // Места всех копий вместе с самим объектом — для точек в окне сцены.
        public static List<ArrayCopyPose> AllPosesOf(ArrayModifier array, Vector3 copySize)
        {
            var poses = new List<ArrayCopyPose>();

            if (TotalCountOf(array) > ArrayModifier.MaxCopies)
                return poses;

            var skipped = new HashSet<int>(array.SkippedCopies);
            skipped.Remove(1);

            if (array.Layout == ArrayModifierLayout.Circle)
                AddCirclePoses(array, skipped, poses);
            else
                AddGridPoses(array, GridStepOf(array, copySize), skipped, poses);

            return poses;
        }

        private static void AddGridPoses(ArrayModifier array, Vector3 step, HashSet<int> skipped, List<ArrayCopyPose> poses)
        {
            var counts = array.Counts;
            var number = 1;

            for (var z = 0; z < counts.z; z++)
                for (var y = 0; y < counts.y; y++)
                    for (var x = 0; x < counts.x; x++, number++)
                        if (!skipped.Contains(number))
                            poses.Add(new ArrayCopyPose(number, Vector3.Scale(new Vector3(x, y, z), step), Quaternion.identity));
        }

        // Места по кругу — относительно первой копии: она и есть объект.
        private static void AddCirclePoses(ArrayModifier array, HashSet<int> skipped, List<ArrayCopyPose> poses)
        {
            var count = array.CircleCount;
            CirclePose(array, 0, out var firstPosition, out var firstRotation);
            var toFirst = Quaternion.Inverse(firstRotation);

            for (var index = 0; index < count; index++)
            {
                if (skipped.Contains(index + 1))
                    continue;

                CirclePose(array, index, out var position, out var rotation);
                poses.Add(new ArrayCopyPose(index + 1, toFirst * (position - firstPosition), toFirst * rotation));
            }
        }


        // Центр круга относительно объекта — для дуги и ручки радиуса.
        public static Vector3 CircleCenterOf(ArrayModifier array)
        {
            CirclePose(array, 0, out var firstPosition, out var firstRotation);
            return Quaternion.Inverse(firstRotation) * -firstPosition;
        }


        // Место копии на круге вокруг центра: полный круг — поровну, дуга — от одного конца до другого.
        private static void CirclePose(ArrayModifier array, int index, out Vector3 position, out Quaternion rotation)
        {
            var count = array.CircleCount;
            var isFullCircle = array.Arc >= 359.999f;
            var angleStep = isFullCircle ? 360f / count : count > 1 ? array.Arc / (count - 1) : 0f;
            var direction = Quaternion.Euler(0f, index * angleStep, 0f) * Vector3.back;

            position = direction * array.Radius;
            rotation = array.FacesCenter ? Quaternion.LookRotation(-direction, Vector3.up) : Quaternion.identity;
        }
    }
}
