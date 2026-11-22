using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Наружные поверхности одного объекта из коробок с памятью по плоскостям: ответ тот же, что у <see cref="BoxModelSurfaceFinder"/>, байт в байт и по порядку.
    /// - грань закрывают и сливают только грани той же плоскости — плоскость считается сама по себе
    /// - плоскость с теми же гранями по порядку берётся из памяти: сдвиг колонны не трогает плоскость, где лежат 150 столбиков ограды
    /// - помнит два прохода: что не спросили два прохода подряд, забывается
    /// </summary>
    public sealed class BoxModelSurfaceCache
    {
        private Dictionary<(int axis, int plane), BoxModelSurfaceSlice> currentSlices = new();
        private Dictionary<(int axis, int plane), BoxModelSurfaceSlice> previousSlices = new();


        // Сколько плоскостей последний расчёт считал заново, а не взял из памяти.
        public int RecomputedSliceCount { get; private set; }


        // ── Расчёт ───────────────────────────────────────────────────────

        public List<BoxModelSurface> FindOuterSurfaces(IReadOnlyList<BoxModelCell> cells)
        {
            (previousSlices, currentSlices) = (currentSlices, previousSlices);
            currentSlices.Clear();
            return FindSurfaces(cells, true);
        }


        // Предпросмотр рисования: память только читается.
        public List<BoxModelSurface> FindOuterSurfacesWithoutStoring(IReadOnlyList<BoxModelCell> cells)
        {
            return FindSurfaces(cells, false);
        }


        // Порядок как у эталона: группа «грань, плоскость, коробка» встаёт туда, где в списке кусков её первая видимая грань.
        private List<BoxModelSurface> FindSurfaces(IReadOnlyList<BoxModelCell> cells, bool isStoring)
        {
            var groupsByFirstFace = new List<BoxModelSurface>[cells.Count * BoxFaces.Count];
            RecomputedSliceCount = 0;

            foreach (var (key, faces) in FacesBySlice(cells))
            {
                var slice = Find(key, faces, isStoring);

                if (slice == null)
                {
                    RecomputedSliceCount++;
                    slice = BoxModelSurfaceSlice.Solve(key.plane, faces);

                    if (isStoring)
                        currentSlices[key] = slice;
                }

                foreach (var group in slice.Groups)
                {
                    var firstFace = faces[group.FirstFace];
                    groupsByFirstFace[firstFace.CellIndex * BoxFaces.Count + firstFace.Face] = group.Surfaces;
                }
            }

            var surfaces = new List<BoxModelSurface>();

            foreach (var group in groupsByFirstFace)
                if (group != null)
                    surfaces.AddRange(group);

            return surfaces;
        }

        // Грани всех кусков по плоскостям: ось и координата плоскости; внутри — по порядку кусков и граней.
        private static Dictionary<(int axis, int plane), List<BoxModelSliceFace>> FacesBySlice(IReadOnlyList<BoxModelCell> cells)
        {
            var slices = new Dictionary<(int axis, int plane), List<BoxModelSliceFace>>();

            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var cell = cells[cellIndex];

                for (var face = 0; face < BoxFaces.Count; face++)
                {
                    var key = (BoxFaces.Axis(face), BoxModelSurfaceFinder.PlaneOf(cell.Box, face));

                    if (!slices.TryGetValue(key, out var faces))
                        slices[key] = faces = new List<BoxModelSliceFace>();

                    faces.Add(new BoxModelSliceFace(cellIndex, face, BoxModelSurfaceFinder.AreaOf(cell.Box, face), cell.FaceSources[face]));
                }
            }

            return slices;
        }


        private BoxModelSurfaceSlice Find((int axis, int plane) key, List<BoxModelSliceFace> faces, bool isStoring)
        {
            if (currentSlices.TryGetValue(key, out var slice) && slice.HasFaces(faces))
                return slice;

            if (!previousSlices.TryGetValue(key, out slice) || !slice.HasFaces(faces))
                return null;

            if (isStoring)
                currentSlices[key] = slice;

            return slice;
        }


        /// <summary>Грань куска в своей плоскости: номер куска в списке, сторона, прямоугольник и коробка, которая её создала.</summary>
        private readonly struct BoxModelSliceFace
        {
            public BoxModelSliceFace(int cellIndex, int face, RectInt area, int source)
            {
                CellIndex = cellIndex;
                Face = face;
                Area = area;
                Source = source;
            }


            public int CellIndex { get; }
            public int Face { get; }
            public RectInt Area { get; }
            public int Source { get; }


            // Номер куска не сравнивается: в плоскости важен только порядок граней, а он у списка тот же.
            public bool IsSameAs(BoxModelSliceFace other)
            {
                return Face == other.Face && Source == other.Source && Area.xMin == other.Area.xMin && Area.yMin == other.Area.yMin
                       && Area.width == other.Area.width && Area.height == other.Area.height;
            }
        }


        /// <summary>Группа «грань, плоскость, коробка» плоскости: слитые поверхности и номер грани плоскости, с которой группа началась.</summary>
        private sealed class BoxModelSurfaceGroup
        {
            public BoxModelSurfaceGroup(int firstFace)
            {
                FirstFace = firstFace;
            }


            public int FirstFace { get; }
            public List<RectInt> Areas { get; } = new();
            public List<BoxModelSurface> Surfaces { get; } = new();
        }


        /// <summary>
        /// Одна плоскость, посчитанная как у эталона: каждую грань закрывают встречные грани той же плоскости по порядку,
        /// остаток сливается в группах «грань, коробка». Помнит грани, по которым посчитана.
        /// </summary>
        private sealed class BoxModelSurfaceSlice
        {
            private readonly BoxModelSliceFace[] faces;


            private BoxModelSurfaceSlice(BoxModelSliceFace[] faces, List<BoxModelSurfaceGroup> groups)
            {
                this.faces = faces;
                Groups = groups;
            }


            public List<BoxModelSurfaceGroup> Groups { get; }


            public static BoxModelSurfaceSlice Solve(int plane, List<BoxModelSliceFace> faces)
            {
                var groups = new List<BoxModelSurfaceGroup>();
                var groupsBySideAndSource = new Dictionary<(int face, int source), BoxModelSurfaceGroup>();

                for (var index = 0; index < faces.Count; index++)
                {
                    var visible = VisiblePartOf(faces, faces[index]);

                    if (visible.Count == 0)
                        continue;

                    var key = (faces[index].Face, faces[index].Source);

                    if (!groupsBySideAndSource.TryGetValue(key, out var group))
                    {
                        groupsBySideAndSource[key] = group = new BoxModelSurfaceGroup(index);
                        groups.Add(group);
                    }

                    group.Areas.AddRange(visible);
                }

                foreach (var group in groups)
                {
                    BoxModelSurfaceFinder.MergeAreas(group.Areas);
                    var face = faces[group.FirstFace].Face;

                    foreach (var area in group.Areas)
                        group.Surfaces.Add(new BoxModelSurface(face, plane, area, faces[group.FirstFace].Source));
                }

                return new BoxModelSurfaceSlice(faces.ToArray(), groups);
            }

            // Встречная грань мимо прямоугольника не меняет ни один его остаток — её можно пропустить, порядок остальных тот же.
            private static List<RectInt> VisiblePartOf(List<BoxModelSliceFace> faces, BoxModelSliceFace face)
            {
                var visible = new List<RectInt> { face.Area };
                var coveringSide = BoxFaces.Opposite(face.Face);

                foreach (var cover in faces)
                {
                    if (visible.Count == 0)
                        break;

                    if (cover.Face == coveringSide && BoxModelSurfaceFinder.Overlaps(face.Area, cover.Area))
                        visible = BoxModelSurfaceFinder.Subtract(visible, cover.Area);
                }

                return visible;
            }


            public bool HasFaces(List<BoxModelSliceFace> otherFaces)
            {
                if (otherFaces.Count != faces.Length)
                    return false;

                for (var index = 0; index < faces.Length; index++)
                    if (!faces[index].IsSameAs(otherFaces[index]))
                        return false;

                return true;
            }
        }
    }
}
