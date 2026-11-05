using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Направляющая, к которой прилипла точка линии: откуда она идёт, куда, как подписана и каким цветом нарисована.</summary>
    public readonly struct LiveBuilderLineGuide
    {
        public LiveBuilderLineGuide(Vector3 origin, Vector3 direction, string label, Color color)
        {
            Origin = origin;
            Direction = direction;
            Label = label;
            Color = color;
        }


        public Vector3 Origin { get; }

        // В мире, длиной 1.
        public Vector3 Direction { get; }

        public string Label { get; }
        public Color Color { get; }
    }


    /// <summary>
    /// Направляющие от последней точки линии, как подсказки SketchUp: точка под мышью прилипает к лучу из последней точки — по оси X или Z
    /// плоскости рисования (цвет оси), под 45°, параллельно или перпендикулярно прошлому отрезку, если до луча на экране меньше
    /// <see cref="SnapPixels"/>. Длина вдоль луча — по шагу сетки. С Ctrl — любой угол с шагом 15°. Точки построек (<see cref="LiveBuilderPointSnap"/>)
    /// сильнее направляющих: угол здания важнее оси. S выключает их вместе с прилипанием к точкам. Без окна сцены (проверки) не прилипает.
    /// </summary>
    public static class LiveBuilderLineGuides
    {
        // Ближе этого к лучу на экране — прилипает, пикс.
        public const float SnapPixels = 10f;

        // Шаг угла с Ctrl, градусы.
        public const float AngleStep = 15f;

        // Как далеко рисуется пунктир направляющей за точкой, м.
        private const float GuideTail = 2f;

        private static readonly Color DiagonalColor = new(0.95f, 0.8f, 0.25f);
        private static readonly Color SegmentColor = new(0.85f, 0.4f, 0.95f);


        // ── Привязка ─────────────────────────────────────────────────────

        // Точка мира на плоскости рисования (frameToWorld, ось нормали planeAxis) — к ближайшей направляющей из last; previous — точка перед last
        // (для «параллельно» и «перпендикулярно»), нет её — null. Прилипла — точка на луче с длиной по сетке и сама направляющая.
        public static bool TrySnap(Matrix4x4 frameToWorld, int planeAxis, Vector3 last, Vector3? previous, Vector3 point, out Vector3 snapped, out LiveBuilderLineGuide guide)
        {
            snapped = point;
            guide = default;

            // S выключает всё «умное» прилипание сразу — и к точкам, и к направляющим.
            if (!LiveBuilderPointSnap.IsEnabled || Camera.current == null || Event.current == null)
                return false;

            return TrySnap(frameToWorld, planeAxis, last, previous, point, Event.current.control, HandleUtility.WorldToGUIPoint, out snapped, out guide);
        }


        // То же с явным переводом точки мира в точку экрана и шагом угла — для проверок без окна сцены.
        public static bool TrySnap(Matrix4x4 frameToWorld, int planeAxis, Vector3 last, Vector3? previous, Vector3 point, bool isAngleStep,
            System.Func<Vector3, Vector2> worldToScreen, out Vector3 snapped, out LiveBuilderLineGuide guide)
        {
            snapped = point;
            guide = default;

            if ((point - last).sqrMagnitude < 0.0001f)
                return false;

            // Оси плоскости по порядку, как стороны прямоугольника: на полу X и Z, на стене — по горизонтали и вверх.
            var (firstAxis, secondAxis) = planeAxis switch
            {
                0 => (2, 1),
                1 => (0, 2),
                _ => (0, 1)
            };

            var axisU = ((Vector3)frameToWorld.GetColumn(firstAxis)).normalized;
            var axisV = ((Vector3)frameToWorld.GetColumn(secondAxis)).normalized;
            var mouse = worldToScreen(point);
            var bestPixels = SnapPixels;
            var isFound = false;
            var foundGuide = default(LiveBuilderLineGuide);

            void Consider(Vector3 direction, string label, Color color)
            {
                var along = Vector3.Dot(point - last, direction);

                if (along <= 0f)
                {
                    direction = -direction;
                    along = -along;
                }

                var pixels = (worldToScreen(last + direction * along) - mouse).magnitude;

                if (pixels >= bestPixels)
                    return;

                bestPixels = pixels;
                foundGuide = new LiveBuilderLineGuide(last, direction, label, color);
                isFound = true;
            }

            if (isAngleStep)
            {
                ConsiderAngleStep(axisU, axisV, last, point, Consider);
            }
            else
            {
                var nameU = AxisName(frameToWorld, firstAxis);
                var nameV = AxisName(frameToWorld, secondAxis);
                Consider(axisU, "по оси " + nameU, AxisColor(nameU));
                Consider(axisV, "по оси " + nameV, AxisColor(nameV));
                Consider((axisU + axisV).normalized, "45°", DiagonalColor);
                Consider((axisU - axisV).normalized, "45°", DiagonalColor);

                if (previous is { } before && (last - before).sqrMagnitude > 0.0001f)
                {
                    var segment = Vector3.ProjectOnPlane(last - before, Vector3.Cross(axisU, axisV)).normalized;
                    Consider(segment, "продолжение отрезка", SegmentColor);
                    Consider(Vector3.Cross(Vector3.Cross(axisU, axisV), segment).normalized, "перпендикулярно отрезку", SegmentColor);
                }
            }

            if (!isFound)
                return false;

            guide = foundGuide;
            var length = LiveBuilderGrid.Snap(Vector3.Dot(point - last, guide.Direction), 0);
            snapped = last + guide.Direction * Mathf.Max(length, 0f);
            return true;
        }

        // С Ctrl — направление, округлённое до шага угла от оси U; подпись — сам угол.
        private static void ConsiderAngleStep(Vector3 axisU, Vector3 axisV, Vector3 last, Vector3 point, System.Action<Vector3, string, Color> consider)
        {
            var offset = point - last;
            var angle = Mathf.Atan2(Vector3.Dot(offset, axisV), Vector3.Dot(offset, axisU)) * Mathf.Rad2Deg;
            var stepped = Mathf.Round(angle / AngleStep) * AngleStep;
            var radians = stepped * Mathf.Deg2Rad;
            var label = Mathf.Repeat(stepped, 360f).ToString("0") + "°";
            consider((axisU * Mathf.Cos(radians) + axisV * Mathf.Sin(radians)).normalized, label, DiagonalColor);
        }

        // «X», «Y» или «Z» мира — по тому, куда смотрит ось рамки; рамка повёрнута — её буква.
        private static string AxisName(Matrix4x4 frameToWorld, int axis)
        {
            var direction = ((Vector3)frameToWorld.GetColumn(axis)).normalized;

            if (Mathf.Abs(direction.x) > 0.999f)
                return "X";

            if (Mathf.Abs(direction.z) > 0.999f)
                return "Z";

            return Mathf.Abs(direction.y) > 0.999f ? "Y" : "XYZ"[axis].ToString();
        }


        // Цвет оси, как у ручек Unity: X — красный, Y — зелёный, Z — синий.
        private static Color AxisColor(string axisName)
        {
            return axisName switch
            {
                "X" => Handles.xAxisColor,
                "Y" => Handles.yAxisColor,
                _ => Handles.zAxisColor
            };
        }


        // ── Рисование ────────────────────────────────────────────────────

        // Пунктир направляющей от её начала через точку и чуть дальше.
        public static void Draw(LiveBuilderLineGuide guide, Vector3 point)
        {
            var end = point + guide.Direction * GuideTail;
            var previousColor = Handles.color;
            Handles.color = guide.Color;
            Handles.DrawDottedLine(guide.Origin, end, 3f);
            Handles.color = previousColor;
        }
    }
}
