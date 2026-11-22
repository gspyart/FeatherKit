using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Заготовки для жеста с проёмом: пока проём двигают, у каждой постройки, которую он режет, помнит меш, прорезанный всеми остальными вырезами.
    /// - шаг жеста режет заготовку только двигаемыми — черновик: треугольники идут в другом порядке, форма та же
    /// - заготовка устарела (поменялась постройка или другие вырезы) — собирается заново
    /// - забывает всё, когда жест кончился (<see cref="LiveBuilderGestureTracker.GestureEnded"/>)
    /// </summary>
    [InitializeOnLoad]
    public static class GeometryCutterDragBlanks
    {
        private static readonly HashSet<GeometryCutter> MovingCutters = new();
        private static readonly Dictionary<Component, GeometryCutterDragBlank> BlanksByOwner = new();


        static GeometryCutterDragBlanks()
        {
            LiveBuilderGestureTracker.GestureEnded += Clear;
        }


        public static bool HasMovingCutters => MovingCutters.Count > 0;


        // ── Двигаемые вырезы ─────────────────────────────────────────────

        // Вырез поменяли во время жеста — дальше до конца жеста он режет заготовку один.
        public static void NoteMoving(GeometryCutter cutter)
        {
            if (LiveBuilderGestureTracker.IsGestureActive && MovingCutters.Add(cutter))
                BlanksByOwner.Clear();
        }


        // ── Шаг жеста ────────────────────────────────────────────────────

        // Режет готовую заготовку двигаемыми вырезами и пишет в mesh. Заготовки нет или она не та — false, меш не тронут.
        // uncutKey — подпись постройки до вырезов; knownCutters — вырезы постройки, null — найти по габариту заготовки.
        public static bool TryCutFromBlank(Component owner, Mesh mesh, string uncutKey, List<GeometryCutter> knownCutters, out List<GeometryCutter> cutters,
            out Material[] materials)
        {
            cutters = knownCutters;
            materials = null;

            if (!BlanksByOwner.TryGetValue(owner, out var blank) || blank.UncutKey != uncutKey)
                return false;

            cutters ??= GeometryCutterLookup.CuttersFor(owner, GeometryCutterLookup.WorldBoundsOf(blank.UncutBounds, owner.transform));
            var moving = cutters.FindAll(IsMoving);
            var others = cutters.FindAll(cutter => !IsMoving(cutter));

            if (moving.Count == 0 || GeometryCutterLookup.KeyOf(owner, others) != blank.OthersKey)
                return false;

            var materialList = new List<Material>(blank.Materials);
            var polygons = new MeshSubtractionPolygons(CopyOf(blank.Polygons));
            GeometryCutterApplier.CutPolygons(polygons, owner.transform, moving, materialList);
            MeshSubtraction.Write(mesh, polygons.InOrder(), materialList.Count);
            materials = materialList.ToArray();
            return true;
        }


        // Режет меш вырезами, а если среди них есть двигаемые — сначала остальными, и этот меш запоминает заготовкой.
        public static Material[] CutAndRememberBlank(Component owner, Mesh mesh, string uncutKey, List<GeometryCutter> cutters, Material[] materials)
        {
            var moving = cutters.FindAll(IsMoving);

            if (moving.Count == 0 || mesh.vertexCount == 0)
                return GeometryCutterApplier.Cut(mesh, owner.transform, cutters, materials);

            var others = cutters.FindAll(cutter => !IsMoving(cutter));
            var uncutBounds = mesh.bounds;
            var materialList = GeometryCutterApplier.MaterialListOf(mesh, materials);
            var cutByOthers = new MeshSubtractionPolygons(MeshSubtraction.Read(mesh));
            GeometryCutterApplier.CutPolygons(cutByOthers, owner.transform, others, materialList);

            var blankPolygons = cutByOthers.InOrder();
            BlanksByOwner[owner] = new GeometryCutterDragBlank(uncutKey, GeometryCutterLookup.KeyOf(owner, others), uncutBounds, CopyOf(blankPolygons),
                materialList.ToArray());

            var polygons = new MeshSubtractionPolygons(blankPolygons);
            GeometryCutterApplier.CutPolygons(polygons, owner.transform, moving, materialList);
            MeshSubtraction.Write(mesh, polygons.InOrder(), materialList.Count);
            return materialList.ToArray();
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static bool IsMoving(GeometryCutter cutter)
        {
            return MovingCutters.Contains(cutter);
        }

        // Вычитание переворачивает и режет многоугольники на месте — заготовке нужна своя копия.
        private static List<MeshBooleanPolygon> CopyOf(List<MeshBooleanPolygon> polygons)
        {
            var copies = new List<MeshBooleanPolygon>(polygons.Count);

            foreach (var polygon in polygons)
                copies.Add(new MeshBooleanPolygon(new List<MeshBooleanVertex>(polygon.Vertices), polygon.Submesh, polygon.IsFromCutter, polygon.Plane));

            return copies;
        }

        private static void Clear()
        {
            MovingCutters.Clear();
            BlanksByOwner.Clear();
        }
    }


    /// <summary>Заготовка одной постройки: по какой подписи она собрана, какими вырезами прорезана, её многоугольники и материалы.</summary>
    public sealed class GeometryCutterDragBlank
    {
        public GeometryCutterDragBlank(string uncutKey, string othersKey, Bounds uncutBounds, List<MeshBooleanPolygon> polygons, Material[] materials)
        {
            UncutKey = uncutKey;
            OthersKey = othersKey;
            UncutBounds = uncutBounds;
            Polygons = polygons;
            Materials = materials;
        }


        public string UncutKey { get; }

        // Подпись вырезов заготовки — всех, кроме двигаемых.
        public string OthersKey { get; }

        // Габарит меша до вырезов, в осях постройки: по нему ищут её вырезы.
        public Bounds UncutBounds { get; }

        public List<MeshBooleanPolygon> Polygons { get; }
        public Material[] Materials { get; }
    }
}
