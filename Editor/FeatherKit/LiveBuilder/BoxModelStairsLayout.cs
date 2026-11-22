using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Раскладка лестницы на коробки расчёта для сборщика (<see cref="BoxModelShapeCollector"/>): одна лестница — один кусок.
    /// - вырез над маршем — только там, где достаёт голова; свои ступени он не режет
    /// - марши ступенями снизу вверх, площадка между маршами
    /// - невидимый скат через носики ступеней каждого марша — для столкновений
    /// </summary>
    internal static class BoxModelStairsLayout
    {
        // Толщина ската у лестницы из отдельных ступеней, м: тонкая полоса — под маршем остаётся место.
        private const float OpenStepsRampThickness = 0.1f;


        // Один марш лестницы: где он поперёк подъёма, откуда (низ марша — расстояние от начала лестницы по подъёму) и куда идёт (+1 — от начала, −1 — к нему),
        // с какой высоты и сколько в нём ступеней.
        private readonly struct StairsFlight
        {
            public StairsFlight(float crossMin, float width, float lowEndRun, float runSign, float baseHeight, int stepCount)
            {
                CrossMin = crossMin;
                Width = width;
                LowEndRun = lowEndRun;
                RunSign = runSign;
                BaseHeight = baseHeight;
                StepCount = stepCount;
            }


            public float CrossMin { get; }
            public float Width { get; }
            public float LowEndRun { get; }
            public float RunSign { get; }
            public float BaseHeight { get; }
            public int StepCount { get; }
        }


        // ── Лестница ─────────────────────────────────────────────────────

        // Вырез — во весь габарит лестницы от HeadroomHeight над полом до верха: ниже стены и всё вокруг целы.
        // У лестницы с площадкой первый марш идёт к дальнему концу, площадка на всю ширину, второй марш обратно, рядом с первым.
        public static void Add(BoxModelStairs stairs, Vector3 min, BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            var fit = stairs.TextureFit;
            var materials = new BoxModelSideMaterials(stairs.Material).WithFits(fit.IsDefault ? default : new BoxModelSideFits(fit));
            var size = stairs.Size;
            var riser = stairs.Height / stairs.StepCount;
            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;

            if (stairs.CutsAbove && stairs.Height > BoxModelStairs.HeadroomHeight)
            {
                var cutMin = min + Vector3.up * BoxModelStairs.HeadroomHeight;
                var cutSize = size;
                cutSize.y = stairs.Height - BoxModelStairs.HeadroomHeight;
                boxes.Add(MillimeterBox.FromMeters(cutMin, cutSize), BoxModelPartMode.Cut, stairs, materials);
            }

            if (!stairs.IsSwitchback)
            {
                var flight = new StairsFlight(min[crossAxis], stairs.Width, 0f, 1f, 0f, stairs.StepCount);
                AddFlight(stairs, min, flight, riser, materials, boxes, ramps);
                return;
            }

            var flightWidth = stairs.FlightWidth;
            var firstCross = stairs.IsFirstFlightOnOtherSide ? min[crossAxis] + stairs.Width - flightWidth : min[crossAxis];
            var secondCross = stairs.IsFirstFlightOnOtherSide ? min[crossAxis] : min[crossAxis] + stairs.Width - flightWidth;
            var landingHeight = stairs.FirstFlightStepCount * riser;

            var first = new StairsFlight(firstCross, flightWidth, stairs.FlightLength - stairs.FirstFlightStepCount * stairs.StepDepth, 1f, 0f, stairs.FirstFlightStepCount);
            AddFlight(stairs, min, first, riser, materials, boxes, ramps);
            AddLanding(stairs, min, landingHeight, materials, boxes);
            var second = new StairsFlight(secondCross, flightWidth, stairs.FlightLength, -1f, landingHeight, stairs.SecondFlightStepCount);
            AddFlight(stairs, min, second, riser, materials, boxes, ramps);
        }

        // Ступени одного марша снизу вверх и его скат.
        private static void AddFlight(BoxModelStairs stairs, Vector3 min, StairsFlight flight, float riser, BoxModelSideMaterials materials,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            var runAxis = stairs.RunAxis;
            var crossAxis = runAxis == 0 ? 2 : 0;
            var tread = stairs.StepDepth;
            var isOpen = stairs.Style == BoxModelStairsStyle.Open;

            for (var step = 0; step < flight.StepCount; step++)
            {
                var stepTop = flight.BaseHeight + (step + 1) * riser;
                var stepBottom = isOpen ? Mathf.Max(0f, stepTop - stairs.StepThickness) : 0f;
                var runA = flight.LowEndRun + flight.RunSign * step * tread;
                var runB = flight.LowEndRun + flight.RunSign * (step + 1) * tread;
                var stepMin = min;
                var stepSize = Vector3.zero;
                stepMin[runAxis] = RunToAxis(stairs, min, Mathf.Min(runA, runB), Mathf.Max(runA, runB), out var stepLength);
                stepMin[crossAxis] = flight.CrossMin;
                stepMin.y = min.y + stepBottom;
                stepSize[runAxis] = stepLength;
                stepSize[crossAxis] = flight.Width;
                stepSize.y = stepTop - stepBottom;
                boxes.Add(MillimeterBox.FromMeters(stepMin, stepSize), BoxModelPartMode.Add, stairs, materials);
            }

            ramps.Add(RampOf(stairs, min, flight, riser, tread));
        }

        // Площадка между маршами — на всю ширину у дальнего конца, верх — на верху первого марша; у отдельных ступеней — плита толщиной в ступень.
        private static void AddLanding(BoxModelStairs stairs, Vector3 min, float landingHeight, BoxModelSideMaterials materials, BoxModelCollectedBoxes boxes)
        {
            var bottom = stairs.Style == BoxModelStairsStyle.Open ? Mathf.Max(0f, landingHeight - stairs.StepThickness) : 0f;
            var landingMin = min;
            var landingSize = stairs.Size;
            landingMin[stairs.RunAxis] = RunToAxis(stairs, min, stairs.FlightLength, stairs.Length, out var landingLength);
            landingMin.y = min.y + bottom;
            landingSize[stairs.RunAxis] = landingLength;
            landingSize.y = landingHeight - bottom;
            boxes.Add(MillimeterBox.FromMeters(landingMin, landingSize), BoxModelPartMode.Add, stairs, materials);
        }

        // Скат марша: от пола за ступень до первой ступени, через носики до верха.
        // У сплошной — клин до пола и ровно до конца марша; у отдельных ступеней — тонкая полоса вдоль носиков, под лестницей можно пройти.
        private static BoxModelRampShape RampOf(BoxModelStairs stairs, Vector3 min, StairsFlight flight, float riser, float tread)
        {
            var stepCount = flight.StepCount;
            var bottomStart = new Vector2(-tread, 0f);
            var lastNose = new Vector2((stepCount - 1) * tread, stepCount * riser);
            var profile = stairs.Style == BoxModelStairsStyle.Open
                ? new[] { bottomStart, lastNose, lastNose + Vector2.down * OpenStepsRampThickness, bottomStart + Vector2.down * OpenStepsRampThickness }
                : new[] { bottomStart, lastNose, new Vector2(stepCount * tread, stepCount * riser), new Vector2(stepCount * tread, -flight.BaseHeight) };

            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;
            var corners = new Vector3[8];

            for (var edge = 0; edge < 2; edge++)
                for (var index = 0; index < profile.Length; index++)
                {
                    var corner = min;
                    corner[stairs.RunAxis] = RunPointToAxis(stairs, min, flight.LowEndRun + flight.RunSign * profile[index].x);
                    corner.y = min.y + flight.BaseHeight + profile[index].y;
                    corner[crossAxis] = flight.CrossMin + edge * flight.Width;
                    corners[edge * profile.Length + index] = corner;
                }

            return new BoxModelRampShape(corners);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Отрезок марша [from, to] от начала подъёма — в координату по оси объекта: возвращает меньший край и длину.
        private static float RunToAxis(BoxModelStairs stairs, Vector3 min, float from, float to, out float length)
        {
            length = to - from;
            return Mathf.Min(RunPointToAxis(stairs, min, from), RunPointToAxis(stairs, min, to));
        }

        private static float RunPointToAxis(BoxModelStairs stairs, Vector3 min, float run)
        {
            var axisMin = min[stairs.RunAxis];
            return stairs.RunsTowardsPlus ? axisMin + run : axisMin + stairs.Length - run;
        }
    }
}
