using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Перенос пивота у построек Live Builder — объекта из коробок (<see cref="BoxModel"/>), стены по линии (<see cref="SplineWall"/>)
    /// и здания по контуру (<see cref="SplineBuilding"/>) — так, что в мире они остаются на месте. Их форма записана относительно пивота,
    /// поэтому пересчитывается вместе с ним: точки сплайна сдвигаются навстречу, коробки и всё поставленное внутрь (дети) — остаются на местах в мире.
    /// Части, которые здание собирает само, не трогаются: пересобираются по новым точкам.
    /// У объекта из коробок сдвиг — в целых миллиметрах: коробки считаются в миллиметрах, и дробный сдвиг округлился бы у каждой по-своему.
    /// Запускается из общего инструмента «Пивот» FeatherKit (<see cref="PivotMover"/>): центр, центр низа, своя точка, числа.
    /// </summary>
    public sealed class LiveBuilderPivotMover : IShapePivotMover
    {
        private const string UndoName = "Move Pivot";

        // Сдвиг меньше этого — пивот уже там, м.
        private const float MinShift = 0.0001f;


        // ── IShapePivotMover ─────────────────────────────────────────────

        public bool IsOwnerOf(Transform root)
        {
            return root.TryGetComponent<BoxModel>(out _) || root.TryGetComponent<SplineWall>(out _) || root.TryGetComponent<SplineBuilding>(out _);
        }


        public void MovePivot(Transform root, Vector3 newPivot)
        {
            var localShift = root.InverseTransformPoint(newPivot);

            if (root.TryGetComponent<BoxModel>(out _))
                localShift = RoundToMillimeters(localShift);

            if (localShift.magnitude < MinShift)
                return;

            var keptChildren = ChildrenKeptInPlace(root);
            var keptPositions = keptChildren.ConvertAll(child => child.position);

            Undo.RecordObject(root, UndoName);

            foreach (var child in keptChildren)
                Undo.RecordObject(child, UndoName);

            ShiftSplineKnots(root, -localShift);
            root.position = root.TransformPoint(localShift);

            for (var index = 0; index < keptChildren.Count; index++)
                keptChildren[index].position = keptPositions[index];

            LiveBuilderRebuildQueue.RequestCheck();
        }

        private static Vector3 RoundToMillimeters(Vector3 meters)
        {
            return new Vector3(Mathf.Round(meters.x * 1000f), Mathf.Round(meters.y * 1000f), Mathf.Round(meters.z * 1000f)) / 1000f;
        }

        // Дети, которые остаются на местах в мире: всё, кроме частей, собранных самим зданием, — они встанут по новым точкам.
        private static List<Transform> ChildrenKeptInPlace(Transform root)
        {
            var isBuilding = root.TryGetComponent<SplineBuilding>(out _);
            var kept = new List<Transform>();

            foreach (Transform child in root)
                if (!isBuilding || !IsBuiltBuildingPart(child))
                    kept.Add(child);

            return kept;
        }

        // Крыша — часть прямо в здании; группа этажа «Storey N» — с частями внутри.
        private static bool IsBuiltBuildingPart(Transform child)
        {
            if (child.TryGetComponent<SplineBuildingPart>(out _))
                return true;

            foreach (Transform grandchild in child)
                if (grandchild.TryGetComponent<SplineBuildingPart>(out _))
                    return true;

            return false;
        }

        // Все точки всех сплайнов объекта — на сдвиг, в его осях; усы точек не меняются.
        private static void ShiftSplineKnots(Transform root, Vector3 localShift)
        {
            if (!root.TryGetComponent<SplineContainer>(out var container))
                return;

            Undo.RecordObject(container, UndoName);

            foreach (var spline in container.Splines)
                for (var index = 0; index < spline.Count; index++)
                {
                    var knot = spline[index];
                    knot.Position += (float3)localShift;
                    spline.SetKnot(index, knot);
                }
        }
    }
}
