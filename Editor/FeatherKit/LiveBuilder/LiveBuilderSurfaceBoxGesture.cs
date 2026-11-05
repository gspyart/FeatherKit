using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Коробка» на поверхности, которая не по осям объекта, — на косой стене, повёрнутом здании: коробки объекта поворачиваться не умеют,
    /// поэтому здесь своё. Прямоугольник ложится точно на поверхность, мышью — глубина по её нормали, клик — готово.
    /// Наружу — новый объект из коробок, повёрнутый по поверхности; внутрь — вырез (<see cref="GeometryCutter"/>) на эту глубину.
    /// Режим «Добавить» или «Вырезать» в окне решает сам, в «Авто» — куда тянешь. Новое ложится в постройку под прямоугольником.
    /// </summary>
    public sealed class LiveBuilderSurfaceBoxGesture
    {
        // Отклонение от осей объекта больше этого — поверхность косая, градусы.
        private const float AngledThresholdDegrees = 1f;

        private readonly LiveBuilderSurfaceRectangle rectangle = new();
        private bool isChoosingDepth;
        private Vector2 depthMouseStart;

        // Глубина по нормали поверхности: плюс — наружу, минус — внутрь, м.
        private float depth;


        public bool IsActive => rectangle.IsActive;

        public bool IsChoosingDepth => rectangle.IsActive && isChoosingDepth;

        public string Hint => !isChoosingDepth
            ? "Косая поверхность: тяни прямоугольник прямо по ней. Отпусти — дальше глубина"
            : "Мышь от поверхности — коробка наружу, внутрь — проём: " + LiveBuilderSceneText.FormatMeters(Mathf.Abs(depth)) + " м. Клик — готово, Esc — отмена";

        private bool IsCutting => BoxModelDrawModeSetting.Current switch
        {
            BoxModelDrawMode.Add => false,
            BoxModelDrawMode.Cut => true,
            _ => depth < 0f
        };

        // Глубина, как встанет: в «Добавить» — всегда наружу, в «Вырезать» — всегда внутрь, в «Авто» — куда тянешь.
        private float ResultDepth => BoxModelDrawModeSetting.Current switch
        {
            BoxModelDrawMode.Add => Mathf.Abs(depth),
            BoxModelDrawMode.Cut => -Mathf.Abs(depth),
            _ => depth
        };


        // ── Косая поверхность ────────────────────────────────────────────

        // Поверхность под мышью не по осям объекта, в котором «Коробка» нарисовала бы коробку.
        public static bool IsAngledSurface(Ray worldRay, bool isForNewModel)
        {
            if (!LiveBuilderSurfaceRectangle.TryFindSurface(worldRay, out var hit) || !BoxModelDrawPlaneFinder.TryFind(worldRay, isForNewModel, out var plane, out _))
                return false;

            var best = 0f;

            for (var axis = 0; axis < 3; axis++)
                best = Mathf.Max(best, Mathf.Abs(Vector3.Dot(hit.normal, ((Vector3)plane.FrameToWorld.GetColumn(axis)).normalized)));

            return best < Mathf.Cos(AngledThresholdDegrees * Mathf.Deg2Rad);
        }


        // ── Жест ─────────────────────────────────────────────────────────

        public bool TryBegin(Ray worldRay)
        {
            isChoosingDepth = false;
            depth = 0f;
            return rectangle.TryBegin(worldRay);
        }


        public void UpdateRectangle(Ray worldRay)
        {
            rectangle.Update(worldRay);
        }


        // Отпустил мышь — прямоугольник готов, дальше глубина. Не протянул — ничего.
        public void FinishRectangle(Vector2 mousePosition)
        {
            if (!rectangle.HasArea)
            {
                Cancel();
                return;
            }

            isChoosingDepth = true;
            depthMouseStart = mousePosition;
        }


        // Мышь от точки, где отпустил, — глубина по нормали, по сетке.
        public void UpdateDepth(Vector2 mousePosition)
        {
            if (!IsChoosingDepth)
                return;

            var center = rectangle.PointAt(rectangle.Min + rectangle.Size / 2f);
            var translation = HandleUtility.CalcLineTranslation(depthMouseStart, mousePosition, center, rectangle.Normal);
            var step = LiveBuilderGrid.CurrentStep;
            depth = Mathf.Round(translation / step) * step;
        }


        // Клик — коробка или вырез готов и выделен. Глубина ноль — ничего.
        public void Commit()
        {
            if (Mathf.Abs(depth) > 0.0001f)
                Selection.activeGameObject = IsCutting ? CreateCutter() : CreateBox();

            Cancel();
        }


        public void Cancel()
        {
            rectangle.Cancel();
            isChoosingDepth = false;
            depth = 0f;
        }


        // ── Что встаёт ───────────────────────────────────────────────────

        // Новый объект, повёрнутый по поверхности: его оси — оси прямоугольника, Z — внутрь поверхности; коробка — наружу на глубину.
        private GameObject CreateBox()
        {
            var size = rectangle.Size;
            var min = rectangle.Min;
            var thickness = Mathf.Abs(depth);
            var model = BoxModelFactory.CreateModel(rectangle.Origin);
            model.transform.rotation = rectangle.Rotation;

            if (rectangle.SurfaceOwner != null)
                model.transform.SetParent(rectangle.SurfaceOwner, true);

            BoxModelFactory.CreatePart(model, new Vector3(min.x, min.y, -thickness), new Vector3(min.x + size.x, min.y + size.y, 0f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            return model.gameObject;
        }

        // Вырез на глубину от поверхности: опора — низ посередине на поверхности, половина выреза снаружи ничего не режет.
        // В стене по линии и здании — едет за их точками и следит за этажами, как проём.
        private GameObject CreateCutter()
        {
            var size = rectangle.Size;
            var bottom = rectangle.PointAt(new Vector2(rectangle.Min.x + size.x / 2f, rectangle.Min.y));
            var cutter = BoxModelFactory.CreateCutter(rectangle.SurfaceOwner, bottom, rectangle.Rotation, size, Mathf.Abs(depth) * 2f);
            LiveBuilderCutterGesture.AttachToOwner(cutter, rectangle.Normal);
            return cutter.gameObject;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Прямоугольник на поверхности и углы, прилипшие к краю грани; при выборе глубины — коробка до неё: зелёная наружу, оранжевая — вырез.
        public void DrawPreview()
        {
            if (!isChoosingDepth)
                rectangle.DrawEdgeMarkers();

            if (!rectangle.IsActive || !rectangle.HasArea)
                return;

            var min = rectangle.Min;
            var size = rectangle.Size;
            var corners = new[] { min, new Vector2(min.x + size.x, min.y), min + size, new Vector2(min.x, min.y + size.y) };
            var offset = rectangle.Normal * ResultDepth;

            Handles.color = !isChoosingDepth ? BoxModelPartHandles.AddColor : IsCutting ? GeometryCutterGizmos.CutterColor : BoxModelPartHandles.AddColor;

            for (var index = 0; index < 4; index++)
            {
                var start = rectangle.PointAt(corners[index]);
                var end = rectangle.PointAt(corners[(index + 1) % 4]);
                Handles.DrawLine(start, end, 2f);

                if (!isChoosingDepth)
                    continue;

                Handles.DrawLine(start + offset, end + offset, 2f);
                Handles.DrawLine(start, start + offset, 2f);
            }

            var label = LiveBuilderSceneText.FormatMeters(size.x) + " × " + LiveBuilderSceneText.FormatMeters(size.y) + " м" +
                        (isChoosingDepth ? " × " + LiveBuilderSceneText.FormatMeters(Mathf.Abs(depth)) + " м · " + (IsCutting ? "проём" : "коробка") : "");
            LiveBuilderSceneText.DrawLabel(rectangle.PointAt(min + size) + offset, label);
        }
    }
}
