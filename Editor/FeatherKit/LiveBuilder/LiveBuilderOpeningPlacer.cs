using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Клик инструментом «Проём»: наводишь на стену — рамка проёма выбранного размера (<see cref="LiveBuilderOpeningPresets"/>) и формы, клик — проём насквозь.
    /// Место — по настройке «Место»: «Где навёл» — серединой ровно под мышью, «По сетке» — левым краем и низом по сетке.
    /// По высоте этажи есть только у здания (и его перегородок): там низ — от пола его этажа, выбранного в окне (если он у здания есть,
    /// иначе того, на который навёл), на «Низ над полом». Без здания низ — там же, где навёл, но не ниже пола: у стены по линии — её низа
    /// (у копий вверх — низа копии под мышью), у объекта из коробок — пола этажа уровня.
    /// - стена объекта из коробок — прямоугольник: вырезающая коробка в конце списка, глубиной ровно в толщину стены; арка и круг — вырез в объекте
    /// - стена по линии и здание по контуру — вырез (<see cref="GeometryCutter"/>) в них: прямой, вдоль стены в этом месте, насквозь с запасом,
    ///   едет за точками линии; у здания — к его этажам, с «И все выше» — копии вверх до крыши
    /// </summary>
    public sealed class LiveBuilderOpeningPlacer
    {
        // Самая толстая стена, сквозь которую ищем обратную сторону, м; толще — режем на столько.
        private const float MaxWallThickness = 2f;
        private const float FallbackThickness = 0.5f;

        // Сколько точек проверить вдоль проёма на изогнутой стене: насколько она уходит от прямого выреза.
        private const int CurveSampleCount = 8;

        private BoxModel targetModel;
        private Vector3 cutMin;
        private Vector3 cutMax;
        private int wallAxis;

        // Стена по линии или здание по контуру, и вырез в её осях: низ посередине, поворот вдоль стены, глубина.
        private Component targetPathOwner;
        private Vector3 cutterLocalBottom;
        private Quaternion cutterLocalRotation;
        private float cutterDepth;

        // Сколько этажей здания выше, считая этот, — для «на все этажи».
        private int storeysAbove = 1;
        private float buildingStoreyHeight;


        public bool HasTarget => targetModel != null || targetPathOwner != null;

        public string Hint => HasTarget
            ? "Клик — проём «" + LiveBuilderOpeningPresets.Selected.DisplayName + "»" + CopiesHint + ". Протяни — свой размер"
            : "Наведи на стену — рамка проёма «" + LiveBuilderOpeningPresets.Selected.DisplayName + "». Протяни по любой поверхности — свой размер";

        // Сколько проёмов встанет одним кликом: в здании или его перегородке с «И все выше» — этот и все этажи выше.
        private int CopiesForTarget => LiveBuilderPathOwners.LinkedBuildingOf(targetPathOwner) != null && LiveBuilderBuildingContext.IsOnAllStoreys ? storeysAbove : 1;

        private string CopiesHint => CopiesForTarget > 1
            ? " на этом и ещё " + (CopiesForTarget - 1) + " этажах выше"
            : "";

        // Прямоугольник в объекте из коробок — вырезающая коробка; арка и круг — вырез.
        private static bool IsRectangle => LiveBuilderCutterSettings.Shape == LiveBuilderCutterShape.Rectangle;


        // ── Под мышью ────────────────────────────────────────────────────

        // Ближайшая к камере стена под мышью: объекта из коробок или по линии. Пол и потолок — не стена.
        public void UpdateHover(LiveBuilderSceneHit hit)
        {
            targetModel = null;
            targetPathOwner = null;

            var hasModelWall = hit.HasBoxSurface && BoxFaces.Axis(hit.BoxSurface.Surface.Face) != 1;
            var isPathWallCloser = hit.PathOwner != null && (!hasModelWall || hit.VisibleHit.distance < hit.BoxSurface.Distance)
                && Mathf.Abs(hit.VisibleHit.normal.y) < 0.7f;

            if (isPathWallCloser)
                AimAtPathWall(hit.PathOwner, hit.VisibleHit.point);
            else if (hasModelWall)
                AimAtModelWall(hit.BoxSurface);
        }


        // Стена объекта из коробок: коробка от грани внутрь на толщину стены.
        private void AimAtModelWall(BoxModelSurfaceHit hit)
        {
            var size = LiveBuilderOpeningPresets.Selected;
            var face = hit.Surface.Face;
            var normalAxis = BoxFaces.Axis(face);
            var alongAxis = normalAxis == 0 ? 2 : 0;
            var surfacePlane = MillimeterBox.ToMeters(hit.Surface.Plane);
            var floor = StoreyFloorUnder(hit.Model.transform.TransformPoint(hit.LocalPoint));
            var floorLocal = hit.Model.transform.InverseTransformPoint(new Vector3(0f, floor, 0f)).y;

            var min = Vector3.zero;
            var max = Vector3.zero;
            min[alongAxis] = LeftEdgeAlong(hit.LocalPoint[alongAxis] - size.Width / 2f, alongAxis);
            max[alongAxis] = min[alongAxis] + size.Width;
            min.y = BottomUnderMouse(hit.LocalPoint.y, size.Height, floorLocal);
            max.y = min.y + size.Height;

            var thickness = ThicknessBehind(hit.Model, face, surfacePlane, (min + max) / 2f);
            var inward = BoxFaces.IsPositive(face) ? -1f : 1f;
            min[normalAxis] = Mathf.Min(surfacePlane, surfacePlane + inward * thickness);
            max[normalAxis] = Mathf.Max(surfacePlane, surfacePlane + inward * thickness);

            targetModel = hit.Model;
            cutMin = min;
            cutMax = max;
            wallAxis = normalAxis;
        }

        // Толщина стены: до ближайшей обратной стороны того же объекта за гранью, напротив середины проёма.
        private static float ThicknessBehind(BoxModel model, int face, float surfacePlane, Vector3 probe)
        {
            var oppositeFace = BoxFaces.Opposite(face);
            var axisU = BoxFaces.SurfaceAxisU(oppositeFace);
            var axisV = BoxFaces.SurfaceAxisV(oppositeFace);
            var probeU = Mathf.RoundToInt(probe[axisU] * MillimeterBox.MillimetersPerMeter);
            var probeV = Mathf.RoundToInt(probe[axisV] * MillimeterBox.MillimetersPerMeter);
            var inward = BoxFaces.IsPositive(face) ? -1f : 1f;
            var best = float.MaxValue;

            foreach (var surface in BoxModelRebuilder.GetSurfaces(model))
            {
                if (surface.Face != oppositeFace)
                    continue;

                var depth = (MillimeterBox.ToMeters(surface.Plane) - surfacePlane) * inward;
                var area = surface.Area;

                if (depth > 0.001f && depth < best && probeU >= area.xMin && probeU <= area.xMax && probeV >= area.yMin && probeV <= area.yMax)
                    best = depth;
            }

            return best <= MaxWallThickness ? best : FallbackThickness;
        }


        // Стена по линии или здания: место вдоль пути; низ — у здания от пола его этажа, иначе где навёл.
        private void AimAtPathWall(Component wall, Vector3 worldPoint)
        {
            var size = LiveBuilderOpeningPresets.Selected;
            var localPoint = wall.transform.InverseTransformPoint(worldPoint);
            SplineWallPath nearestPath = null;
            var nearestGap = float.MaxValue;
            var along = 0f;

            foreach (var path in LiveBuilderPathOwners.PathsOf(wall))
            {
                var pathAlong = path.ClosestDistanceTo(localPoint, out var gap);

                if (gap >= nearestGap)
                    continue;

                nearestGap = gap;
                nearestPath = path;
                along = pathAlong;
            }

            if (nearestPath == null)
                return;

            var floorLocal = wall is SplineWall splineWall ? CopyFloorOf(splineWall, nearestPath, along, localPoint.y) : 0f;
            var hasStoreys = wall is SplineBuilding;
            storeysAbove = 1;

            // Перегородка в здании: проём привяжется к зданию — «и все выше» считается от этажа копии под мышью, как и при создании.
            if (wall.TryGetComponent<SplineBuildingStoreyLink>(out var wallLink) && wallLink.IsValid)
            {
                var copy = Mathf.RoundToInt((floorLocal - nearestPath.BaseHeightAt(along)) / Mathf.Max(0.1f, wallLink.Building.StoreyHeight));
                storeysAbove = Mathf.Max(1, wallLink.Building.StoreyCount - (wallLink.Storey + copy));
                buildingStoreyHeight = wallLink.Building.StoreyHeight;
                hasStoreys = true;
            }

            if (wall is SplineBuilding building)
            {
                var firstFloor = nearestPath.BaseHeightAt(0f) + building.FloorTopHeight;
                var localHeight = wall.transform.InverseTransformPoint(worldPoint).y - firstFloor;
                var windowStorey = LiveBuilderStoreys.CurrentStorey - LiveBuilderBuildingContext.BaseStoreyOf(building);
                var storey = windowStorey >= 0 && windowStorey < building.StoreyCount
                    ? windowStorey
                    : Mathf.Clamp(Mathf.FloorToInt(localHeight / building.StoreyHeight + 0.001f), 0, building.StoreyCount - 1);
                floorLocal = firstFloor + storey * building.StoreyHeight;
                storeysAbove = building.StoreyCount - storey;
                buildingStoreyHeight = building.StoreyHeight;
            }

            var alongFrom = Mathf.Clamp(LeftEdgeAlong(along - size.Width / 2f, 0), 0f, Mathf.Max(0f, nearestPath.Length - size.Width));
            var bottom = hasStoreys ? floorLocal + size.Sill : BottomUnderMouse(localPoint.y, size.Height, floorLocal);
            targetPathOwner = wall;
            FitCutterToWall(wall, nearestPath, alongFrom, size.Width, bottom, out cutterLocalBottom, out cutterLocalRotation, out cutterDepth);
        }

        // Низ стены по линии под мышью, в её осях: низ линии, а у стены с копиями вверх — низ той копии, на которую навёл.
        // Не пол этажа уровня: стена может стоять на подиуме или в здании со своим полом.
        private static float CopyFloorOf(SplineWall wall, SplineWallPath path, float along, float localHeight)
        {
            var baseHeight = path.BaseHeightAt(along);
            var copy = Mathf.Clamp(Mathf.FloorToInt((localHeight - baseHeight) / wall.CopyStep + 0.001f), 0, wall.CopiesUp - 1);
            return baseHeight + copy * wall.CopyStep;
        }

        // Левый край проёма вдоль стены: там, где середина под мышью, или по сетке — по настройке «Место».
        private static float LeftEdgeAlong(float edgeUnderMouse, int gridAxis)
        {
            return LiveBuilderCutterSettings.IsSnappedToGrid ? BoxModelGrid.Snap(edgeUnderMouse, gridAxis) : edgeUnderMouse;
        }

        // Низ проёма без этажей здания: середина по высоте под мышью или низ по сетке — по настройке «Место»; не ниже пола.
        private static float BottomUnderMouse(float heightUnderMouse, float openingHeight, float floor)
        {
            var bottom = heightUnderMouse - openingHeight / 2f;

            if (LiveBuilderCutterSettings.IsSnappedToGrid)
                bottom = BoxModelGrid.Snap(bottom, 1);

            return Mathf.Max(bottom, floor);
        }

        // Вырез в стену по линии или здания на отрезке пути от alongFrom шириной width, в осях стены: прямой, по хорде отрезка, посередине толщины;
        // низ — на высоте bottomHeight. Насквозь с запасом: на изогнутой стене — ещё и на то, насколько она уходит от хорды.
        public static void FitCutterToWall(Component wall, SplineWallPath path, float alongFrom, float width, float bottomHeight,
            out Vector3 localBottom, out Quaternion localRotation, out float depth)
        {
            LiveBuilderPathOwners.GetWallSides(wall, path, out var leftOffset, out var rightOffset);
            var middleOffset = (leftOffset + rightOffset) / 2f;

            var start = path.PointAt(alongFrom, middleOffset, 0f);
            var end = path.PointAt(alongFrom + width, middleOffset, 0f);
            var chord = end - start;
            chord.y = 0f;

            if (chord.sqrMagnitude < 1e-6f)
                chord = path.DirectionOf(path.SegmentAt(alongFrom, out _));

            var bottom = path.PointAt(alongFrom + width / 2f, middleOffset, 0f);
            bottom.y = bottomHeight;
            var rotation = Quaternion.LookRotation(Vector3.Cross(chord.normalized, Vector3.up), Vector3.up);
            var across = rotation * Vector3.forward;
            var deviation = 0f;

            for (var sample = 0; sample <= CurveSampleCount; sample++)
            {
                var point = path.PointAt(alongFrom + width * sample / CurveSampleCount, middleOffset, 0f);
                deviation = Mathf.Max(deviation, Mathf.Abs(Vector3.Dot(point - bottom, across)));
            }

            localBottom = bottom;
            localRotation = rotation;
            depth = rightOffset - leftOffset + 2f * (LiveBuilderCutterGesture.ThroughMargin + deviation);
        }

        // Пол того этажа, на высоте которого точка, в мире.
        private static float StoreyFloorUnder(Vector3 worldPoint)
        {
            var storeyHeight = LiveBuilderStoreys.StoreyHeight;
            var storey = Mathf.FloorToInt((worldPoint.y - LiveBuilderStoreys.GroundLevel) / storeyHeight + 0.001f);
            return LiveBuilderStoreys.GroundLevel + storey * storeyHeight;
        }


        // ── Клик ─────────────────────────────────────────────────────────

        // Кладёт проём и выделяет его. Под мышью не стена — ничего.
        public void Commit()
        {
            if (targetModel != null)
                CommitInModel();
            else if (targetPathOwner != null)
                CommitInPathWall();
        }

        // Прямоугольник — вырезающая коробка объекта; арка и круг — вырез в нём, по центру толщины стены.
        private void CommitInModel()
        {
            if (IsRectangle)
            {
                var part = BoxModelFactory.CreatePart(targetModel, cutMin, cutMax, BoxModelPartMode.Cut);
                part.name = "Opening";
                Selection.activeGameObject = part.gameObject;
            }
            else
            {
                var model = targetModel.transform;
                var bottom = model.TransformPoint(new Vector3((cutMin.x + cutMax.x) / 2f, cutMin.y, (cutMin.z + cutMax.z) / 2f));
                var across = wallAxis == 0 ? Vector3.right : Vector3.forward;
                var size = cutMax - cutMin;
                var width = wallAxis == 0 ? size.z : size.x;
                var cutter = CreateCutter(model, bottom, model.rotation * Quaternion.LookRotation(across, Vector3.up),
                    new Vector2(width, size.y), size[wallAxis] + LiveBuilderCutterGesture.ThroughMargin * 2f);
                Selection.activeGameObject = cutter.gameObject;
            }

            BoxModelRebuilder.Rebuild(targetModel);
        }

        // Вырез в стене или здании: едет за точками линии, в здании — к его этажам.
        private void CommitInPathWall()
        {
            var size = LiveBuilderOpeningPresets.Selected;
            var owner = targetPathOwner.transform;
            var worldRotation = owner.rotation * cutterLocalRotation;
            var cutter = CreateCutter(owner, owner.TransformPoint(cutterLocalBottom), worldRotation, new Vector2(size.Width, size.Height), cutterDepth);
            LiveBuilderCutterGesture.AttachToOwner(cutter, worldRotation * Vector3.forward);
            Selection.activeGameObject = cutter.gameObject;

            if (targetPathOwner is SplineBuilding ownBuilding)
                SplineBuildingRebuilder.Rebuild(ownBuilding);
            else
                SplineWallRebuilder.Rebuild((SplineWall)targetPathOwner);
        }

        private static GeometryCutter CreateCutter(Transform owner, Vector3 worldBottom, Quaternion worldRotation, Vector2 size, float depth)
        {
            var cutter = BoxModelFactory.CreateCutter(owner, worldBottom, worldRotation, size, depth);
            cutter.name = "Opening";
            cutter.RevealMaterial = LiveBuilderCutterSettings.RevealMaterial;
            LiveBuilderCutterGesture.ApplyShape(cutter);
            return cutter;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        public void DrawPreview()
        {
            if (targetModel != null)
                DrawModelFrame();
            else if (targetPathOwner != null)
                DrawWallFrame();
        }

        private void DrawModelFrame()
        {
            using (new Handles.DrawingScope(targetModel.transform.localToWorldMatrix))
            {
                Handles.color = GeometryCutterGizmos.CutterColor;
                var size = cutMax - cutMin;
                var width = wallAxis == 0 ? size.z : size.x;

                if (IsRectangle)
                    Handles.DrawWireCube((cutMin + cutMax) / 2f, size);
                else
                    DrawOutline(Matrix4x4.TRS(new Vector3((cutMin.x + cutMax.x) / 2f, cutMin.y, (cutMin.z + cutMax.z) / 2f),
                        Quaternion.LookRotation(wallAxis == 0 ? Vector3.right : Vector3.forward, Vector3.up), Vector3.one), new Vector2(width, size.y), size[wallAxis]);

                BoxModelSceneText.DrawLabel(cutMax, BoxModelSceneText.FormatMeters(width) + " × " + BoxModelSceneText.FormatMeters(size.y) +
                                                    " м, толщина " + BoxModelSceneText.FormatMeters(size[wallAxis]) + " м");
            }
        }

        // Форма проёма спереди и сзади стены; у здания с «И все выше» — на каждом этаже выше.
        private void DrawWallFrame()
        {
            var size = LiveBuilderOpeningPresets.Selected;
            var frameSize = new Vector2(size.Width, size.Height);
            Handles.color = GeometryCutterGizmos.CutterColor;

            for (var copy = 0; copy < CopiesForTarget; copy++)
            {
                var copyToWorld = Matrix4x4.Translate(Vector3.up * (copy * buildingStoreyHeight)) * targetPathOwner.transform.localToWorldMatrix
                                  * Matrix4x4.TRS(cutterLocalBottom, cutterLocalRotation, Vector3.one);

                using (new Handles.DrawingScope(copyToWorld))
                    DrawOutline(Matrix4x4.identity, frameSize, cutterDepth);

                if (copy == 0)
                    BoxModelSceneText.DrawLabel(copyToWorld.MultiplyPoint3x4(new Vector3(size.Width / 2f, size.Height, 0f)),
                        BoxModelSceneText.FormatMeters(size.Width) + " × " + BoxModelSceneText.FormatMeters(size.Height) + " м" +
                        (CopiesForTarget > 1 ? ", × " + CopiesForTarget + " этажа" : ""));
            }
        }

        // Контур выбранной формы спереди и сзади, в осях выреза (опора — низ посередине).
        private static void DrawOutline(Matrix4x4 cutterToFrame, Vector2 size, float depth)
        {
            var outline = LiveBuilderCutterGesture.PreviewOutline(size);
            var front = new Vector3[outline.Count + 1];
            var back = new Vector3[outline.Count + 1];

            for (var index = 0; index <= outline.Count; index++)
            {
                var point = outline[index % outline.Count];
                front[index] = cutterToFrame.MultiplyPoint3x4(new Vector3(point.x, point.y, depth / 2f));
                back[index] = cutterToFrame.MultiplyPoint3x4(new Vector3(point.x, point.y, -depth / 2f));
            }

            Handles.DrawAAPolyLine(3f, front);
            Handles.DrawAAPolyLine(3f, back);
        }
    }
}
