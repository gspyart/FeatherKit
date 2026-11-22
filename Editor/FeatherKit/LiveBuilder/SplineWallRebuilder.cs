using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш стен по линии, когда изменились линия, размеры, вырезы или вырезающие коробки объекта вокруг; когда — решает <see cref="LiveBuilderRebuildQueue"/>.
    /// - как у объектов из коробок — по подписи, без лишних пересборок; меш — файлом рядом со сценой
    /// - вырезающие коробки объекта вокруг — проёмы насквозь; кого они режут, решает <see cref="LiveBuilderCutRules"/>
    /// - вырезы (<see cref="GeometryCutter"/>) вычитаются из готового меша (<see cref="GeometryCutterApplier"/>)
    /// - у стены-комнаты в здании стороны на его стенах не строятся (<see cref="SplineWallBuildingSides"/>)
    /// - стена под повтором (<see cref="LiveBuilderArray"/>) — все копии в её меше, у каждой свои проёмы
    /// </summary>
    public static class SplineWallRebuilder
    {
        // Габарит стены до вырезов, в её осях: по нему ищут вырезы. По прорезанному мешу нельзя — стена целиком в вырезе пустая,
        // её вырез «пропадает», подпись не сходится, и стена пересобиралась бы на каждой правке. Помнится до перезагрузки скриптов.
        private static readonly Dictionary<EntityId, Bounds> UncutBoundsByWall = new();


        // ── Пересборка ───────────────────────────────────────────────────

        // Как у объектов из коробок: экземпляр префаба без своих правок берёт меш префаба,
        // общий меш копий не перезаписывается — изменённая копия получает свой файл.
        public static void RebuildChangedWalls()
        {
            RebuildChangedWalls(LiveBuilderRebuildScope.WholeScene, LiveBuilderBuildQuality.Final);
        }


        public static void RebuildChangedWalls(LiveBuilderRebuildScope scope, LiveBuilderBuildQuality quality)
        {
            foreach (var wall in scope.Find<SplineWall>())
            {
                try
                {
                    if (TryUsePrefabGeometry(wall))
                        continue;

                    bool isUpToDate;

                    // Пустой меш — тоже готовый: стена из одной точки или целиком в вырезе.
                    using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
                        isUpToDate = wall.GetComponent<MeshFilter>().sharedMesh != null
                                     && LiveBuilderDraftBuilds.IsBuilt(wall, wall.BuiltSignature, SignatureOf(wall, quality), quality);

                    if (!isUpToDate)
                        Rebuild(wall, quality);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, wall);
                }
            }
        }


        // Стена удалена или её сцена закрыта — её габарит до вырезов больше не нужен.
        public static void ForgetDestroyed()
        {
            LiveBuilderDestroyedObjects.RemoveFrom(UncutBoundsByWall);
        }


        // Экземпляр префаба без своих правок берёт меш префаба (отставший префаб пересобирается) — сам тогда не собирается.
        public static bool TryUsePrefabGeometry(SplineWall wall)
        {
            return BoxModelPrefabInstances.TryUsePrefabGeometry(wall, () => IsPrefabMeshUpToDate(wall), () => PrefabStateOf(wall));
        }

        // Меш префаба собран по тому же, что у экземпляра без правок, и не пустой.
        private static bool IsPrefabMeshUpToDate(SplineWall instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return sourceMesh != null && sourceMesh.vertexCount > 0 && source.BuiltSignature == SignatureOf(instance, LiveBuilderBuildQuality.Final);
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
            Rebuild(wall, LiveBuilderBuildQuality.Final);
        }


        // Черновик — без коллайдера, подпись в сцене не пишется (<see cref="LiveBuilderDraftBuilds"/>).
        public static void Rebuild(SplineWall wall, LiveBuilderBuildQuality quality)
        {
            LiveBuilderRebuildProfiler.CountRebuiltConstruction();

            List<SplineWallPath> paths;
            List<SplineWallPath> slabPaths;
            List<SplineWallCopy> copies;
            var material = wall.Material != null ? wall.Material : BoxModelFactory.LoadDefaultMaterial();

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.WallGeometry))
            {
                paths = PathsOf(wall);

                // Пол и потолок — по всей замкнутой линии, и вдоль стен здания тоже: стороны там не строятся, а комната под ними есть.
                slabPaths = wall.HasSlabs ? SplineWallPath.FromWall(wall).FindAll(path => path.IsClosed && path.IsUsable) : new List<SplineWallPath>();
                copies = CopiesOf(wall, paths, slabPaths.Count > 0);
            }

            // Подпись до вырезов — только для заготовки жеста с проёмом: иначе её незачем считать.
            var uncutKey = quality == LiveBuilderBuildQuality.Draft && GeometryCutterDragBlanks.HasMovingCutters ? SplineWallSignature.UncutOf(wall) : null;
            var materials = slabPaths.Count > 0
                ? new[] { material, wall.FloorMaterial != null ? wall.FloorMaterial : material, wall.CeilingMaterial != null ? wall.CeilingMaterial : material }
                : new[] { material };
            var written = LiveBuilderMeshWriter.Write(wall, null, mesh =>
            {
                using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.WallGeometry))
                    SplineWallMeshBuilder.Fill(mesh, wall, paths, copies, slabPaths, SplineWallTexturing.ForWall(wall, materials));

                UncutBoundsByWall[wall.GetEntityId()] = mesh.bounds;
                return materials;
            }, null, quality, uncutKey);

            string signature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
                signature = SplineWallSignature.Of(wall, written.Cutters, quality);

            if (quality == LiveBuilderBuildQuality.Draft)
            {
                LiveBuilderDraftBuilds.Remember(wall, signature);
                return;
            }

            var isSceneChanged = written.IsNewMesh || wall.BuiltSignature != signature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Collider))
                LiveBuilderMeshWriter.UpdateCollider(wall, wall.HasCollider, written.Mesh);
            wall.BuiltSignature = signature;
            LiveBuilderDraftBuilds.Forget(wall);

            if (isSceneChanged)
            {
                EditorUtility.SetDirty(wall);
                LiveBuilderSyncEdits.RecordPrefabInstance(wall);
            }
        }

        // Копии стены или здания из повторов над ними — каждая со своими проёмами и дырами в перекрытиях; без повторов — одна, сама постройка.
        public static List<SplineWallCopy> CopiesOf(Component owner, IReadOnlyList<SplineWallPath> paths, bool includesSlabHoles)
        {
            var offsets = LiveBuilderArrayCopyPoses.CopiesOf(owner.transform);
            var copies = new List<SplineWallCopy>(offsets.Count);

            foreach (var offset in offsets)
            {
                var slabHoles = includesSlabHoles ? SplineBuildingSlabHoles.Collect(owner, offset) : new List<SplineBuildingSlabHole>();
                copies.Add(new SplineWallCopy(LiveBuilderArrayCopyPoses.LocalOffsetOf(owner.transform, offset), CollectHoles(owner, paths, offset), slabHoles));
            }

            return copies;
        }


        // ── Проёмы ───────────────────────────────────────────────────────

        // Пути, по которым строится стена или стены здания, в их осях.
        public static List<SplineWallPath> PathsOf(Component owner)
        {
            return owner is SplineBuilding building
                ? SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep)
                : SplineWallBuildingSides.PathsToBuild((SplineWall)owner);
        }


        // Проёмы каждого пути в координатах стены или здания — от вырезающих коробок объекта вокруг, что режут стены; порядок в списке не важен.
        // Вырез лестницы и коробка «только перекрытия» стену не пробивают; вырез-коробка, которая стену не проходит насквозь, режет её вычитанием.
        public static List<List<SplineWallHole>> CollectHoles(Component owner, IReadOnlyList<SplineWallPath> paths)
        {
            return CollectHoles(owner, paths, Vector3.zero);
        }


        // Проёмы копии постройки, сдвинутой повтором на copyOffset в мире: коробки те же, а копия стоит в другом месте.
        public static List<List<SplineWallHole>> CollectHoles(Component owner, IReadOnlyList<SplineWallPath> paths, Vector3 copyOffset)
        {
            var holesByPath = new List<List<SplineWallHole>>(paths.Count);

            foreach (var _ in paths)
                holesByPath.Add(new List<SplineWallHole>());

            if (owner is SplineWall wall)
                AddChildCutterHoles(wall, paths, holesByPath);

            var model = OuterModelOf(owner);

            if (model == null)
                return holesByPath;

            Dictionary<GeometryCutter, bool> isPreciseByCutter = null;
            var wallToModel = model.transform.worldToLocalMatrix * LiveBuilderArrayCopyPoses.LocalToWorldOfCopy(owner.transform, copyOffset);

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (box.Mode != BoxModelPartMode.Cut || !box.CutSource.CutsWalls || !LiveBuilderCutRules.CutBoxCutsConstruction(model, box, owner))
                    continue;

                if (box.Owner is GeometryCutter cutter && !IsCutterPrecise(cutter, model, owner, paths, ref isPreciseByCutter))
                    continue;

                for (var index = 0; index < paths.Count; index++)
                    AddCutBoxHoles(wallToModel, paths[index], box.Box, holesByPath[index]);
            }

            return holesByPath;
        }

        // Дверь — ребёнок стены, коробка по её осям — проём по линии (<see cref="LiveBuilderCutRules.IsWallChildBox"/>). Копия стены несёт свою дверь.
        private static void AddChildCutterHoles(SplineWall wall, IReadOnlyList<SplineWallPath> paths, List<List<SplineWallHole>> holesByPath)
        {
            foreach (var cutter in LiveBuilderCutRules.PreciseChildCuttersOf(wall))
                foreach (var box in GeometryCutterSolverBoxes.BoxesOf(cutter, wall.transform, Vector3.zero))
                    for (var index = 0; index < paths.Count; index++)
                        AddCutBoxHoles(Matrix4x4.identity, paths[index], box, holesByPath[index]);
        }

        // Решается раз на вырез: у его копий — одно решение, как у вычитания.
        private static bool IsCutterPrecise(GeometryCutter cutter, BoxModel model, Component owner, IReadOnlyList<SplineWallPath> paths,
            ref Dictionary<GeometryCutter, bool> isPreciseByCutter)
        {
            isPreciseByCutter ??= new Dictionary<GeometryCutter, bool>();

            if (!isPreciseByCutter.TryGetValue(cutter, out var isPrecise))
                isPreciseByCutter[cutter] = isPrecise = LiveBuilderCutRules.CutsWallsPrecisely(cutter, model, owner, paths);

            return isPrecise;
        }

        // Где линия стены проходит внутри коробки (в плане) — там проём на высоту коробки. По отрезку на каждый кусок пути.
        // wallToModel — из осей стены (её копии) в оси коробки.
        private static void AddCutBoxHoles(Matrix4x4 wallToModel, SplineWallPath path, MillimeterBox box, List<SplineWallHole> holes)
        {
            var boxMin = box.MinInMeters;
            var boxMax = box.MaxInMeters;
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

                holes.Add(new SplineWallHole(alongFrom, alongTo, bottom - baseHeight, top - baseHeight));
            }
        }

        // ── Насквозь ли ──────────────────────────────────────────────────

        // Вырез-коробка проходит стену насквозь всюду, где её задевает: обе стороны стены входят в коробку там же, где линия. Иначе это ниша.
        // Стена под повтором — так в каждой её копии.
        public static bool IsThroughThickness(Component owner, BoxModel model, GeometryCutter cutter, IReadOnlyList<SplineWallPath> paths)
        {
            var cutterBoxes = new List<MillimeterBox>();

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Owner == cutter)
                    cutterBoxes.Add(box.Box);

            foreach (var ownerCopy in LiveBuilderArrayCopyPoses.CopiesOf(owner.transform))
            {
                var wallToModel = model.transform.worldToLocalMatrix * LiveBuilderArrayCopyPoses.LocalToWorldOfCopy(owner.transform, ownerCopy);

                if (!IsThroughThickness(owner, wallToModel, cutterBoxes, paths))
                    return false;
            }

            return true;
        }


        // Коробки в осях boxSpace, wallToBox — из осей стены туда же. Дверь-ребёнок стены — в осях самой стены.
        public static bool IsThroughThickness(Component owner, Matrix4x4 wallToBox, IReadOnlyList<MillimeterBox> boxes, IReadOnlyList<SplineWallPath> paths)
        {
            foreach (var box in boxes)
                foreach (var path in paths)
                {
                    GetSideOffsets(owner, path, out var leftOffset, out var rightOffset);

                    for (var segment = 0; segment < path.SegmentCount; segment++)
                        if (!IsSegmentThrough(path, segment, leftOffset, rightOffset, wallToBox, box))
                            return false;
                }

            return true;
        }

        private static void GetSideOffsets(Component owner, SplineWallPath path, out float leftOffset, out float rightOffset)
        {
            if (owner is SplineBuilding building)
                SplineBuildingMeshBuilder.GetWallOffsets(building, path, out leftOffset, out rightOffset);
            else
                SplineWallMeshBuilder.GetSideOffsets((SplineWall)owner, out leftOffset, out rightOffset);
        }

        // Коробки отрезок не касается — годится; касается — линия и обе стороны входят в неё и выходят на одних долях отрезка.
        // Стороны сверяются с коробкой, расширенной на миллиметр: сторона вровень с гранью коробки — тоже насквозь.
        private static bool IsSegmentThrough(SplineWallPath path, int segment, float leftOffset, float rightOffset, Matrix4x4 wallToModel, MillimeterBox box)
        {
            const float sideMargin = 0.001f;
            const float fractionTolerance = 0.002f;

            var start = path.Points[segment];
            var end = path.Points[segment + 1];
            var side = Vector3.Cross(Vector3.up, path.DirectionOf(segment));
            var boxMin = box.MinInMeters;
            var boxMax = box.MaxInMeters;
            var margin = new Vector3(sideMargin, 0f, sideMargin);

            var isCenterInside = TryClipToRectangle(wallToModel.MultiplyPoint3x4(start), wallToModel.MultiplyPoint3x4(end), boxMin, boxMax,
                out var centerFrom, out var centerTo);
            var isLeftInside = TryClipToRectangle(wallToModel.MultiplyPoint3x4(start + side * leftOffset), wallToModel.MultiplyPoint3x4(end + side * leftOffset),
                boxMin - margin, boxMax + margin, out var leftFrom, out var leftTo);
            var isRightInside = TryClipToRectangle(wallToModel.MultiplyPoint3x4(start + side * rightOffset), wallToModel.MultiplyPoint3x4(end + side * rightOffset),
                boxMin - margin, boxMax + margin, out var rightFrom, out var rightTo);

            if (!isCenterInside && !isLeftInside && !isRightInside)
                return true;

            if (!isCenterInside || !isLeftInside || !isRightInside)
                return false;

            var tolerance = fractionTolerance / Mathf.Max(path.Distances[segment + 1] - path.Distances[segment], 1e-4f);
            return Mathf.Abs(leftFrom - centerFrom) <= tolerance && Mathf.Abs(rightFrom - centerFrom) <= tolerance
                   && Mathf.Abs(leftTo - centerTo) <= tolerance && Mathf.Abs(rightTo - centerTo) <= tolerance;
        }


        // Часть отрезка внутри прямоугольника коробки в плане (X и Z), доли от 0 до 1. Отсечение Лианга — Барски.
        internal static bool TryClipToRectangle(Vector3 start, Vector3 end, Vector3 min, Vector3 max, out float from, out float to)
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

        // Подпись с вырезами, что режут нынешний меш стены (<see cref="SplineWallSignature"/>).
        private static string SignatureOf(SplineWall wall, LiveBuilderBuildQuality quality)
        {
            return SplineWallSignature.Of(wall, GeometryCutterLookup.CuttersFor(wall, UncutWorldBoundsOf(wall)), quality);
        }

        // Габарит до вырезов; не помнится (скрипты перезагрузились) — габарит готового меша.
        private static Bounds UncutWorldBoundsOf(SplineWall wall)
        {
            return UncutBoundsByWall.TryGetValue(wall.GetEntityId(), out var uncutBounds)
                ? GeometryCutterLookup.WorldBoundsOf(uncutBounds, wall.transform)
                : GeometryCutterLookup.WorldBoundsOf(wall.GetComponent<MeshRenderer>());
        }
    }
}
