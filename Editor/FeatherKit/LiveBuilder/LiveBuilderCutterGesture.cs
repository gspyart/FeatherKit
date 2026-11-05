using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Проём своего размера в инструменте «Проём»: протянул прямоугольник по любой поверхности, хоть по косой стене, — встаёт вырез (<see cref="GeometryCutter"/>)
    /// по её нормали. Насквозь — на толщину стены с запасом, ниша — на свою глубину. Форма — прямоугольник, арка или круг из настроек.
    /// Вырез ложится в стену, здание или объект из коробок, которые режет: едет вместе с ними; в стене по линии и здании — ещё и за их точками.
    /// </summary>
    public sealed class LiveBuilderCutterGesture
    {
        // Запас насквозь с каждой стороны, м: грань выреза вровень с гранью стены оставила бы плёнку.
        public const float ThroughMargin = 0.05f;

        // Круче этого (доля вертикали в нормали) — уже пол или потолок, а не стена: за линией стены такой вырез не едет.
        private const float WallNormalMaxVertical = 0.7f;

        private readonly LiveBuilderSurfaceRectangle rectangle = new();


        public bool IsActive => rectangle.IsActive;

        public string Hint => "Тяни — размер проёма. Отпусти — готово. Esc — отмена";


        // ── Жест ─────────────────────────────────────────────────────────

        public bool TryBegin(Ray worldRay)
        {
            return rectangle.TryBegin(worldRay);
        }


        public void Update(Ray worldRay)
        {
            rectangle.Update(worldRay);
        }


        // Отпустил мышь — вырез готов и выделен. Не протянул — ничего.
        public void Commit()
        {
            if (rectangle.HasArea)
                Selection.activeGameObject = CreateCutter().gameObject;

            Cancel();
        }


        public void Cancel()
        {
            rectangle.Cancel();
        }


        // ── Вырез ────────────────────────────────────────────────────────

        // Опора — низ посередине: насквозь — в середине толщины, ниша — на поверхности (половина выреза снаружи ничего не режет).
        private GeometryCutter CreateCutter()
        {
            var size = rectangle.Size;
            var min = rectangle.Min;
            var bottomOnSurface = rectangle.PointAt(new Vector2(min.x + size.x / 2f, min.y));
            var isThrough = LiveBuilderCutterSettings.IsThrough;
            var thickness = isThrough ? rectangle.ThicknessAt(rectangle.PointAt(min + size / 2f)) : 0f;

            var cutter = BoxModelFactory.CreateCutter(rectangle.SurfaceOwner, bottomOnSurface - rectangle.Normal * (thickness / 2f), rectangle.Rotation,
                size, isThrough ? thickness + ThroughMargin * 2f : LiveBuilderCutterSettings.NicheDepth * 2f);
            cutter.name = "Opening";
            cutter.RevealMaterial = LiveBuilderCutterSettings.RevealMaterial;
            ApplyShape(cutter);
            AttachToOwner(cutter, rectangle.Normal);
            return cutter;
        }


        // Вырез лёг в стену по линии или здание: на боку стены (не на полу и не на крыше) — едет за их точками;
        // в здании или его перегородке — к этажам здания, как проём кликом: с «И этажи выше» — на этом и всех этажах выше.
        public static void AttachToOwner(GeometryCutter cutter, Vector3 surfaceNormal)
        {
            var owner = cutter.transform.parent;
            cutter.FollowsWallLine = LiveBuilderPathOwners.IsPathOwner(owner) && Mathf.Abs(surfaceNormal.y) < WallNormalMaxVertical;

            var building = LiveBuilderPathOwners.LinkedBuildingOf(owner);

            if (building != null)
                SplineBuildingLinkSync.Attach(cutter.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys);
        }


        // Форма из настроек инструмента: арка — верх полукругом, круг — овал во весь размер.
        public static void ApplyShape(GeometryCutter cutter)
        {
            switch (LiveBuilderCutterSettings.Shape)
            {
                case LiveBuilderCutterShape.Arch:
                    cutter.TopCornerRadius = cutter.Width / 2f;
                    break;

                case LiveBuilderCutterShape.Circle:
                    cutter.Shape = GeometryCutterShape.Circle;
                    break;
            }
        }

        // ── Рамка ────────────────────────────────────────────────────────

        // Пока тянешь — форма выреза на поверхности, размеры и углы, прилипшие к краю стены.
        public void DrawPreview()
        {
            var size = rectangle.Size;
            var min = rectangle.Min;
            rectangle.DrawEdgeMarkers();

            if (!IsActive || !rectangle.HasArea)
                return;

            Handles.color = GeometryCutterGizmos.CutterColor;

            var outline = PreviewOutline(size);
            var bottomCenter = new Vector2(min.x + size.x / 2f, min.y);
            var points = new Vector3[outline.Count + 1];

            for (var index = 0; index <= outline.Count; index++)
                points[index] = rectangle.PointAt(bottomCenter + outline[index % outline.Count]);

            Handles.DrawAAPolyLine(3f, points);
            LiveBuilderSceneText.DrawLabel(rectangle.PointAt(min + size), LiveBuilderSceneText.FormatMeters(size.x) + " × " + LiveBuilderSceneText.FormatMeters(size.y) + " м · " +
                                                                          (LiveBuilderCutterSettings.IsThrough ? "насквозь" : "ниша " + LiveBuilderSceneText.FormatMeters(LiveBuilderCutterSettings.NicheDepth) + " м"));
        }

        // Контур будущего выреза той формы, что выбрана, — тем же кодом, что у самого выреза.
        public static System.Collections.Generic.List<Vector2> PreviewOutline(Vector2 size)
        {
            return LiveBuilderCutterSettings.Shape switch
            {
                LiveBuilderCutterShape.Circle => GeometryCutterOutline.EllipseOutline(size.x, size.y, 24),
                LiveBuilderCutterShape.Arch => GeometryCutterOutline.RoundedBoxOutline(size.x, size.y, Mathf.Min(size.x, size.y) / 2f, 0f, 6),
                _ => GeometryCutterOutline.RoundedBoxOutline(size.x, size.y, 0f, 0f, 1)
            };
        }
    }
}
