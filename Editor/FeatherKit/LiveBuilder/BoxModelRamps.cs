using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пандус — коробка с полным скосом (<see cref="BoxModelEdgeChamfer.Full"/>) верхнего ребра: скат от низа одной стороны до верха противоположной.
    /// Сделать, повернуть, узнать пандус и куда он поднимается; стороны — номерами <see cref="BoxFaces"/>, подъём — только к боку (±X, ±Z).
    /// </summary>
    public static class BoxModelRamps
    {
        private const int TopFace = 2;

        // Бока по часовой, если смотреть сверху: +Z, +X, −Z, −X.
        private static readonly int[] ClockwiseSides = { 4, 0, 5, 1 };


        // ── Пандус ───────────────────────────────────────────────────────

        // Полный скос верхнего ребра со стороны, противоположной подъёму; прежний пандус коробки убирается. Undo — у зовущего.
        public static void MakeRamp(BoxModelPart part, int risesTowardFace)
        {
            RemoveRamp(part);
            part.SetChamfer(new BoxModelEdgeChamfer(TopFace, BoxFaces.Opposite(risesTowardFace), BoxModelEdgeChamfer.Full, BoxModelEdgeChamfer.Full));
        }


        // На четверть оборота по часовой, если смотреть сверху, в той же коробке.
        public static void TurnRamp(BoxModelPart part)
        {
            if (!IsRamp(part, out var risesTowardFace))
                return;

            Undo.RecordObject(part, "Turn Ramp");
            MakeRamp(part, Clockwise(risesTowardFace));
            EditorUtility.SetDirty(part);
        }


        // Полный скос верхнего ребра на обе стороны — пандус; risesTowardFace — бок, к которому он поднимается.
        public static bool IsRamp(BoxModelPart part, out int risesTowardFace)
        {
            foreach (var side in ClockwiseSides)
                if (part.TryGetChamfer(TopFace, side, out var chamfer) && chamfer.IsFullOnFirstFace && chamfer.IsFullOnSecondFace)
                {
                    risesTowardFace = BoxFaces.Opposite(side);
                    return true;
                }

            risesTowardFace = -1;
            return false;
        }

        private static void RemoveRamp(BoxModelPart part)
        {
            while (IsRamp(part, out var risesTowardFace))
                part.RemoveChamfer(TopFace, BoxFaces.Opposite(risesTowardFace));
        }


        // ── Направление ──────────────────────────────────────────────────

        // Куда поднимается пандус, нарисованный протягиванием на плоскости planeAxis: вдоль длинной горизонтальной стороны к концу протягивания,
        // и ещё quarterTurns четвертей по часовой (Tab).
        public static int RiseFaceOf(Vector3 dragDelta, int planeAxis, int quarterTurns)
        {
            var axis = planeAxis switch
            {
                0 => 2,
                2 => 0,
                _ => Mathf.Abs(dragDelta.x) >= Mathf.Abs(dragDelta.z) ? 0 : 2
            };

            var face = BoxFaces.Index(axis, dragDelta[axis] >= 0f);

            for (var turn = 0; turn < ((quarterTurns % 4) + 4) % 4; turn++)
                face = Clockwise(face);

            return face;
        }


        public static int Clockwise(int side)
        {
            var index = System.Array.IndexOf(ClockwiseSides, side);
            return index < 0 ? side : ClockwiseSides[(index + 1) % ClockwiseSides.Length];
        }


        // «+Z» — для подписей.
        public static string SideName(int side)
        {
            return side switch
            {
                0 => "+X",
                1 => "−X",
                4 => "+Z",
                5 => "−Z",
                _ => "?"
            };
        }
    }
}
