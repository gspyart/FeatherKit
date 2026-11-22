using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вложенный объект «Kept From Cuts»: туда встаёт стена, нарисованная внутри пустоты комнаты или выреза объекта, — вырезы родителя её не режут.
    /// - режет ли вырез объекта то, что поставят в эту точку этого родителя: для подсказки заранее и для самой стены
    /// - найти такой объект у родителя или создать, переложить в него с Undo
    /// Только у новых данных: у старых пустота комнаты стены и так не режет.
    /// </summary>
    public static class LiveBuilderKeptFromCuts
    {
        public const string GroupName = "Kept From Cuts";

        // Точку пола поднимаем на столько: пустота комнаты начинается ровно на полу, м.
        private const float ProbeLift = 0.1f;


        // ── Внутри выреза ли ─────────────────────────────────────────────

        // Стену у родителя parent в точке пола worldPoint режет вырез его объекта: точка внутри выреза, и вырез достаёт стену там.
        public static bool IsInsideCut(Transform parent, Vector3 worldPoint)
        {
            var model = parent != null ? LiveBuilderGroups.GroupOf(parent) : null;

            if (model == null || LiveBuilderCutRules.IsLegacy(model))
                return false;

            var probe = MillimeterBox.ToMillimeters(model.transform.InverseTransformPoint(worldPoint + Vector3.up * ProbeLift));

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Mode == BoxModelPartMode.Cut && box.CutSource.CutsWalls && IsInside(box.Box, probe) && ReachesWallUnder(box.CutSource, parent))
                    return true;

            return false;
        }

        private static bool IsInside(MillimeterBox box, Vector3Int point)
        {
            return point.x > box.Min.x && point.x < box.Max.x && point.y > box.Min.y && point.y < box.Max.y && point.z > box.Min.z && point.z < box.Max.z;
        }

        // То же, что LiveBuilderCutRules.Reaches, для стены, которой ещё нет: она будет лежать прямо в parent.
        private static bool ReachesWallUnder(LiveBuilderCutSource cut, Transform parent)
        {
            if (cut.Scope == GeometryCutterScope.EverythingItTouches || cut.OwnConstruction == null)
                return true;

            return parent.IsChildOf(cut.OwnConstruction) && LiveBuilderGroups.GroupOf(parent) == cut.OwnModel;
        }


        // ── Переложить ───────────────────────────────────────────────────

        // Постройка в точке пола внутри выреза — в «Kept From Cuts» своего родителя. Возвращает, создан ли для неё новый такой объект.
        public static bool MoveIfInsideCut(Transform construction, Vector3 worldPoint)
        {
            var parent = construction.parent;

            if (!IsInsideCut(parent, worldPoint))
                return false;

            var group = parent.Find(GroupName);
            var isCreated = group == null;

            if (isCreated)
            {
                var keptGroup = BoxModelFactory.CreateGroup(parent);
                keptGroup.name = GroupName;
                group = keptGroup.transform;
            }

            Undo.SetTransformParent(construction, group, true, "Keep From Cuts");
            return isCreated;
        }
    }
}
