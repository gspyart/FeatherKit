using System.Collections.Generic;
using System.Text;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш стен по линии, когда изменились линия, размеры, вырезы или вырезающие коробки объекта вокруг:
    /// правка точек, перенос, Ctrl+Z. Как у объектов из коробок — по «подписи», без лишних пересборок; меш — файлом рядом со сценой.
    /// Когда проверять — решает общая очередь <see cref="LiveBuilderRebuildQueue"/>: стены — последними, после коробок и зданий.
    /// Вырезающие коробки объекта, в котором стена лежит, переводит в проёмы в координатах стены: там, где коробка пересекает линию стены,
    /// стена прорезана насквозь. Коробка во всю высоту стены — комната, в которой стена стоит, а не проём: такую стену она не режет.
    /// Вырезы (<see cref="GeometryCutter"/>) вычитаются из готового меша (<see cref="GeometryCutterApplier"/>).
    /// У стены-комнаты в здании стороны на его стенах не строятся (<see cref="SplineWallBuildingSides"/>).
    /// </summary>
    public static class SplineWallRebuilder
    {
        // Версия расчёта стены: поменялась — все стены пересобираются. 2 — стороны на отрезке строго поперёк, откосы прямые;
        // 4 — коробка во всю высоту стены её не режет.
        private const int GeometryVersion = 4;


        // ── Пересборка ───────────────────────────────────────────────────

        // Как у объектов из коробок: экземпляр префаба без своих правок берёт меш префаба,
        // общий меш копий не перезаписывается — изменённая копия получает свой файл.
        public static void RebuildChangedWalls()
        {
            var walls = StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineWall>();
            var meshUserCounts = LiveBuilderMeshWriter.CountMeshUsers(walls);

            foreach (var wall in walls)
            {
                if (BoxModelPrefabInstances.TryUsePrefabGeometry(wall, () => IsPrefabMeshUpToDate(wall), () => PrefabStateOf(wall)))
                    continue;

                // Пустой меш — тоже готовый: стена из одной точки или целиком в вырезе.
                if (wall.GetComponent<MeshFilter>().sharedMesh != null && wall.BuiltSignature == SignatureOf(wall))
                    continue;

                LiveBuilderMeshWriter.ReleaseIfShared(wall, meshUserCounts);
                Rebuild(wall);
            }
        }

        // Меш префаба собран по тому же, что у экземпляра без правок, и не пустой.
        private static bool IsPrefabMeshUpToDate(SplineWall instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return sourceMesh != null && sourceMesh.vertexCount > 0 && source.BuiltSignature == SignatureOf(instance);
        }

        // Чем сейчас собран префаб — если после пересборки он всё равно не сходится, второй раз его не трогаем.
        private static string PrefabStateOf(SplineWall instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return (source != null ? source.BuiltSignature : "") + "|" + (sourceMesh != null ? sourceMesh.vertexCount : -1);
        }


        public static void Rebuild(SplineWall wall)
        {
            var paths = SplineWallBuildingSides.PathsToBuild(wall);
            var holesByPath = paths.ConvertAll(path => CollectHoles(wall, path));
            var material = wall.Material != null ? wall.Material : BoxModelFactory.LoadDefaultMaterial();
            var written = LiveBuilderMeshWriter.Write(wall, null, mesh =>
            {
                SplineWallMeshBuilder.Fill(mesh, wall, paths, holesByPath);
                return new[] { material };
            });

            var signature = SignatureOf(wall, written.Cutters);
            var isSceneChanged = written.IsNewMesh || wall.BuiltSignature != signature;

            LiveBuilderMeshWriter.UpdateCollider(wall, wall.HasCollider, written.Mesh);
            wall.BuiltSignature = signature;

            if (isSceneChanged)
                EditorUtility.SetDirty(wall);
        }

        // ── Проёмы ───────────────────────────────────────────────────────

        // Проёмы одного пути в координатах стены или здания — от вырезающих коробок объекта вокруг.
        public static List<SplineWallHole> CollectHoles(Component owner, SplineWallPath path)
        {
            var holes = new List<SplineWallHole>();
            var model = OuterModelOf(owner);

            // Вырез лестницы — только для перекрытий: стену рядом с маршем он не пробивает.
            if (model != null)
                foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                    if (box.Mode == BoxModelPartMode.Cut && box.Owner is not BoxModelStairs)
                        AddCutBoxHoles(owner, model, path, box.Box, holes);

            return holes;
        }

        // Где линия стены проходит внутри коробки (в плане) — там проём на высоту коробки. По отрезку на каждый кусок пути.
        private static void AddCutBoxHoles(Component owner, BoxModel model, SplineWallPath path, MillimeterBox box, List<SplineWallHole> holes)
        {
            var boxMin = (Vector3)box.Min / MillimeterBox.MillimetersPerMeter;
            var boxMax = (Vector3)box.Max / MillimeterBox.MillimetersPerMeter;
            var wallToModel = model.transform.worldToLocalMatrix * owner.transform.localToWorldMatrix;
            var modelToWall = wallToModel.inverse;

            for (var segment = 0; segment < path.SegmentCount; segment++)
            {
                var start = wallToModel.MultiplyPoint3x4(path.Points[segment]);
                var end = wallToModel.MultiplyPoint3x4(path.Points[segment + 1]);

                if (!TryClipToRectangle(start, end, boxMin, boxMax, out var fractionFrom, out var fractionTo))
                    continue;

                var segmentFrom = path.Distances[segment];
                var segmentLength = path.Distances[segment + 1] - segmentFrom;
                var alongFrom = segmentFrom + segmentLength * fractionFrom;
                var alongTo = segmentFrom + segmentLength * fractionTo;

                var middle = Vector3.Lerp(start, end, (fractionFrom + fractionTo) / 2f);
                var bottom = modelToWall.MultiplyPoint3x4(new Vector3(middle.x, boxMin.y, middle.z)).y;
                var top = modelToWall.MultiplyPoint3x4(new Vector3(middle.x, boxMax.y, middle.z)).y;
                var baseHeight = path.BaseHeightAt((alongFrom + alongTo) / 2f);

                if (owner is SplineWall wall && IsSpaceAroundWall(wall, bottom - baseHeight, top - baseHeight))
                    continue;

                holes.Add(new SplineWallHole(alongFrom, alongTo, bottom - baseHeight, top - baseHeight));
            }
        }

        // Коробка во всю высоту стены (хоть одной её копии) — это комната, в которой стена стоит, а не проём в ней: стену внутри дома из коробок она не режет.
        private static bool IsSpaceAroundWall(SplineWall wall, float bottom, float top)
        {
            const float tolerance = 0.01f;

            for (var copy = 0; copy < wall.CopiesUp; copy++)
            {
                var copyBottom = copy * wall.CopyStep;

                if (bottom <= copyBottom + tolerance && top >= copyBottom + wall.Height - tolerance)
                    return true;
            }

            return false;
        }

        // Часть отрезка внутри прямоугольника коробки в плане (X и Z), доли от 0 до 1. Отсечение Лианга — Барски.
        private static bool TryClipToRectangle(Vector3 start, Vector3 end, Vector3 min, Vector3 max, out float from, out float to)
        {
            from = 0f;
            to = 1f;
            var delta = end - start;

            foreach (var axis in new[] { 0, 2 })
            {
                if (Mathf.Abs(delta[axis]) < 1e-6f)
                {
                    if (start[axis] <= min[axis] || start[axis] >= max[axis])
                        return false;

                    continue;
                }

                var enter = (min[axis] - start[axis]) / delta[axis];
                var exit = (max[axis] - start[axis]) / delta[axis];

                if (enter > exit)
                    (enter, exit) = (exit, enter);

                from = Mathf.Max(from, enter);
                to = Mathf.Min(to, exit);

                if (from >= to)
                    return false;
            }

            return true;
        }


        // Объект из коробок, в котором лежит стена или здание: его вырезающие коробки режут и их.
        public static BoxModel OuterModelOf(Component owner)
        {
            return owner.transform.parent != null ? owner.transform.parent.GetComponentInParent<BoxModel>() : null;
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Всё, от чего зависит меш: точки линии, размеры, вырезающие коробки объекта вокруг, вырезы и где стоят стена и объект.
        // Вырезы — те, что режут нынешний меш стены.
        private static string SignatureOf(SplineWall wall)
        {
            return SignatureOf(wall, GeometryCutterLookup.CuttersFor(wall, GeometryCutterLookup.WorldBoundsOf(wall.GetComponent<MeshRenderer>())));
        }

        private static string SignatureOf(SplineWall wall, List<GeometryCutter> cutters)
        {
            var signature = new StringBuilder();
            signature.Append(GeometryVersion).Append('|').Append(wall.Thickness).Append(',').Append(wall.Height).Append(',').Append((int)wall.Alignment)
                .Append(',').Append(wall.CopiesUp).Append(',').Append(wall.CopyStep)
                .Append(',').Append(wall.CurveStep).Append(',').Append(wall.CornerSegments).Append(',').Append(BoxModelSideMaterials.KeyOf(wall.Material)).Append(wall.HasCollider ? "c" : "n")
                .Append("|b").Append(SplineWallBuildingSides.BuildingKeyOf(wall));

            foreach (var spline in wall.GetComponent<SplineContainer>().Splines)
            {
                signature.Append(spline.Closed ? "|closed" : "|open");

                for (var index = 0; index < spline.Count; index++)
                {
                    var knot = spline[index];
                    signature.Append(';').Append(knot.Position).Append(knot.TangentIn).Append(knot.TangentOut).Append(knot.Rotation)
                        .Append('r').Append(LiveBuilderCornerRadii.RadiusOf(spline, index))
                        .Append((int)spline.GetTangentMode(index));
                }
            }

            var model = OuterModelOf(wall);

            if (model != null)
                signature.Append("|m").Append(CutBoxesHashOf(model, false))
                    .Append(model.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix);

            signature.Append(GeometryCutterLookup.KeyOf(wall, cutters));
            return signature.ToString();
        }

        // Вырезающие коробки объекта — для подписи стены или здания: правка добавляющих коробок, материалов и ступеней их не пересобирает.
        // Вырез над лестницей режет только перекрытия здания, стенам он не нужен.
        public static Hash128 CutBoxesHashOf(BoxModel model, bool includesStairsCuts)
        {
            var key = new StringBuilder();

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Mode == BoxModelPartMode.Cut && (includesStairsCuts || box.Owner is not BoxModelStairs))
                    key.Append(box.Box.Min).Append(box.Box.Max).Append('|');

            return Hash128.Compute(key.ToString());
        }
    }
}
