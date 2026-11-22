using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Двускатная крыша здания с прямоугольным контуром — кнопка «Gable Roof»: объект из коробок «Gable Roof» в этаже «Roof» здания,
    /// два пандуса спиной к спине (<see cref="BoxModelRamps"/>), конёк вдоль длинной стороны, свес за наружные стены.
    /// - едет с крышей, когда меняются этажи; нажал ещё раз — коробки пересоздаются по нынешнему контуру
    /// - парапет выключается тем же шагом Undo
    /// </summary>
    public static class SplineBuildingGableRoof
    {
        public const string RoofObjectName = "Gable Roof";

        private const float StraightTolerance = 0.001f;

        private const int RisesTowardPlusZ = 4;
        private const int RisesTowardMinusZ = 5;


        // ── Можно ли ─────────────────────────────────────────────────────

        // Контур — прямоугольник: одна замкнутая линия, 4 точки, прямые стороны под прямым углом, без скруглений; здание с этажами-контейнерами.
        public static bool CanBuild(SplineBuilding building, out string reason)
        {
            reason = null;

            if (building == null)
                reason = "No building selected.";
            else if (!SplineBuildingStoreys.HasStoreyContainers(building))
                reason = "Gable Roof needs an upgraded building with storey objects (Storey N, Roof).";
            else if (!TryGetCorners(building, out _))
                reason = "Gable Roof needs a rectangular contour: 4 points, straight sides at right angles, no rounded corners.";

            return reason == null;
        }

        // Углы контура в осях здания, по порядку линии.
        private static bool TryGetCorners(SplineBuilding building, out Vector3[] corners)
        {
            corners = null;
            var container = building.GetComponent<SplineContainer>();

            if (container == null || container.Splines.Count != 1)
                return false;

            var spline = container.Spline;

            if (spline.Count != 4 || !spline.Closed)
                return false;

            corners = new Vector3[4];

            for (var index = 0; index < 4; index++)
            {
                if (LiveBuilderCornerRadii.RadiusOf(spline, index) > 0f || !IsStraight(spline.GetCurve(index)))
                    return false;

                corners[index] = spline[index].Position;
            }

            for (var index = 0; index < 4; index++)
            {
                var along = corners[(index + 1) % 4] - corners[index];
                var next = corners[(index + 2) % 4] - corners[(index + 1) % 4];

                if (along.magnitude < StraightTolerance || Mathf.Abs(along.y) > StraightTolerance
                                                        || Mathf.Abs(Vector3.Dot(along.normalized, next.normalized)) > StraightTolerance)
                    return false;
            }

            return true;
        }

        // Обе ручки кривой — на прямой между её концами.
        private static bool IsStraight(BezierCurve curve)
        {
            var chord = (Vector3)(curve.P3 - curve.P0);

            if (chord.magnitude < StraightTolerance)
                return true;

            var direction = chord.normalized;
            return Vector3.Cross((Vector3)(curve.P1 - curve.P0), direction).magnitude < StraightTolerance
                   && Vector3.Cross((Vector3)(curve.P2 - curve.P0), direction).magnitude < StraightTolerance;
        }


        // ── Постройка ────────────────────────────────────────────────────

        // Ставит или пересоздаёт крышу одним шагом Undo. Нельзя — null.
        public static BoxModel Build(SplineBuilding building, float pitchDegrees, float overhang)
        {
            if (!CanBuild(building, out _) || !TryGetCorners(building, out var corners))
                return null;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Gable Roof");
            var undoGroup = Undo.GetCurrentGroup();

            var roofContainer = SplineBuildingStoreys.RoofOf(building).transform;
            var model = FindRoofObject(roofContainer);

            if (model == null)
                model = CreateRoofObject(building, roofContainer);
            else
                ClearBoxes(model);

            PlaceBoxes(building, model, corners, Mathf.Clamp(pitchDegrees, 5f, 60f), Mathf.Max(0f, overhang));

            if (building.HasParapet)
            {
                Undo.RecordObject(building, "Gable Roof");
                building.HasParapet = false;
                EditorUtility.SetDirty(building);
            }

            Undo.CollapseUndoOperations(undoGroup);
            BoxModelRebuilder.Rebuild(model);
            return model;
        }

        public static BoxModel FindRoofObject(Transform roofContainer)
        {
            foreach (Transform child in roofContainer)
                if (child.name == RoofObjectName && child.TryGetComponent<BoxModel>(out var model))
                    return model;

            return null;
        }

        private static BoxModel CreateRoofObject(SplineBuilding building, Transform roofContainer)
        {
            var model = BoxModelFactory.CreateModel(roofContainer.position);
            model.name = RoofObjectName;
            model.transform.SetParent(roofContainer, false);

            if (building.RoofMaterial != null)
                model.DefaultMaterial = building.RoofMaterial;

            return model;
        }

        private static void ClearBoxes(BoxModel model)
        {
            foreach (var part in model.GetComponentsInChildren<BoxModelPart>(true))
                Undo.DestroyObjectImmediate(part.gameObject);
        }


        // Объект — по сторонам контура: X вдоль длинной стороны, Z поперёк, опора — нижний угол со свесом.
        // Низ ската на свесе опускается ниже крыши, чтобы конёк был на половине ширины × тангенс уклона над ней.
        private static void PlaceBoxes(SplineBuilding building, BoxModel model, Vector3[] corners, float pitchDegrees, float overhang)
        {
            var firstSide = corners[1] - corners[0];
            var secondSide = corners[2] - corners[1];
            var longSide = firstSide.magnitude >= secondSide.magnitude ? firstSide : secondSide;
            var shortSide = firstSide.magnitude >= secondSide.magnitude ? secondSide : firstSide;
            var wallsOutside = OuterExpansionOf(building) * 2f;
            var length = longSide.magnitude + wallsOutside;
            var width = shortSide.magnitude + wallsOutside;

            var alongLength = new Vector3(longSide.x, 0f, longSide.z).normalized;
            var acrossWidth = Vector3.Cross(alongLength, Vector3.up);
            var slope = Mathf.Tan(pitchDegrees * Mathf.Deg2Rad);
            var center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
            var origin = new Vector3(center.x, 0f, center.z) - alongLength * (length / 2f + overhang) - acrossWidth * (width / 2f + overhang)
                         + Vector3.down * (overhang * slope);

            Undo.RecordObject(model.transform, "Gable Roof");
            model.transform.localPosition = origin;
            model.transform.localRotation = Quaternion.LookRotation(acrossWidth, Vector3.up);
            model.transform.localScale = Vector3.one;

            var halfWidth = width / 2f + overhang;
            var boxLength = length + 2f * overhang;
            var height = halfWidth * slope;
            var frontSlope = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(boxLength, height, halfWidth), BoxModelPartMode.Add);
            var backSlope = BoxModelFactory.CreatePart(model, new Vector3(0f, 0f, halfWidth), new Vector3(boxLength, height, 2f * halfWidth), BoxModelPartMode.Add);
            BoxModelRamps.MakeRamp(frontSlope, RisesTowardPlusZ);
            BoxModelRamps.MakeRamp(backSlope, RisesTowardMinusZ);
        }

        // Насколько наружная сторона стен дальше контура, м: контур снаружи — 0, изнутри — толщина, по оси — половина.
        private static float OuterExpansionOf(SplineBuilding building)
        {
            return building.ContourSide switch
            {
                SplineBuildingContourSide.Inside => building.WallThickness,
                SplineBuildingContourSide.Center => building.WallThickness / 2f,
                _ => 0f
            };
        }
    }
}
