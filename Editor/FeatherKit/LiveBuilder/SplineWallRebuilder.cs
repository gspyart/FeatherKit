using System.Collections.Generic;
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
    /// стена прорезана насквозь. Режут только коробки ниже стены в иерархии (<see cref="LiveBuilderHierarchyOrder"/>), которые режут стены.
    /// Коробка во всю высоту стены — комната, в которой стена стоит, а не проём: такую стену она не режет.
    /// Вырезы (<see cref="GeometryCutter"/>) вычитаются из готового меша (<see cref="GeometryCutterApplier"/>).
    /// У стены-комнаты в здании стороны на его стенах не строятся (<see cref="SplineWallBuildingSides"/>).
    /// </summary>
    public static class SplineWallRebuilder
    {
        // Версия расчёта стены: поменялась — все стены пересобираются. 2 — стороны на отрезке строго поперёк, откосы прямые;
        // 4 — коробка во всю высоту стены её не режет; 5 — развёртка верха и низа по плану.
        private const int GeometryVersion = 5;

        // Запас габарита стены или здания для поиска вырезающих коробок, м: коробка, вплотную касающаяся стены, тоже в подписи.
        private const float OwnerBoundsMargin = 0.05f;

        // Габарит стены до вырезов, в её осях: по нему ищут вырезы. По прорезанному мешу нельзя — стена целиком в вырезе пустая,
        // её вырез «пропадает», подпись не сходится, и стена пересобиралась бы на каждой правке. Помнится до перезагрузки скриптов.
        private static readonly Dictionary<EntityId, Bounds> UncutBoundsByWall = new();


        // ── Пересборка ───────────────────────────────────────────────────

        // Как у объектов из коробок: экземпляр префаба без своих правок берёт меш префаба,
        // общий меш копий не перезаписывается — изменённая копия получает свой файл.
        public static void RebuildChangedWalls()
        {
            foreach (var wall in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineWall>())
            {
                try
                {
                    if (TryUsePrefabGeometry(wall))
                        continue;

                    // Пустой меш — тоже готовый: стена из одной точки или целиком в вырезе.
                    if (wall.GetComponent<MeshFilter>().sharedMesh == null || wall.BuiltSignature != SignatureOf(wall))
                        Rebuild(wall);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, wall);
                }
            }
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

            // Пол и потолок — по всей замкнутой линии, и вдоль стен здания тоже: стороны там не строятся, а комната под ними есть.
            var slabPaths = wall.HasSlabs ? SplineWallPath.FromWall(wall).FindAll(path => path.IsClosed && path.IsUsable) : new List<SplineWallPath>();
            var slabHoles = slabPaths.Count > 0 ? SplineBuildingRebuilder.CollectSlabHoles(wall) : new List<SplineBuildingSlabHole>();

            var written = LiveBuilderMeshWriter.Write(wall, null, mesh =>
            {
                SplineWallMeshBuilder.Fill(mesh, wall, paths, holesByPath, slabPaths, slabHoles);
                UncutBoundsByWall[wall.GetEntityId()] = mesh.bounds;

                return slabPaths.Count > 0
                    ? new[] { material, wall.FloorMaterial != null ? wall.FloorMaterial : material, wall.CeilingMaterial != null ? wall.CeilingMaterial : material }
                    : new[] { material };
            });

            var signature = SignatureOf(wall, written.Cutters);
            var isSceneChanged = written.IsNewMesh || wall.BuiltSignature != signature;

            LiveBuilderMeshWriter.UpdateCollider(wall, wall.HasCollider, written.Mesh);
            wall.BuiltSignature = signature;

            if (isSceneChanged)
            {
                EditorUtility.SetDirty(wall);
                LiveBuilderSyncEdits.RecordPrefabInstance(wall);
            }
        }

        // ── Проёмы ───────────────────────────────────────────────────────

        // Проёмы одного пути в координатах стены или здания — от вырезающих коробок объекта вокруг, что режут стены и стоят ниже в иерархии.
        // Вырез лестницы и коробка «только перекрытия» стену не пробивают.
        public static List<SplineWallHole> CollectHoles(Component owner, SplineWallPath path)
        {
            var holes = new List<SplineWallHole>();
            var model = OuterModelOf(owner);

            if (model == null)
                return holes;

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (!box.CutsWalls)
                    continue;

                if (box.IsBelow(owner))
                    AddCutBoxHoles(owner, model, path, box.Box, holes);
                else
                    ReportIfWouldCut(owner, model, path, box);
            }

            return holes;
        }

        // Коробка выше стены в иерархии, а прорезала бы её — старая сцена: сказать в консоль, что теперь не режет.
        private static void ReportIfWouldCut(Component owner, BoxModel model, SplineWallPath path, BoxModelSolverBox box)
        {
            var skippedHoles = new List<SplineWallHole>();
            AddCutBoxHoles(owner, model, path, box.Box, skippedHoles);

            if (skippedHoles.Count > 0)
                LiveBuilderCutOrderNotices.ReportBoxAboveTarget(box.Owner, owner);
        }

        // Где линия стены проходит внутри коробки (в плане) — там проём на высоту коробки. По отрезку на каждый кусок пути.
        private static void AddCutBoxHoles(Component owner, BoxModel model, SplineWallPath path, MillimeterBox box, List<SplineWallHole> holes)
        {
            var boxMin = box.MinInMeters;
            var boxMax = box.MaxInMeters;
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
            return SignatureOf(wall, GeometryCutterLookup.CuttersFor(wall, UncutWorldBoundsOf(wall)));
        }

        // Габарит до вырезов; не помнится (скрипты перезагрузились) — габарит готового меша.
        private static Bounds UncutWorldBoundsOf(SplineWall wall)
        {
            return UncutBoundsByWall.TryGetValue(wall.GetEntityId(), out var uncutBounds)
                ? GeometryCutterLookup.WorldBoundsOf(uncutBounds, wall.transform)
                : GeometryCutterLookup.WorldBoundsOf(wall.GetComponent<MeshRenderer>());
        }

        // Всё, от чего зависит меш стены, — хешем, одинаковым на любом запуске и машине (<see cref="LiveBuilderStableHash"/>); в конце — вырезы.
        private static string SignatureOf(SplineWall wall, List<GeometryCutter> cutters)
        {
            var hash = new Hash128();
            hash.Append(GeometryVersion);
            hash.Append(wall.Thickness);
            hash.Append(wall.Height);
            hash.Append((int)wall.Alignment);
            hash.Append(wall.CopiesUp);
            hash.Append(wall.CopyStep);
            hash.Append(wall.CurveStep);
            hash.Append(wall.CornerSegments);
            hash.Append(BoxModelSideMaterials.KeyOf(wall.Material));
            hash.Append(wall.HasCollider ? 1 : 0);
            SplineWallBuildingSides.AppendBuildingKey(ref hash, wall);
            LiveBuilderStableHash.AppendSplines(ref hash, wall.GetComponent<SplineContainer>());

            // Пол и потолок — только если есть: у прежних стен подпись та же, пересборки нет.
            if (wall.HasSlabs)
            {
                hash.Append(wall.HasFloor ? 1 : 0);
                hash.Append(wall.HasCeiling ? 1 : 0);
                hash.Append(wall.SlabThickness);
                hash.Append(BoxModelSideMaterials.KeyOf(wall.FloorMaterial));
                hash.Append(BoxModelSideMaterials.KeyOf(wall.CeilingMaterial));
            }

            var model = OuterModelOf(wall);

            // Коробки — уже в осях стены: где стоит сама стена в объекте, подписи не важно. С полом и потолком — и те, что режут перекрытия.
            if (model != null)
            {
                var cutBoxes = CutBoxesHashOf(model, wall, wall.HasSlabs);
                hash.Append(ref cutBoxes);
            }

            return hash + GeometryCutterLookup.KeyOf(wall, cutters);
        }

        // Вырезающие коробки объекта, которые режут эту стену или здание, — для их подписи: правка добавляющих коробок, материалов и ступеней
        // их не пересобирает, а переставил коробку выше или ниже в иерархии — пересоберёт. Перекрытия есть только у здания.
        // Только коробки, что задевают габарит стены или здания, и их углы — в осях самой стены или здания, с округлением до 0,1 мм:
        // коробка в другом конце объекта (копия здания с лестницей рядом) подпись не меняет, а здание, которое едет вместе со своей лестницей, не пересобирается.
        // Что режет коробка — только если не «всё».
        public static Hash128 CutBoxesHashOf(BoxModel model, Component owner, bool includesSlabCuts)
        {
            var hash = new Hash128();
            var modelToOwner = LiveBuilderStableHash.RelativeMatrix(owner.transform, model.transform);
            var ownerBounds = UncutLocalBoundsOf(owner);

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                var isApplied = box.CutsWalls || includesSlabCuts && box.CutsSlabs;

                if (!isApplied || !box.IsBelow(owner))
                    continue;

                var min = modelToOwner.MultiplyPoint3x4(box.Box.MinInMeters);
                var max = modelToOwner.MultiplyPoint3x4(box.Box.MaxInMeters);
                var boxBounds = new Bounds(min, Vector3.zero);
                boxBounds.Encapsulate(max);

                // Коробка по осям объекта, а стена может стоять в нём повёрнутой: габарит — по всем восьми углам.
                foreach (var corner in CornersOf(box.Box.MinInMeters, box.Box.MaxInMeters))
                    boxBounds.Encapsulate(modelToOwner.MultiplyPoint3x4(corner));

                if (!boxBounds.Intersects(ownerBounds))
                    continue;

                AppendQuantized(ref hash, min);
                AppendQuantized(ref hash, max);

                if (box.OwnerPart != null && box.CutTargets != LiveBuilderCutTargets.Everything)
                    hash.Append((int)box.CutTargets);
            }

            return hash;
        }

        private static void AppendQuantized(ref Hash128 hash, Vector3 point)
        {
            LiveBuilderStableHash.AppendQuantized(ref hash, point.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, point.y);
            LiveBuilderStableHash.AppendQuantized(ref hash, point.z);
        }

        private static Vector3[] CornersOf(Vector3 min, Vector3 max)
        {
            var corners = new Vector3[8];

            for (var index = 0; index < 8; index++)
                corners[index] = new Vector3((index & 1) == 0 ? min.x : max.x, (index & 2) == 0 ? min.y : max.y, (index & 4) == 0 ? min.z : max.z);

            return corners;
        }

        // Где может быть стена или здание до вырезов, в их осях, — по точкам линии с усами, с запасом на толщину стен; по высоте — от низа линии
        // до верха стены со всеми копиями вверх или до крыши здания. Считается из чисел, а не из меша: подпись не зависит от того, собран ли он.
        private static Bounds UncutLocalBoundsOf(Component owner)
        {
            var container = owner.GetComponent<SplineContainer>();
            var bounds = new Bounds();
            var hasPoints = false;

            foreach (var spline in container.Splines)
            {
                foreach (var knot in spline.Knots)
                {
                    Vector3 position = knot.Position;
                    Quaternion rotation = knot.Rotation;

                    foreach (var point in new[] { position, position + rotation * (Vector3)knot.TangentIn, position + rotation * (Vector3)knot.TangentOut })
                    {
                        if (hasPoints)
                            bounds.Encapsulate(point);
                        else
                            bounds = new Bounds(point, Vector3.zero);

                        hasPoints = true;
                    }
                }
            }

            var isBuilding = owner is SplineBuilding;
            var thickness = isBuilding ? ((SplineBuilding)owner).WallThickness : ((SplineWall)owner).Thickness;
            var top = isBuilding
                ? ((SplineBuilding)owner).TotalHeight + ((SplineBuilding)owner).ParapetRise
                : ((SplineWall)owner).Height + (((SplineWall)owner).CopiesUp - 1) * ((SplineWall)owner).CopyStep;
            var margin = thickness + OwnerBoundsMargin;
            var min = bounds.min - new Vector3(margin, OwnerBoundsMargin, margin);
            var max = bounds.max + new Vector3(margin, top + OwnerBoundsMargin, margin);
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
